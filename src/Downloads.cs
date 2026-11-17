using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace OrbisWebManager
{
    /// <summary>
    /// Live progress of the console's own downloads (PSN games, updates, DLC and Remote Package
    /// Installer jobs). The PS4 background transfer service (BGFT) keeps one folder per task in
    /// /user/bgft/task/XXXXXXXX/ with a small key/value file (d0.pdb, mirrored to d1.pdb) that it
    /// rewrites as bytes arrive. Reading those over FTP gives the same numbers the console shows.
    /// </summary>
    static class Downloads
    {
        const string TaskRoot = "/user/bgft/task/";

        // d0.pdb keys (u32 key, u32 len, u32 len, value)
        const uint K_TitleName = 0x69, K_IconUrl = 0x6A, K_ContentId = 0x74, K_TitleId = 0x7C,
                   K_Downloaded = 0x7A, K_Total = 0x7B, K_Version = 0x84, K_PkgPath = 0x1389;

        public class Task
        {
            public string taskId, titleId, contentId, name, version, kind, state;
            public long downloaded, total;
            public double percent;
            public long speed;      // bytes/s, 0 when unknown
            public long etaSec;     // -1 when unknown
            public string finishedAt;
        }

        class Sample { public long bytes; public DateTime at; public DateTime lastChange; public double speed; }

        static readonly object sync = new object();
        static readonly Dictionary<string, Sample> samples = new Dictionary<string, Sample>();
        static readonly Dictionary<string, Task> active = new Dictionary<string, Task>();
        static readonly List<Task> finished = new List<Task>();
        static readonly Dictionary<string, byte[]> icons = new Dictionary<string, byte[]>();
        static readonly Dictionary<string, DateTime> noIcon = new Dictionary<string, DateTime>();
        static HashSet<string> ignored;   // task folders whose download is complete
        static readonly Dictionary<string, DateTime> recheck = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        static object lastResult;
        static string lastGood = "";
        static DateTime lastAt = DateTime.MinValue;

        static string IgnoredPath { get { return Path.Combine(Program.CacheDir, "bgft-ignored.json"); } }
        static readonly Regex TaskRx = new Regex("^[0-9a-fA-F]{8}$");

        public static bool IsTaskId(string s) { return s != null && TaskRx.IsMatch(s); }

        public static object Get()
        {
            lock (sync)
            {
                if (lastResult != null && (DateTime.UtcNow - lastAt).TotalSeconds < 3) return lastResult;
                LoadIgnored();

                List<FtpItem> dirs;
                try { dirs = Ftp.List(TaskRoot); }
                catch (Exception ex)
                {
                    // Keep showing the last numbers we read (the console keeps downloading in
                    // rest mode, but GoldHEN's FTP doesn't answer then).
                    var stale = new List<Task>(active.Values);
                    lastResult = new
                    {
                        ok = false,
                        error = "O PS4 não respondeu: " + ex.Message,
                        tasks = stale.ToArray(),
                        finished = finished.ToArray(),
                        updated = lastGood
                    };
                    lastAt = DateTime.UtcNow;
                    return lastResult;
                }

                var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                bool ignoredChanged = false;
                foreach (var d in dirs)
                {
                    if (!d.isDir || !IsTaskId(d.name)) continue;
                    present.Add(d.name);
                    if (ignored.Contains(d.name)) continue;
                    DateTime until;
                    if (recheck.TryGetValue(d.name, out until) && DateTime.UtcNow < until) continue;

                    Task t = ReadTask(d.name);
                    if (t == null || t.total <= 0)
                    {
                        // Not a download (an upload, a capture job…) or not written yet: look again later.
                        recheck[d.name] = DateTime.UtcNow.AddMinutes(15);
                        continue;
                    }
                    recheck.Remove(d.name);
                    if (t.downloaded >= t.total)
                    {
                        if (active.ContainsKey(d.name)) MarkFinished(t);
                        active.Remove(d.name);
                        samples.Remove(d.name);
                        ignored.Add(d.name);
                        ignoredChanged = true;
                        continue;
                    }
                    UpdateSpeed(t);
                    active[d.name] = t;
                }

                // Tasks that vanished while we were watching them: the download finished
                // (the console moves on to installing) or it was cancelled.
                foreach (var id in new List<string>(active.Keys))
                {
                    if (present.Contains(id)) continue;
                    MarkFinished(active[id]);
                    active.Remove(id);
                    samples.Remove(id);
                }
                // Forget ignored folders the console has deleted, so the list doesn't grow forever.
                int before = ignored.Count;
                ignored.RemoveWhere(x => !present.Contains(x));
                if (ignoredChanged || ignored.Count != before) SaveIgnored();

                var list = new List<Task>(active.Values);
                list.Sort((a, b) =>
                {
                    int sa = a.state == "downloading" ? 0 : 1, sb = b.state == "downloading" ? 0 : 1;
                    if (sa != sb) return sa - sb;
                    return b.percent.CompareTo(a.percent);
                });
                finished.RemoveAll(f => (DateTime.Now - DateTime.Parse(f.finishedAt)).TotalHours > 2);

                lastGood = DateTime.Now.ToString("HH:mm:ss");
                lastResult = new { ok = true, tasks = list.ToArray(), finished = finished.ToArray(), updated = lastGood };
                lastAt = DateTime.UtcNow;
                return lastResult;
            }
        }

        static void MarkFinished(Task t)
        {
            t.state = "finished";
            t.speed = 0; t.etaSec = -1;
            t.finishedAt = DateTime.Now.ToString("s");
            finished.RemoveAll(f => f.taskId == t.taskId);
            finished.Insert(0, t);
            if (finished.Count > 10) finished.RemoveAt(finished.Count - 1);
        }

        static void UpdateSpeed(Task t)
        {
            var now = DateTime.UtcNow;
            Sample s;
            if (!samples.TryGetValue(t.taskId, out s))
            {
                samples[t.taskId] = new Sample { bytes = t.downloaded, at = now, lastChange = now };
                t.state = "downloading";   // first look: assume it's moving until proven otherwise
                t.speed = 0; t.etaSec = -1;
                return;
            }
            double secs = (now - s.at).TotalSeconds;
            if (t.downloaded > s.bytes && secs > 0.5)
            {
                double inst = (t.downloaded - s.bytes) / secs;
                s.speed = s.speed <= 0 ? inst : s.speed * 0.6 + inst * 0.4;
                s.lastChange = now;
                s.bytes = t.downloaded; s.at = now;
            }
            else if (t.downloaded < s.bytes)
            {
                s.bytes = t.downloaded; s.at = now; s.lastChange = now; s.speed = 0;
            }
            // The console rewrites the file every few seconds, so only call it stalled after a while.
            bool stalled = (now - s.lastChange).TotalSeconds > 90;
            t.state = stalled ? "paused" : "downloading";
            t.speed = stalled ? 0 : (long)s.speed;
            t.etaSec = t.speed > 0 ? (t.total - t.downloaded) / t.speed : -1;
        }

        static Task ReadTask(string id)
        {
            Dictionary<uint, byte[]> a = Parse(Ftp.TryDownload(TaskRoot + id + "/d0.pdb"));
            if (a == null || !a.ContainsKey(K_Total)) return null;   // uploads, captures, etc.
            // d0 and d1 are written alternately; the newer one has more bytes.
            Dictionary<uint, byte[]> b = Parse(Ftp.TryDownload(TaskRoot + id + "/d1.pdb"));
            if (b != null && U64(b, K_Downloaded) > U64(a, K_Downloaded) && U64(b, K_Total) > 0) a = b;

            var t = new Task
            {
                taskId = id,
                titleId = Str(a, K_TitleId),
                contentId = Str(a, K_ContentId),
                name = Str(a, K_TitleName),
                version = Str(a, K_Version),
                downloaded = (long)U64(a, K_Downloaded),
                total = (long)U64(a, K_Total),
                etaSec = -1
            };
            string pkg = Str(a, K_PkgPath).ToLowerInvariant();
            if (pkg.EndsWith("/patch.pkg")) t.kind = "Atualização";
            else if (pkg.EndsWith("/app.pkg")) t.kind = "Jogo / app";
            else if (t.contentId.Length > 0 && !t.contentId.EndsWith("00000000") && pkg.EndsWith(".pkg")) t.kind = "Conteúdo adicional";
            else t.kind = "Download";
            if (string.IsNullOrEmpty(t.name)) t.name = t.titleId.Length > 0 ? t.titleId : "Download " + id;
            t.percent = t.total > 0 ? Math.Round(100.0 * t.downloaded / t.total, 1) : 0;
            if (t.percent > 100) t.percent = 100;
            return t;
        }

        static Dictionary<uint, byte[]> Parse(byte[] b)
        {
            if (b == null || b.Length < 16) return null;
            var r = new Dictionary<uint, byte[]>();
            int p = 4;
            while (p + 12 <= b.Length)
            {
                uint k = BitConverter.ToUInt32(b, p);
                int len = (int)BitConverter.ToUInt32(b, p + 4);
                p += 12;
                if (len < 0 || p + len > b.Length) break;
                var v = new byte[len];
                Buffer.BlockCopy(b, p, v, 0, len);
                r[k] = v;
                p += len;
            }
            return r;
        }

        static ulong U64(Dictionary<uint, byte[]> d, uint k)
        {
            byte[] v;
            if (!d.TryGetValue(k, out v)) return 0;
            if (v.Length >= 8) return BitConverter.ToUInt64(v, 0);
            if (v.Length >= 4) return BitConverter.ToUInt32(v, 0);
            return 0;
        }

        static string Str(Dictionary<uint, byte[]> d, uint k)
        {
            byte[] v;
            if (!d.TryGetValue(k, out v)) return "";
            return Encoding.UTF8.GetString(v).TrimEnd('\0').Trim();
        }

        /// <summary>The icon the console saved with the task, or null.</summary>
        public static byte[] Icon(string id)
        {
            if (!IsTaskId(id)) return null;
            lock (icons)
            {
                byte[] b;
                if (icons.TryGetValue(id, out b)) return b;
                DateTime retry;
                if (noIcon.TryGetValue(id, out retry) && DateTime.UtcNow < retry) return null;
            }
            byte[] data = Ftp.TryDownload(TaskRoot + id + "/icon.png");
            lock (icons)
            {
                if (data != null && data.Length > 0) icons[id] = data;
                else noIcon[id] = DateTime.UtcNow.AddMinutes(10);   // updates often have no icon.png
            }
            return data;
        }

        static void LoadIgnored()
        {
            if (ignored != null) return;
            ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (File.Exists(IgnoredPath))
                    foreach (var s in new JavaScriptSerializer().Deserialize<string[]>(File.ReadAllText(IgnoredPath)))
                        if (IsTaskId(s)) ignored.Add(s);
            }
            catch { }
        }

        static void SaveIgnored()
        {
            try { File.WriteAllText(IgnoredPath, new JavaScriptSerializer().Serialize(new List<string>(ignored))); }
            catch { }
        }
    }
}
