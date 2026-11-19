using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace OrbisWebManager
{
    // ==================================================================== activity log
    public static class Activity
    {
        public class Entry { public string at { get; set; } public string kind { get; set; } public string text { get; set; } }
        static List<Entry> items;
        static string FilePath { get { return Path.Combine(Program.CacheDir, "activity.json"); } }

        static void Load()
        {
            if (items != null) return;
            items = new List<Entry>();
            try { if (File.Exists(FilePath)) items = new JavaScriptSerializer().Deserialize<List<Entry>>(File.ReadAllText(FilePath)) ?? items; } catch { }
        }

        public static void Add(string kind, string text)
        {
            lock (typeof(Activity))
            {
                Load();
                items.Insert(0, new Entry { at = DateTime.Now.ToString("s"), kind = kind, text = text });
                if (items.Count > 300) items.RemoveRange(300, items.Count - 300);
                try { File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(items)); } catch { }
            }
        }

        public static List<Entry> List(int max)
        {
            lock (typeof(Activity))
            {
                Load();
                return items.GetRange(0, Math.Min(max, items.Count));
            }
        }
    }

    // ==================================================================== play-time history
    /// <summary>GoldHEN only keeps a running total per game, so we snapshot it every day and
    /// derive "minutes played per day" from the differences.</summary>
    public static class History
    {
        // date (yyyy-MM-dd) -> titleId -> total minutes at the last refresh of that day
        static Dictionary<string, Dictionary<string, int>> days;
        static string FilePath { get { return Path.Combine(Program.CacheDir, "history.json"); } }

        static void Load()
        {
            if (days != null) return;
            days = new Dictionary<string, Dictionary<string, int>>();
            try { if (File.Exists(FilePath)) days = new JavaScriptSerializer().Deserialize<Dictionary<string, Dictionary<string, int>>>(File.ReadAllText(FilePath)) ?? days; } catch { }
        }

        public static void Record(List<GameItem> games)
        {
            lock (typeof(History))
            {
                Load();
                string today = DateTime.Now.ToString("yyyy-MM-dd");
                var snap = new Dictionary<string, int>();
                foreach (var g in games) if (g.timePlayedMinutes > 0) snap[g.id] = g.timePlayedMinutes;
                if (snap.Count == 0) return;
                days[today] = snap;
                try { File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(days)); } catch { }
            }
        }

        public static object Get(int span)
        {
            lock (typeof(History))
            {
                Load();
                var keys = new List<string>(days.Keys);
                keys.Sort(StringComparer.Ordinal);
                var perDay = new List<object>();
                var perGame = new Dictionary<string, int>();
                DateTime start = DateTime.Today.AddDays(-(span - 1));
                Dictionary<string, int> prev = null;
                // baseline: last snapshot before the window
                foreach (var k in keys) if (string.CompareOrdinal(k, start.ToString("yyyy-MM-dd")) < 0) prev = days[k];
                for (int i = 0; i < span; i++)
                {
                    string d = start.AddDays(i).ToString("yyyy-MM-dd");
                    Dictionary<string, int> cur;
                    int total = 0;
                    var games = new List<object>();
                    if (days.TryGetValue(d, out cur))
                    {
                        if (prev != null)
                        {
                            foreach (var kv in cur)
                            {
                                int before;
                                prev.TryGetValue(kv.Key, out before);
                                int delta = kv.Value - before;
                                if (delta <= 0 || delta > 24 * 60) continue;
                                total += delta;
                                games.Add(new { id = kv.Key, name = Library.TitleOf(kv.Key), minutes = delta });
                                int acc;
                                perGame.TryGetValue(kv.Key, out acc);
                                perGame[kv.Key] = acc + delta;
                            }
                        }
                        prev = cur;
                    }
                    perDay.Add(new { date = d, minutes = total, games = games, recorded = cur != null });
                }
                var top = new List<object>();
                var list = new List<KeyValuePair<string, int>>(perGame);
                list.Sort((a, b) => b.Value.CompareTo(a.Value));
                foreach (var kv in list) top.Add(new { id = kv.Key, name = Library.TitleOf(kv.Key), minutes = kv.Value, iconUrl = Library.IconUrl(kv.Key) });
                string first = keys.Count > 0 ? keys[0] : "";
                return new { days = perDay, top = top, trackingSince = first };
            }
        }
    }

    // ==================================================================== game updates (PSN)
    public static class Updates
    {
        // Public key the console itself uses to build the patch-info URL (np_<TITLEID>).
        static readonly byte[] Key = Hex("AD62E37F905E06BC19593142281C112CEC0E7EC3E97EFDCAEFCDBAAFA6378D84");
        public class Info { public string id { get; set; } public string latest { get; set; } public long size { get; set; } public string checkedAt { get; set; } public string error { get; set; } public string requiresFw { get; set; } }
        static Dictionary<string, Info> cache;
        static string FilePath { get { return Path.Combine(Program.CacheDir, "updates.json"); } }
        public static volatile bool Checking;
        public static string LastRun = "";

        static byte[] Hex(string s)
        {
            var b = new byte[s.Length / 2];
            for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
            return b;
        }

        static void Load()
        {
            if (cache != null) return;
            cache = new Dictionary<string, Info>(StringComparer.OrdinalIgnoreCase);
            try { if (File.Exists(FilePath)) cache = new Dictionary<string, Info>(new JavaScriptSerializer().Deserialize<Dictionary<string, Info>>(File.ReadAllText(FilePath)) ?? cache, StringComparer.OrdinalIgnoreCase); } catch { }
        }

        static void Save()
        {
            try { File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(cache)); } catch { }
        }

        public static Info Check(string id)
        {
            string hash;
            using (var h = new HMACSHA256(Key))
            {
                var sb = new StringBuilder();
                foreach (byte x in h.ComputeHash(Encoding.ASCII.GetBytes("np_" + id))) sb.Append(x.ToString("x2"));
                hash = sb.ToString();
            }
            // Sony's https endpoint uses a certificate Windows doesn't trust; the same file is served over http.
            string url = "http://gs-sec.ww.np.dl.playstation.net/plo/np/" + id + "/" + hash + "/" + id + "-ver.xml";
            var info = new Info { id = id, latest = "", checkedAt = DateTime.Now.ToString("s") };
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = 12000;
                req.UserAgent = "Mozilla/5.0 (PlayStation 4)";
                using (var res = (HttpWebResponse)req.GetResponse())
                using (var r = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
                {
                    string xml = r.ReadToEnd();
                    var m = Regex.Match(xml, "<package[^>]*\\bversion=\"([0-9.]+)\"[^>]*\\bsize=\"(\\d+)\"");
                    if (m.Success) { info.latest = m.Groups[1].Value; info.size = long.Parse(m.Groups[2].Value); }
                    // system_ver is BCD-ish: 0x13520000 -> 13.52
                    var sv = Regex.Match(xml, "<package[^>]*\\bsystem_ver=\"(\\d+)\"");
                    if (sv.Success)
                    {
                        string hex = long.Parse(sv.Groups[1].Value).ToString("X8");
                        info.requiresFw = hex.Substring(0, 2).TrimStart('0') + "." + hex.Substring(2, 2);
                        if (info.requiresFw.StartsWith(".")) info.requiresFw = "0" + info.requiresFw;
                    }
                }
            }
            catch (WebException ex)
            {
                var hr = ex.Response as HttpWebResponse;
                // 404 = the game never had an update
                if (hr == null || hr.StatusCode != HttpStatusCode.NotFound) info.error = ex.Message;
            }
            return info;
        }

        public static object Get(bool force)
        {
            lock (typeof(Updates)) Load();
            var games = Library.Games();
            if (force && !Checking)
            {
                Checking = true;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        var ids = new List<string>();
                        foreach (var g in games)
                            if (Regex.IsMatch(g.id ?? "", "^(CUSA|PLAS|PCAS|PCJS|PLJS|PLJM|PCKS)\\d{5}$")) ids.Add(g.id);
                        int next = -1;
                        var threads = new List<Thread>();
                        for (int t = 0; t < 6; t++)
                        {
                            var th = new Thread(() =>
                            {
                                while (true)
                                {
                                    int i = Interlocked.Increment(ref next);
                                    if (i >= ids.Count) break;
                                    var info = Check(ids[i]);
                                    lock (typeof(Updates)) cache[ids[i]] = info;
                                }
                            });
                            th.IsBackground = true;
                            th.Start();
                            threads.Add(th);
                        }
                        foreach (var th in threads) th.Join();
                        lock (typeof(Updates)) Save();
                        LastRun = DateTime.Now.ToString("dd/MM HH:mm");
                    }
                    finally { Checking = false; }
                });
            }

            var list = new List<object>();
            int available = 0;
            lock (typeof(Updates))
            {
                foreach (var g in games)
                {
                    Info u;
                    if (!cache.TryGetValue(g.id, out u)) continue;
                    bool has = u.latest.Length > 0 && g.version.Length > 0 && Pkg.CompareVersion(u.latest, g.version) > 0;
                    if (has) available++;
                    string fw = Program.Settings.ps4_fw ?? "";
                    bool tooNew = has && !string.IsNullOrEmpty(u.requiresFw) && fw.Length > 0 && Pkg.CompareVersion(u.requiresFw, fw) > 0;
                    list.Add(new { id = g.id, name = g.name, installed = g.version, latest = u.latest, size = u.size, hasUpdate = has, requiresFw = u.requiresFw ?? "", needsNewerFw = tooNew, error = u.error, iconUrl = g.iconUrl });
                }
            }
            list.Sort((a, b) =>
            {
                bool ha = (bool)a.GetType().GetProperty("hasUpdate").GetValue(a, null);
                bool hb = (bool)b.GetType().GetProperty("hasUpdate").GetValue(b, null);
                return hb.CompareTo(ha);
            });
            return new { checking = Checking, lastRun = LastRun, available = available, games = list, everChecked = cache.Count > 0 };
        }
    }

    // ==================================================================== disk usage + DLC
    public static class Storage
    {
        public class Dlc { public string folder { get; set; } public string name { get; set; } public long size { get; set; } public string contentId { get; set; } }
        public class Usage { public string id { get; set; } public long app { get; set; } public long patch { get; set; } public long dlc { get; set; } public List<Dlc> dlcs { get; set; } public string sig { get; set; } }
        static Dictionary<string, Usage> cache;
        static string FilePath { get { return Path.Combine(Program.CacheDir, "storage.json"); } }
        public static volatile bool Scanning;
        public static int Done, Total;

        static void Load()
        {
            if (cache != null) return;
            cache = new Dictionary<string, Usage>(StringComparer.OrdinalIgnoreCase);
            try { if (File.Exists(FilePath)) cache = new Dictionary<string, Usage>(new JavaScriptSerializer().Deserialize<Dictionary<string, Usage>>(File.ReadAllText(FilePath)) ?? cache, StringComparer.OrdinalIgnoreCase); } catch { }
        }

        static long SumPkgs(string dir)
        {
            long s = 0;
            foreach (var f in Ftp.TryList(dir)) if (!f.isDir) s += f.size;
            return s;
        }

        public static Usage Scan(string id)
        {
            var u = new Usage { id = id, dlcs = new List<Dlc>() };
            u.app = SumPkgs("/user/app/" + id + "/");
            u.patch = SumPkgs("/user/patch/" + id + "/");
            foreach (var d in Ftp.TryList("/user/addcont/" + id + "/"))
            {
                if (!d.isDir) continue;
                var files = Ftp.TryList("/user/addcont/" + id + "/" + d.name + "/");
                long size = 0;
                string pkgPath = null;
                foreach (var f in files) { if (!f.isDir) size += f.size; if (f.name.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase)) pkgPath = "/user/addcont/" + id + "/" + d.name + "/" + f.name; }
                var dlc = new Dlc { folder = d.name, name = d.name, size = size, contentId = "" };
                if (pkgPath != null)
                {
                    var info = Pkg.InfoRemoteCached(pkgPath, size);
                    if (info != null && string.IsNullOrEmpty(info.error))
                    {
                        if (!string.IsNullOrEmpty(info.title)) dlc.name = info.title;
                        dlc.contentId = info.contentId;
                    }
                }
                u.dlc += size;
                u.dlcs.Add(dlc);
            }
            return u;
        }

        public static object Get(bool force)
        {
            lock (typeof(Storage)) Load();
            var games = Library.Games();
            if (force && !Scanning)
            {
                Scanning = true;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    Ftp.BackgroundThread = true;
                    try
                    {
                        Total = games.Count; Done = 0;
                        foreach (var g in games)
                        {
                            var u = Scan(g.id);
                            lock (typeof(Storage)) cache[g.id] = u;
                            Done++;
                        }
                        lock (typeof(Storage))
                        {
                            try { File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(cache)); } catch { }
                        }
                    }
                    finally { Scanning = false; Ftp.BackgroundThread = false; }
                });
            }
            var list = new List<object>();
            long total = 0;
            lock (typeof(Storage))
            {
                foreach (var g in games)
                {
                    Usage u;
                    if (!cache.TryGetValue(g.id, out u)) continue;
                    long t = u.app + u.patch + u.dlc;
                    total += t;
                    list.Add(new { id = g.id, name = g.name, iconUrl = g.iconUrl, app = u.app, patch = u.patch, dlc = u.dlc, total = t, dlcs = u.dlcs, minutes = g.timePlayedMinutes });
                }
            }
            list.Sort((a, b) => ((long)b.GetType().GetProperty("total").GetValue(b, null)).CompareTo((long)a.GetType().GetProperty("total").GetValue(a, null)));
            return new { scanning = Scanning, done = Done, total = Total, bytes = total, games = list };
        }

        public static Usage For(string id, bool rescan)
        {
            lock (typeof(Storage)) Load();
            Usage u;
            lock (typeof(Storage)) cache.TryGetValue(id, out u);
            if (u == null || rescan)
            {
                u = Scan(id);
                lock (typeof(Storage)) cache[id] = u;
            }
            return u;
        }
    }

    // ==================================================================== now playing (ps4debug)
    public static class NowPlaying
    {
        public class Current { public string id; public string name; public DateTime since; public string source; }
        static Current current;
        static DateTime lastPoll = DateTime.MinValue;
        static string lastError = "";

        const uint Magic = 0xFFAABBCC, CmdProcList = 0xBDAA0001, CmdProcInfo = 0xBDAA000D, Success = 0x80000000;

        static void Send(NetworkStream s, uint cmd, byte[] packet)
        {
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write(Magic); w.Write(cmd); w.Write((uint)(packet == null ? 0 : packet.Length));
            byte[] h = ms.ToArray();
            s.Write(h, 0, h.Length);
            if (packet != null && packet.Length > 0) s.Write(packet, 0, packet.Length);
        }

        static byte[] Recv(NetworkStream s, int n)
        {
            var b = new byte[n];
            int got = 0;
            while (got < n)
            {
                int r = s.Read(b, got, n - got);
                if (r <= 0) throw new IOException("ps4debug fechou a conexão");
                got += r;
            }
            return b;
        }

        /// <summary>Title ID of the game/app running now (null when on the home screen).</summary>
        static string RunningTitle()
        {
            using (var c = new TcpClient())
            {
                var ar = c.BeginConnect(Program.Settings.ps4_ip, Ps4Services.Ps4DebugPort, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(1200) || !c.Connected) throw new Exception("NO_PS4DEBUG");
                c.EndConnect(ar);
                c.ReceiveTimeout = 4000; c.SendTimeout = 4000;
                var s = c.GetStream();
                Send(s, CmdProcList, null);
                if (BitConverter.ToUInt32(Recv(s, 4), 0) != Success) throw new Exception("ps4debug recusou a lista de processos");
                int count = BitConverter.ToInt32(Recv(s, 4), 0);
                if (count < 0 || count > 2000) throw new Exception("resposta inválida");
                byte[] data = Recv(s, count * 36);
                var pids = new List<int>();
                for (int i = 0; i < count; i++)
                {
                    string name = Encoding.ASCII.GetString(data, i * 36, 32).TrimEnd('\0');
                    int pid = BitConverter.ToInt32(data, i * 36 + 32);
                    if (name == "eboot.bin") pids.Add(pid);
                }
                foreach (int pid in pids)
                {
                    Send(s, CmdProcInfo, BitConverter.GetBytes(pid));
                    if (BitConverter.ToUInt32(Recv(s, 4), 0) != Success) continue;
                    byte[] info = Recv(s, 188);
                    string tid = Encoding.ASCII.GetString(info, 4 + 40 + 64, 16).TrimEnd('\0');
                    if (Regex.IsMatch(tid, "^[A-Z]{4}\\d{5}$")) return tid;
                }
                return null;
            }
        }

        public static object Get()
        {
            lock (typeof(NowPlaying))
            {
                if ((DateTime.UtcNow - lastPoll).TotalSeconds >= 10)
                {
                    lastPoll = DateTime.UtcNow;
                    try
                    {
                        string id = RunningTitle();
                        lastError = "";
                        if (id == null) current = null;
                        else if (current == null || current.id != id)
                            current = new Current { id = id, name = Library.TitleOf(id), since = DateTime.Now, source = "ps4debug" };
                    }
                    catch (Exception ex)
                    {
                        lastError = ex.Message == "NO_PS4DEBUG" ? "NO_PS4DEBUG" : ex.Message;
                    }
                }
                if (current == null) return new { playing = false, needsPs4Debug = lastError == "NO_PS4DEBUG", error = lastError };
                return new
                {
                    playing = true,
                    id = current.id,
                    name = current.name,
                    iconUrl = Library.IconUrl(current.id),
                    since = current.since.ToString("HH:mm"),
                    minutes = (int)(DateTime.Now - current.since).TotalMinutes
                };
            }
        }

        public static Current Snapshot() { lock (typeof(NowPlaying)) return current; }

        // ------------------------------------------------ play-time limit watcher
        static DateTime lastLimitNotice = DateTime.MinValue, lastCurfewNotice = DateTime.MinValue;

        public static void StartWatcher()
        {
            var t = new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(60000);
                    try { Tick(); } catch { }
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        static void Tick()
        {
            var s = Program.Settings;
            if (s.playlimit_minutes <= 0 && string.IsNullOrEmpty(s.playlimit_curfew)) return;
            Get();
            var cur = Snapshot();
            if (cur == null) return;
            int played = (int)(DateTime.Now - cur.since).TotalMinutes;
            if (s.playlimit_minutes > 0 && played >= s.playlimit_minutes && (DateTime.Now - lastLimitNotice).TotalMinutes >= 15)
            {
                lastLimitNotice = DateTime.Now;
                Ps4Services.Notify("Você está jogando " + cur.name + " há " + FormatMinutes(played) + ". Que tal uma pausa?");
                Activity.Add("limit", "Aviso de tempo de jogo enviado (" + cur.name + ", " + FormatMinutes(played) + ")");
            }
            TimeSpan curfew;
            if (!string.IsNullOrEmpty(s.playlimit_curfew) && TimeSpan.TryParse(s.playlimit_curfew, out curfew))
            {
                var now = DateTime.Now.TimeOfDay;
                bool late = now >= curfew && now < curfew.Add(TimeSpan.FromHours(6));
                if (late && (DateTime.Now - lastCurfewNotice).TotalMinutes >= 15)
                {
                    lastCurfewNotice = DateTime.Now;
                    Ps4Services.Notify("Já passou das " + s.playlimit_curfew + ". Hora de salvar o jogo e desligar!");
                    Activity.Add("limit", "Aviso de horário enviado (" + s.playlimit_curfew + ")");
                }
            }
        }

        public static string FormatMinutes(int m)
        {
            int h = m / 60, r = m % 60;
            return h > 0 ? h + "h" + (r > 0 ? " " + r + "min" : "") : r + " min";
        }
    }
}
