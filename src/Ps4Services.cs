using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace OrbisWebManager
{
    /// <summary>Talks to the console's other services: ps4debug (notifications),
    /// BinLoader (payloads), Remote Package Installer, plus port diagnostics.</summary>
    public static class Ps4Services
    {
        public const int Ps4DebugPort = 744;
        public const int RpiPort = 12800;

        public static bool PortOpen(int port, int timeoutMs)
        {
            try
            {
                using (var c = new TcpClient())
                {
                    var ar = c.BeginConnect(Program.Settings.ps4_ip, port, null, null);
                    bool ok = ar.AsyncWaitHandle.WaitOne(timeoutMs) && c.Connected;
                    if (ok) c.EndConnect(ar);
                    return ok;
                }
            }
            catch { return false; }
        }

        public static long Ping(int timeoutMs)
        {
            try
            {
                using (var p = new Ping())
                {
                    var r = p.Send(Program.Settings.ps4_ip, timeoutMs);
                    if (r.Status == IPStatus.Success) return r.RoundtripTime;
                }
            }
            catch { }
            return -1;
        }

        /// <summary>Checks every known service port in parallel (on demand only:
        /// probing the BinLoader with empty connections on every poll is a bad idea).</summary>
        public static List<object> Diagnose()
        {
            var ports = new[]
            {
                new { port = Program.Settings.ftp_port, name = "FTP (GoldHEN)", use = "Arquivos, jogos, capas, cheats" },
                new { port = Program.Settings.payload_port, name = "BinLoader (GoldHEN)", use = "Envio de payloads .bin" },
                new { port = Ps4DebugPort, name = "ps4debug", use = "Notificações na TV" },
                new { port = RpiPort, name = "Remote Package Installer", use = "Instalar PKG pela rede" }
            };
            var results = new bool[ports.Length];
            var threads = new List<Thread>();
            for (int i = 0; i < ports.Length; i++)
            {
                int idx = i;
                var t = new Thread(() => { results[idx] = PortOpen(ports[idx].port, 1200); });
                t.IsBackground = true;
                t.Start();
                threads.Add(t);
            }
            foreach (var t in threads) t.Join();
            var list = new List<object>();
            for (int i = 0; i < ports.Length; i++)
                list.Add(new { port = ports[i].port, name = ports[i].name, use = ports[i].use, open = results[i] });
            return list;
        }

        // ------------------------------------------------------------ notifications

        /// <summary>
        /// Sends an on-screen notification using the ps4debug protocol (port 744).
        ///
        /// Why the old version never showed anything: it wrote the message TEXT to the
        /// BinLoader port (9090). The BinLoader expects an executable payload, so the
        /// console received garbage bytes as "code" - at best ignored, at worst it
        /// crashed the loader (which would also explain 9090 being closed afterwards).
        /// </summary>
        public static void Notify(string message)
        {
            const uint Magic = 0xFFAABBCC;
            const uint CmdNotify = 0xBDDD0004;
            const uint CmdSuccess = 0x80000000;

            byte[] text = Encoding.UTF8.GetBytes(message + "\0");
            using (var c = new TcpClient())
            {
                var ar = c.BeginConnect(Program.Settings.ps4_ip, Ps4DebugPort, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(1500) || !c.Connected)
                    throw new Exception("NO_PS4DEBUG");
                c.EndConnect(ar);
                c.ReceiveTimeout = 4000;
                c.SendTimeout = 4000;
                var s = c.GetStream();
                var hdr = new MemoryStream();
                var w = new BinaryWriter(hdr);
                w.Write(Magic);
                w.Write(CmdNotify);
                w.Write((uint)8);        // size of the notify packet below
                w.Write((int)222);       // message type used by sceSysUtilSendSystemNotificationWithText
                w.Write(text.Length);
                byte[] h = hdr.ToArray();
                s.Write(h, 0, 12);       // command header
                s.Write(h, 12, 8);       // notify packet
                s.Write(text, 0, text.Length);
                byte[] st = new byte[4];
                int got = 0;
                while (got < 4)
                {
                    int n = s.Read(st, got, 4 - got);
                    if (n <= 0) break;
                    got += n;
                }
                if (got == 4 && BitConverter.ToUInt32(st, 0) != CmdSuccess)
                    throw new Exception("ps4debug respondeu com erro 0x" + BitConverter.ToUInt32(st, 0).ToString("X8"));
            }
        }

        // ------------------------------------------------------------ payloads

        public static void SendPayload(byte[] payload)
        {
            using (var c = new TcpClient())
            {
                var ar = c.BeginConnect(Program.Settings.ps4_ip, Program.Settings.payload_port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(2000) || !c.Connected)
                    throw new Exception("A porta " + Program.Settings.payload_port + " (BinLoader) está fechada. No PS4: Configurações do GoldHEN > Server Settings > ative 'BinLoader Server'.");
                c.EndConnect(ar);
                c.SendTimeout = 15000;
                var s = c.GetStream();
                s.Write(payload, 0, payload.Length);
                s.Flush();
                c.Client.Shutdown(SocketShutdown.Send);
            }
        }

        // ------------------------------------------------------------ ps4debug loader

        /// <summary>
        /// Downloads the official ps4debug payload from the GoldHEN GitHub releases
        /// (cached in cache/payloads), sends it to the BinLoader and waits for port 744.
        /// </summary>
        public static string LoadPs4Debug()
        {
            if (PortOpen(Ps4DebugPort, 1200)) return "O ps4debug já está rodando no PS4. Pode enviar notificações!";
            if (!PortOpen(Program.Settings.payload_port, 1500))
                throw new Exception("O BinLoader (porta " + Program.Settings.payload_port + ") está fechado no PS4, então não dá para enviar o ps4debug. " +
                    "No PS4: reinicie o console e ative o GoldHEN de novo (o BinLoader já está ligado no seu config.ini). Depois clique aqui outra vez.");

            string dir = Path.Combine(Program.CacheDir, "payloads");
            Directory.CreateDirectory(dir);
            byte[] payload = null;
            string name = null;

            try
            {
                string json = Covers.HttpGetString("https://api.github.com/repos/GoldHEN/ps4debug/releases/latest", null);
                var rel = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
                object assets;
                if (rel != null && rel.TryGetValue("assets", out assets) && assets is object[])
                {
                    foreach (var a in (object[])assets)
                    {
                        var ad = a as Dictionary<string, object>;
                        if (ad == null) continue;
                        string n = Convert.ToString(ad["name"]);
                        string url = Convert.ToString(ad["browser_download_url"]);
                        if (!n.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!url.StartsWith("https://github.com/GoldHEN/ps4debug/releases/download/")) continue;
                        name = Path.GetFileName(n);
                        string local = Path.Combine(dir, name);
                        if (File.Exists(local)) payload = File.ReadAllBytes(local);
                        else
                        {
                            payload = Covers.HttpGetBytes(url);
                            File.WriteAllBytes(local, payload);
                        }
                        break;
                    }
                }
            }
            catch { /* offline: fall back to the cached copy below */ }

            if (payload == null)
            {
                var cached = Directory.GetFiles(dir, "ps4debug*.bin");
                if (cached.Length == 0) throw new Exception("Não foi possível baixar o ps4debug do GitHub (sem internet?). Baixe manualmente em github.com/GoldHEN/ps4debug/releases e envie pelo card 'Injeção de Payload'.");
                Array.Sort(cached);
                name = Path.GetFileName(cached[cached.Length - 1]);
                payload = File.ReadAllBytes(cached[cached.Length - 1]);
            }

            SendPayload(payload);
            for (int i = 0; i < 16; i++)
            {
                Thread.Sleep(500);
                if (PortOpen(Ps4DebugPort, 600)) return name + " carregado! As notificações na TV já funcionam.";
            }
            return name + " foi enviado ao PS4. Se a porta 744 não abrir em alguns segundos, veja se apareceu algum aviso na TV.";
        }

        // ------------------------------------------------------------ Remote Package Installer

        public static string RpiCall(string api, object body)
        {
            var req = (HttpWebRequest)WebRequest.Create("http://" + Program.Settings.ps4_ip + ":" + RpiPort + "/api/" + api);
            req.Method = "POST";
            req.ContentType = "application/json";
            req.Timeout = 8000;
            byte[] data = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(body));
            req.ContentLength = data.Length;
            try
            {
                using (var s = req.GetRequestStream()) s.Write(data, 0, data.Length);
                using (var res = (HttpWebResponse)req.GetResponse())
                using (var r = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
                {
                    return r.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    using (var r = new StreamReader(ex.Response.GetResponseStream(), Encoding.UTF8)) return r.ReadToEnd();
                }
                throw new Exception("Remote Package Installer não respondeu na porta " + RpiPort + ". Abra o app 'Remote Package Installer' no PS4 e deixe-o aberto.");
            }
        }

        // ------------------------------------------------------------ network helpers

        /// <summary>The PC address the PS4 can reach (the one on the route to the console).</summary>
        public static string LocalIpTowardsConsole()
        {
            try
            {
                using (var u = new UdpClient())
                {
                    u.Connect(Program.Settings.ps4_ip, 9);
                    return ((IPEndPoint)u.Client.LocalEndPoint).Address.ToString();
                }
            }
            catch { }
            var ips = LocalIps();
            return ips.Count > 0 ? ips[0] : "127.0.0.1";
        }

        public static List<string> LocalIps()
        {
            var ips = new List<string>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ua.Address))
                        {
                            string s = ua.Address.ToString();
                            if (!s.StartsWith("169.254.") && !ips.Contains(s)) ips.Add(s);
                        }
                    }
                }
            }
            catch { }
            // Put the address on the console's route first
            try
            {
                using (var u = new UdpClient())
                {
                    u.Connect(Program.Settings.ps4_ip, 9);
                    string best = ((IPEndPoint)u.Client.LocalEndPoint).Address.ToString();
                    if (ips.Remove(best)) ips.Insert(0, best);
                }
            }
            catch { }
            return ips;
        }
    }
}
