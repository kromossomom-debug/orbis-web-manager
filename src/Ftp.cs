using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace OrbisWebManager
{
    public class FtpItem
    {
        public string name { get; set; }
        public bool isDir { get; set; }
        public long size { get; set; }
        public string date { get; set; }
        public string permissions { get; set; }
    }

    public class FtpException : Exception
    {
        public int Code;
        public FtpException(int code, string msg) : base(msg) { Code = code; }
    }

    /// <summary>
    /// Lightweight FTP client that keeps its control connection open.
    /// The old implementation used FtpWebRequest with KeepAlive=false, which
    /// re-did TCP connect + login + CWD for EVERY file/listing (and serialized
    /// everything behind one global lock). Reusing connections makes each
    /// operation a couple of round trips instead of ~8.
    /// </summary>
    public class FtpConnection : IDisposable
    {
        const int CtrlTimeout = 8000;
        const int DataTimeout = 20000;

        readonly string host;
        readonly int port;
        TcpClient ctrl;
        NetworkStream ns;
        StreamReader reader;
        public string Banner = "";
        public DateTime LastUsed = DateTime.UtcNow;
        public bool Broken;

        public FtpConnection(string host, int port)
        {
            this.host = host;
            this.port = port;
        }

        public void Connect()
        {
            ctrl = new TcpClient();
            ctrl.NoDelay = true;
            var ar = ctrl.BeginConnect(host, port, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(3000) || !ctrl.Connected)
            {
                try { ctrl.Close(); } catch { }
                throw new IOException("Não foi possível conectar ao FTP " + host + ":" + port + " (tempo esgotado).");
            }
            ctrl.EndConnect(ar);
            ctrl.ReceiveTimeout = CtrlTimeout;
            ctrl.SendTimeout = CtrlTimeout;
            ns = ctrl.GetStream();
            reader = new StreamReader(ns, new UTF8Encoding(false));

            var hello = ReadReply();
            if (hello.Code != 220) throw new FtpException(hello.Code, hello.Text);
            Banner = hello.Text;

            var u = Cmd("USER anonymous");
            if (u.Code == 331) u = Cmd("PASS anonymous");
            if (u.Code != 230 && u.Code != 202) throw new FtpException(u.Code, "Login FTP recusado: " + u.Text);
            Expect(Cmd("TYPE I"), 200);
        }

        public struct Reply
        {
            public int Code;
            public string Text;
        }

        Reply ReadReply()
        {
            string line = reader.ReadLine();
            if (line == null) throw new IOException("Conexão FTP fechada pelo console.");
            var sb = new StringBuilder(line);
            if (line.Length >= 4 && line[3] == '-')
            {
                string end = line.Substring(0, 3) + " ";
                while (true)
                {
                    string l = reader.ReadLine();
                    if (l == null) throw new IOException("Conexão FTP fechada pelo console.");
                    sb.Append('\n').Append(l);
                    if (l.StartsWith(end)) break;
                }
            }
            int code;
            int.TryParse(line.Length >= 3 ? line.Substring(0, 3) : "0", out code);
            return new Reply { Code = code, Text = sb.ToString() };
        }

        public Reply Cmd(string command)
        {
            LastUsed = DateTime.UtcNow;
            byte[] b = Encoding.UTF8.GetBytes(command + "\r\n");
            ns.Write(b, 0, b.Length);
            return ReadReply();
        }

        static void Expect(Reply r, params int[] ok)
        {
            foreach (int c in ok) if (r.Code == c) return;
            throw new FtpException(r.Code, r.Text);
        }

        TcpClient OpenPassive()
        {
            var r = Cmd("PASV");
            Expect(r, 227);
            var m = Regex.Match(r.Text, @"(\d+),(\d+),(\d+),(\d+),(\d+),(\d+)");
            if (!m.Success) throw new FtpException(r.Code, "Resposta PASV inválida: " + r.Text);
            int p = int.Parse(m.Groups[5].Value) * 256 + int.Parse(m.Groups[6].Value);
            var data = new TcpClient();
            // Always use the console IP we already know (some servers report 0.0.0.0 / internal IPs)
            var ar = data.BeginConnect(host, p, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(4000) || !data.Connected)
            {
                try { data.Close(); } catch { }
                throw new IOException("Falha ao abrir conexão de dados FTP.");
            }
            data.EndConnect(ar);
            data.ReceiveTimeout = DataTimeout;
            data.SendTimeout = DataTimeout;
            return data;
        }

        /// <summary>Runs a data-channel command. The reply is read BEFORE touching the data
        /// socket, so a 550 (path not found) returns immediately instead of hanging.</summary>
        void DataCommand(string command, Action<NetworkStream> body)
        {
            using (var data = OpenPassive())
            {
                var pre = Cmd(command);
                if (pre.Code != 150 && pre.Code != 125)
                {
                    throw new FtpException(pre.Code, pre.Text);
                }
                using (var ds = data.GetStream())
                {
                    body(ds);
                }
            }
            var post = ReadReply();
            Expect(post, 226, 250);
        }

        static readonly Regex ListRegex = new Regex(
            @"^([dlrwxsStT\-]{10})\s+\d+\s+\S+\s+\S+\s+(\d+)\s+([A-Za-z]{3}\s+\d+\s+[\d:]+)\s+(.+)$",
            RegexOptions.Compiled);

        public List<FtpItem> List(string path)
        {
            var items = new List<FtpItem>();
            string raw = "";
            DataCommand("LIST " + path, ds =>
            {
                using (var sr = new StreamReader(ds, new UTF8Encoding(false)))
                {
                    raw = sr.ReadToEnd();
                }
            });

            foreach (string rawLine in raw.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');
                var m = ListRegex.Match(line);
                if (!m.Success) continue;
                string name = m.Groups[4].Value;
                if (name == "." || name == "..") continue;
                items.Add(new FtpItem
                {
                    permissions = m.Groups[1].Value,
                    isDir = m.Groups[1].Value[0] == 'd',
                    size = long.Parse(m.Groups[2].Value),
                    date = Regex.Replace(m.Groups[3].Value, @"\s+", " "),
                    name = name
                });
            }

            items.Sort((a, b) =>
            {
                if (a.isDir && !b.isDir) return -1;
                if (!a.isDir && b.isDir) return 1;
                return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
            });
            return items;
        }

        public void Retrieve(string path, Stream destination, long offset)
        {
            if (offset > 0) Expect(Cmd("REST " + offset), 350);
            DataCommand("RETR " + path, ds =>
            {
                byte[] buf = new byte[256 * 1024];
                int read;
                while ((read = ds.Read(buf, 0, buf.Length)) > 0)
                {
                    destination.Write(buf, 0, read);
                }
            });
        }

        public byte[] RetrieveBytes(string path)
        {
            using (var ms = new MemoryStream())
            {
                Retrieve(path, ms, 0);
                return ms.ToArray();
            }
        }

        public void Store(string path, Stream source, Action<long> progress)
        {
            DataCommand("STOR " + path, ds =>
            {
                byte[] buf = new byte[256 * 1024];
                int read;
                long total = 0;
                while ((read = source.Read(buf, 0, buf.Length)) > 0)
                {
                    ds.Write(buf, 0, read);
                    total += read;
                    if (progress != null) progress(total);
                }
                ds.Flush();
            });
        }

        public long Size(string path)
        {
            var r = Cmd("SIZE " + path);
            if (r.Code != 213) return -1;
            long v;
            return long.TryParse(r.Text.Substring(4).Trim(), out v) ? v : -1;
        }

        public string Mdtm(string path)
        {
            var r = Cmd("MDTM " + path);
            return r.Code == 213 ? r.Text.Substring(4).Trim() : null;
        }

        public void Simple(string command, params int[] ok)
        {
            Expect(Cmd(command), ok);
        }

        public void Noop()
        {
            Expect(Cmd("NOOP"), 200);
        }

        public void Dispose()
        {
            try { if (ns != null) { byte[] q = Encoding.ASCII.GetBytes("QUIT\r\n"); ns.Write(q, 0, q.Length); } } catch { }
            try { if (ctrl != null) ctrl.Close(); } catch { }
        }
    }

    /// <summary>
    /// Connection pool. Measured on a real PS4 (GoldHEN FTP v2.2): one reused
    /// connection does a RETR of a 128 KB file in ~100 ms, but as soon as a second
    /// client connection is open the server slows down to seconds per request
    /// (it services clients in turn). So by default we keep exactly ONE persistent
    /// connection and queue operations on it; a broken connection is transparently
    /// replaced once.
    /// </summary>
    public static class Ftp
    {
        public const int MaxConnections = 1;
        static readonly PrioritySlot Slots = new PrioritySlot();

        /// <summary>Set on threads doing background work (library refresh, dashboard summary).
        /// Their FTP operations yield to anything the user is waiting for (icons, folders...),
        /// so a slow full refresh never makes the UI feel frozen.</summary>
        [ThreadStatic] public static bool BackgroundThread;

        /// <summary>Single-holder lock where foreground waiters always go before background ones.</summary>
        class PrioritySlot
        {
            readonly object gate = new object();
            bool held;
            int foregroundWaiting;

            public bool WaitOne(int timeoutMs)
            {
                bool bg = BackgroundThread;
                DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                lock (gate)
                {
                    if (!bg) foregroundWaiting++;
                    try
                    {
                        while (held || (bg && foregroundWaiting > 0))
                        {
                            int left = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                            if (left <= 0) return false;
                            Monitor.Wait(gate, left);
                        }
                        held = true;
                        return true;
                    }
                    finally
                    {
                        if (!bg) foregroundWaiting--;
                    }
                }
            }

            public void Release()
            {
                lock (gate)
                {
                    held = false;
                    Monitor.PulseAll(gate);
                }
            }
        }
        static readonly Stack<FtpConnection> Idle = new Stack<FtpConnection>();
        static readonly object IdleLock = new object();
        static int generation;
        public static string LastBanner = "";

        static string Host { get { return Program.Settings.ps4_ip; } }
        static int Port { get { return Program.Settings.ftp_port; } }

        /// <summary>Call when IP/port changes.</summary>
        public static void Reset()
        {
            lock (IdleLock)
            {
                generation++;
                while (Idle.Count > 0) Idle.Pop().Dispose();
            }
        }

        static FtpConnection Take(out int gen)
        {
            lock (IdleLock)
            {
                gen = generation;
                while (Idle.Count > 0)
                {
                    var c = Idle.Pop();
                    // Drop connections idle for too long (server may have timed them out)
                    if ((DateTime.UtcNow - c.LastUsed).TotalSeconds < 60) return c;
                    c.Dispose();
                }
            }
            var conn = new FtpConnection(Host, Port);
            conn.Connect();
            LastBanner = conn.Banner;
            return conn;
        }

        static void Give(FtpConnection c, int gen)
        {
            lock (IdleLock)
            {
                if (c.Broken || gen != generation || Idle.Count >= MaxConnections) { c.Dispose(); return; }
                Idle.Push(c);
            }
        }

        public static T Run<T>(Func<FtpConnection, T> op)
        {
            if (!Slots.WaitOne(BackgroundThread ? 600000 : 60000)) throw new IOException("FTP ocupado (tempo de espera esgotado).");
            try
            {
                for (int attempt = 0; ; attempt++)
                {
                    int gen;
                    FtpConnection c = Take(out gen);
                    try
                    {
                        T result = op(c);
                        Give(c, gen);
                        return result;
                    }
                    catch (FtpException)
                    {
                        // Protocol-level error (e.g. 550 not found): connection is still fine
                        Give(c, gen);
                        throw;
                    }
                    catch (Exception)
                    {
                        c.Broken = true;
                        c.Dispose();
                        if (attempt >= 1) throw;
                        // stale pooled connection: retry once on a fresh one
                    }
                }
            }
            finally
            {
                Slots.Release();
            }
        }

        public static void Run(Action<FtpConnection> op)
        {
            Run<bool>(c => { op(c); return true; });
        }

        /// <summary>Name of the long transfer currently holding the connection (shown in the UI).</summary>
        public static volatile string CurrentTransfer = "";

        /// <summary>For streaming transfers (uploads/downloads to the browser): no automatic
        /// retry, because the source/destination stream can't be rewound.</summary>
        public static void RunOnce(string label, Action<FtpConnection> op)
        {
            if (!Slots.WaitOne(120000)) throw new IOException("FTP ocupado com outra transferência. Tente novamente em instantes.");
            CurrentTransfer = label ?? "";
            try
            {
                int gen;
                FtpConnection c = Take(out gen);
                try
                {
                    op(c);
                    Give(c, gen);
                }
                catch (FtpException)
                {
                    Give(c, gen);
                    throw;
                }
                catch
                {
                    c.Broken = true;
                    c.Dispose();
                    throw;
                }
            }
            finally
            {
                CurrentTransfer = "";
                Slots.Release();
            }
        }

        /// <summary>Quick health check. Returns null if the connection is busy with
        /// another operation (which itself means FTP is up).</summary>
        public static bool? TryPing(int waitMs)
        {
            if (!Slots.WaitOne(waitMs)) return null;
            try
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    int gen;
                    FtpConnection c;
                    try { c = Take(out gen); }
                    catch { return false; }
                    try
                    {
                        c.Noop();
                        Give(c, gen);
                        return true;
                    }
                    catch
                    {
                        // stale pooled connection -> try a fresh one
                        c.Broken = true;
                        c.Dispose();
                    }
                }
                return false;
            }
            finally { Slots.Release(); }
        }

        // ---------- Convenience helpers ----------

        public static List<FtpItem> List(string path)
        {
            if (!path.EndsWith("/")) path += "/";
            return Run(c => c.List(path));
        }

        /// <summary>Returns an empty list instead of throwing when the folder doesn't exist / FTP is down.</summary>
        public static List<FtpItem> TryList(string path)
        {
            try { return List(path); } catch { return new List<FtpItem>(); }
        }

        public static byte[] Download(string path)
        {
            return Run(c => c.RetrieveBytes(path));
        }

        public static byte[] TryDownload(string path)
        {
            try { return Download(path); } catch { return null; }
        }

        public static string TryDownloadText(string path)
        {
            var b = TryDownload(path);
            return b == null ? null : Encoding.UTF8.GetString(b);
        }

        public static void DownloadToFile(string remote, string local)
        {
            string tmp = local + ".part";
            try
            {
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                {
                    Run(c => { fs.SetLength(0); fs.Position = 0; c.Retrieve(remote, fs, 0); });
                }
                if (File.Exists(local)) File.Delete(local);
                File.Move(tmp, local);
            }
            finally
            {
                if (File.Exists(tmp)) try { File.Delete(tmp); } catch { }
            }
        }

        public static void Upload(string remote, byte[] data)
        {
            Run(c => { using (var ms = new MemoryStream(data)) c.Store(remote, ms, null); });
        }
    }
}
