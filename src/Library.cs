using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace OrbisWebManager
{
    public class GameMeta
    {
        public string id { get; set; }
        public string name { get; set; }
        public string version { get; set; }
        public string contentId { get; set; }
        public string category { get; set; }
        public string source { get; set; }
    }

    public class StatEntry
    {
        public string sig { get; set; }
        public int minutes { get; set; }
        public int starts { get; set; }
    }

    public class GameItem
    {
        public string id { get; set; }
        public string name { get; set; }
        public string version { get; set; }
        public string category { get; set; }
        public int timePlayedMinutes { get; set; }
        public int gameStarts { get; set; }
        public bool hasCheats { get; set; }
        public bool customCover { get; set; }
        public string iconUrl { get; set; }
        public string installedDate { get; set; }
    }

    public class LibrarySnapshot
    {
        public List<GameItem> games { get; set; }
        public DateTime updated { get; set; }
    }

    /// <summary>
    /// Game library with persistent caches:
    ///  - titles.json : Title ID -> name/version (from param.sfo, read once per game)
    ///  - stats.json  : GoldHEN playtime .ini values (only re-read when the file changed)
    ///  - games.json  : last full list, so the UI shows instantly on startup
    /// A refresh runs in the background; the browser gets the cached list right away.
    /// </summary>
    public static class Library
    {
        static readonly object Sync = new object();
        static readonly object RefreshLock = new object();
        static Dictionary<string, GameMeta> titles = new Dictionary<string, GameMeta>(StringComparer.OrdinalIgnoreCase);
        static Dictionary<string, StatEntry> stats = new Dictionary<string, StatEntry>(StringComparer.OrdinalIgnoreCase);
        static Dictionary<string, string> appDbTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static LibrarySnapshot snapshot;
        static volatile bool refreshing;
        public static string LastError = "";

        static string TitlesPath { get { return Path.Combine(Program.CacheDir, "titles.json"); } }
        static string StatsPath { get { return Path.Combine(Program.CacheDir, "stats.json"); } }
        static string GamesPath { get { return Path.Combine(Program.CacheDir, "games.json"); } }
        static string AppDbPath { get { return Path.Combine(Program.CacheDir, "app.db"); } }

        public static bool Refreshing { get { return refreshing; } }

        public static void Load()
        {
            var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            try
            {
                if (File.Exists(TitlesPath))
                {
                    var t = ser.Deserialize<Dictionary<string, GameMeta>>(File.ReadAllText(TitlesPath));
                    if (t != null) titles = new Dictionary<string, GameMeta>(t, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch { }
            try
            {
                if (File.Exists(StatsPath))
                {
                    var s = ser.Deserialize<Dictionary<string, StatEntry>>(File.ReadAllText(StatsPath));
                    if (s != null) stats = new Dictionary<string, StatEntry>(s, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch { }
            try
            {
                if (File.Exists(GamesPath)) snapshot = ser.Deserialize<LibrarySnapshot>(File.ReadAllText(GamesPath));
                if (snapshot != null)
                {
                    // Force a refresh soon after startup, but show the cached list meanwhile
                    snapshot.updated = DateTime.MinValue;
                    foreach (var g in snapshot.games) g.iconUrl = IconUrl(g.id);
                }
            }
            catch { snapshot = null; }
            if (File.Exists(AppDbPath)) ParseAppDb(File.ReadAllBytes(AppDbPath));
        }

        static void Save()
        {
            var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            try
            {
                lock (Sync)
                {
                    File.WriteAllText(TitlesPath, ser.Serialize(titles));
                    File.WriteAllText(StatsPath, ser.Serialize(stats));
                    if (snapshot != null) File.WriteAllText(GamesPath, ser.Serialize(snapshot));
                }
            }
            catch { }
        }

        public static string TitleOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            lock (Sync)
            {
                GameMeta m;
                if (titles.TryGetValue(id, out m) && !string.IsNullOrEmpty(m.name)) return m.name;
                string n;
                if (appDbTitles.TryGetValue(id, out n)) return n;
            }
            return id;
        }

        public static GameMeta MetaOf(string id)
        {
            lock (Sync)
            {
                GameMeta m;
                return titles.TryGetValue(id, out m) ? m : null;
            }
        }

        public static string IconUrl(string id)
        {
            string f = Covers.CachedIconPath(id);
            long v = File.Exists(f) ? File.GetLastWriteTimeUtc(f).Ticks : 0;
            return "/api/game/icon?id=" + id + "&v=" + v;
        }

        /// <summary>Returns the current snapshot, kicking off a background refresh if stale.
        /// Blocks only when there is nothing cached at all.</summary>
        public static LibrarySnapshot Get(bool force)
        {
            LibrarySnapshot s = snapshot;
            if (s == null)
            {
                // Nothing cached yet: start the refresh and wait only for the first
                // partial list (published right after the folder listing).
                RefreshAsync();
                DateTime until = DateTime.UtcNow.AddSeconds(45);
                while (snapshot == null && refreshing && DateTime.UtcNow < until) Thread.Sleep(100);
                return snapshot ?? new LibrarySnapshot { games = new List<GameItem>(), updated = DateTime.MinValue };
            }
            if (force || (DateTime.UtcNow - s.updated).TotalSeconds > 90) RefreshAsync();
            return s;
        }

        public static void RefreshAsync()
        {
            if (refreshing) return;
            refreshing = true; // set early so the HTTP response already says "refreshing"
            ThreadPool.QueueUserWorkItem(_ => Refresh());
        }

        public static void Refresh()
        {
            if (!Monitor.TryEnter(RefreshLock)) { while (refreshing) Thread.Sleep(100); return; }
            refreshing = true;
            bool wasBackground = Ftp.BackgroundThread;
            Ftp.BackgroundThread = true;
            try
            {
                DoRefresh();
                LastError = "";
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.WriteLine("[!] Falha ao atualizar biblioteca: " + ex.Message);
            }
            finally
            {
                Ftp.BackgroundThread = wasBackground;
                refreshing = false;
                Monitor.Exit(RefreshLock);
            }
        }

        static readonly Regex TitleIdRx = new Regex(@"^[A-Z]{4}\d{5}$", RegexOptions.Compiled);

        static void DoRefresh()
        {
            var appFolders = Ftp.List("/user/appmeta/");
            var ids = new List<KeyValuePair<string, string>>();
            var idSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in appFolders)
            {
                string id = f.name.ToUpperInvariant();
                if (!f.isDir || !TitleIdRx.IsMatch(id) || id.StartsWith("NPXS")) continue;
                ids.Add(new KeyValuePair<string, string>(id, f.date));
                idSet.Add(id);
            }

            // Cheats (one listing)
            var cheatIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in Ftp.TryList("/data/GoldHEN/cheats/json/"))
            {
                var m = Regex.Match(f.name, @"^([A-Z]{4}\d{5})_", RegexOptions.IgnoreCase);
                if (m.Success) cheatIds.Add(m.Groups[1].Value);
            }

            // Show the list right away (cached names/playtime), then fill in the rest
            Publish(ids, cheatIds, false);

            // Names: param.sfo for titles we haven't seen yet (read once, cached forever)
            bool needAppDb = false;
            int fresh = 0;
            foreach (var kv in ids)
            {
                bool known;
                lock (Sync) known = titles.ContainsKey(kv.Key);
                if (known) continue;
                var meta = ReadSfoMeta(kv.Key);
                if (meta == null)
                {
                    needAppDb = true;
                    continue;
                }
                lock (Sync) titles[kv.Key] = meta;
                if (++fresh % 6 == 0) Publish(ids, cheatIds, false);
            }
            if (needAppDb) RefreshAppDb();
            if (fresh > 0 || needAppDb) Publish(ids, cheatIds, false);

            // Playtime: list the stats folder once and only download .ini files that changed
            // (and only for games that are still installed)
            var statFiles = Ftp.TryList("/data/GoldHEN/stats/");
            string today = statFiles.Count > 0 ? MaxDate(statFiles) : "";
            foreach (var f in statFiles)
            {
                if (f.isDir || !f.name.EndsWith(".ini", StringComparison.OrdinalIgnoreCase)) continue;
                string id = Path.GetFileNameWithoutExtension(f.name).ToUpperInvariant();
                if (!idSet.Contains(id)) continue;
                string sig = f.size + "|" + f.date;
                StatEntry cur;
                lock (Sync) stats.TryGetValue(id, out cur);
                // The listing only has day resolution, so files touched "today" are always re-read
                if (cur != null && cur.sig == sig && f.date != today) continue;
                string ini = Ftp.TryDownloadText("/data/GoldHEN/stats/" + f.name);
                if (ini == null) continue;
                var e = new StatEntry { sig = sig };
                var mt = Regex.Match(ini, @"Time_Played\s*=\s*(\d+)");
                if (mt.Success) e.minutes = int.Parse(mt.Groups[1].Value);
                var ms = Regex.Match(ini, @"Game_Starts\s*=\s*(\d+)");
                if (ms.Success) e.starts = int.Parse(ms.Groups[1].Value);
                lock (Sync) stats[id] = e;
            }

            Publish(ids, cheatIds, true);
        }

        static void Publish(List<KeyValuePair<string, string>> ids, HashSet<string> cheatIds, bool final)
        {
            var games = new List<GameItem>();
            foreach (var kv in ids)
            {
                string id = kv.Key;
                StatEntry st;
                GameMeta meta;
                lock (Sync)
                {
                    stats.TryGetValue(id, out st);
                    titles.TryGetValue(id, out meta);
                }
                games.Add(new GameItem
                {
                    id = id,
                    name = TitleOf(id),
                    version = meta != null ? meta.version : "",
                    category = meta != null ? meta.category : "",
                    timePlayedMinutes = st != null ? st.minutes : 0,
                    gameStarts = st != null ? st.starts : 0,
                    hasCheats = cheatIds.Contains(id),
                    customCover = Covers.HasOriginalBackup(id),
                    iconUrl = IconUrl(id),
                    installedDate = kv.Value
                });
            }

            games.Sort((a, b) =>
            {
                int c = b.timePlayedMinutes.CompareTo(a.timePlayedMinutes);
                if (c != 0) return c;
                return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
            });

            lock (Sync)
            {
                DateTime updated = final ? DateTime.UtcNow : (snapshot != null ? snapshot.updated : DateTime.MinValue);
                snapshot = new LibrarySnapshot { games = games, updated = updated };
            }
            if (final)
            {
                Save();
                try { History.Record(games); } catch { }
            }
        }

        public static bool IsInstalled(string id)
        {
            lock (Sync) return snapshot != null && snapshot.games != null && snapshot.games.Exists(g => string.Equals(g.id, id, StringComparison.OrdinalIgnoreCase));
        }

        public static List<GameItem> Games()
        {
            lock (Sync) return snapshot != null && snapshot.games != null ? new List<GameItem>(snapshot.games) : new List<GameItem>();
        }

        static string MaxDate(List<FtpItem> items)
        {
            DateTime best = DateTime.MinValue;
            string bestStr = "";
            foreach (var i in items)
            {
                var d = Program.ParseFtpDate(i.date);
                if (d > best) { best = d; bestStr = i.date; }
            }
            return bestStr;
        }

        static GameMeta ReadSfoMeta(string id)
        {
            byte[] sfo = Ftp.TryDownload("/system_data/priv/appmeta/" + id + "/param.sfo");
            if (sfo == null) return null;
            var d = ParseSfo(sfo);
            if (d == null) return null;
            string name;
            if (!d.TryGetValue("TITLE", out name) || string.IsNullOrEmpty(name)) return null;
            string ver, cid, cat;
            d.TryGetValue("APP_VER", out ver);
            d.TryGetValue("CONTENT_ID", out cid);
            d.TryGetValue("CATEGORY", out cat);
            return new GameMeta { id = id, name = name.Trim(), version = ver ?? "", contentId = cid ?? "", category = cat ?? "", source = "param.sfo" };
        }

        /// <summary>Minimal PARAM.SFO parser (string/int entries).</summary>
        public static Dictionary<string, string> ParseSfo(byte[] b)
        {
            try
            {
                if (b.Length < 20 || b[0] != 0 || b[1] != 'P' || b[2] != 'S' || b[3] != 'F') return null;
                int keyTable = BitConverter.ToInt32(b, 8);
                int dataTable = BitConverter.ToInt32(b, 12);
                int count = BitConverter.ToInt32(b, 16);
                var result = new Dictionary<string, string>();
                for (int i = 0; i < count; i++)
                {
                    int e = 20 + i * 16;
                    int keyOff = BitConverter.ToUInt16(b, e);
                    int fmt = BitConverter.ToUInt16(b, e + 2);
                    int len = BitConverter.ToInt32(b, e + 4);
                    int dataOff = BitConverter.ToInt32(b, e + 12);
                    int ks = keyTable + keyOff, ke = ks;
                    while (ke < b.Length && b[ke] != 0) ke++;
                    string key = Encoding.ASCII.GetString(b, ks, ke - ks);
                    int ds = dataTable + dataOff;
                    string val;
                    if (fmt == 0x0404) val = BitConverter.ToUInt32(b, ds).ToString();
                    else val = Encoding.UTF8.GetString(b, ds, Math.Max(0, len)).TrimEnd('\0');
                    result[key] = val;
                }
                return result;
            }
            catch { return null; }
        }

        /// <summary>Re-downloads app.db only when its size/date changed (fallback for titles).</summary>
        public static void RefreshAppDb()
        {
            try
            {
                long remoteSize = Ftp.Run(c => c.Size("/system_data/priv/mms/app.db"));
                if (File.Exists(AppDbPath) && new FileInfo(AppDbPath).Length == remoteSize && appDbTitles.Count > 0) return;
                Ftp.DownloadToFile("/system_data/priv/mms/app.db", AppDbPath);
                ParseAppDb(File.ReadAllBytes(AppDbPath));
            }
            catch { }
        }

        static void ParseAppDb(byte[] bytes)
        {
            try
            {
                string str = Encoding.UTF8.GetString(bytes);
                var matches = Regex.Matches(str, @"(?:[A-Z]{2}\d{4}-[A-Z0-9]{9}_\d{2}-[A-Z0-9]{16})([^\x00-\x08]+)/user/appmeta/([A-Za-z0-9_]{9})");
                lock (Sync)
                {
                    foreach (Match m in matches)
                    {
                        string id = m.Groups[2].Value.ToUpperInvariant();
                        string name = m.Groups[1].Value.Trim();
                        if (name.Length > 1 && !appDbTitles.ContainsKey(id)) appDbTitles[id] = name;
                    }
                }
            }
            catch { }
        }

        /// <summary>Forget cached title metadata (used by "deep refresh").</summary>
        public static void ClearTitleCache()
        {
            lock (Sync) titles.Clear();
        }

        public static void MarkCoverChanged(string id)
        {
            var s = snapshot;
            if (s == null) return;
            foreach (var g in s.games)
            {
                if (g.id == id)
                {
                    g.iconUrl = IconUrl(id);
                    g.customCover = Covers.HasOriginalBackup(id);
                }
            }
            Save();
        }
    }

    /// <summary>Dashboard summary (users, cheats, pkgs). Expensive, so cached ~30s.</summary>
    public static class Summary
    {
        static object cached;
        static DateTime cachedAt = DateTime.MinValue;
        static volatile bool busy;
        static readonly Dictionary<string, string> userNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public static string ActiveUserName = "";

        public static object Get(bool force)
        {
            if (cached == null || force)
            {
                Compute();
            }
            else if ((DateTime.UtcNow - cachedAt).TotalSeconds > 30 && !busy)
            {
                busy = true;
                ThreadPool.QueueUserWorkItem(_ => Compute());
            }
            return cached;
        }

        public static void Invalidate() { cachedAt = DateTime.MinValue; }

        static void Compute()
        {
            busy = true;
            bool wasBackground = Ftp.BackgroundThread;
            // Only the very first summary (nothing to show yet) runs with normal priority
            Ftp.BackgroundThread = cached != null;
            try
            {
                var users = new List<object>();
                string username = "PlayStation 4", userId = "";
                var homes = Ftp.TryList("/user/home/");
                var list = new List<PS4User>();
                foreach (var uf in homes)
                {
                    if (!uf.isDir || uf.name.Length < 4) continue;
                    string n;
                    lock (userNames)
                    {
                        userNames.TryGetValue(uf.name, out n);
                    }
                    if (n == null)
                    {
                        string dat = Ftp.TryDownloadText("/user/home/" + uf.name + "/username.dat");
                        n = dat == null ? "" : dat.Trim('\0', ' ', '\r', '\n');
                        lock (userNames) userNames[uf.name] = n;
                    }
                    list.Add(new PS4User { id = uf.name, name = string.IsNullOrEmpty(n) ? uf.name : n, date = uf.date, lastModified = Program.ParseFtpDate(uf.date) });
                }
                if (list.Count > 0)
                {
                    list.Sort((a, b) => b.lastModified.CompareTo(a.lastModified));
                    PS4User chosen = null;
                    string pref = Program.Settings.active_user;
                    if (!string.IsNullOrEmpty(pref))
                        chosen = list.Find(u => u.name.Equals(pref, StringComparison.OrdinalIgnoreCase) || u.id.Equals(pref, StringComparison.OrdinalIgnoreCase));
                    if (chosen == null) chosen = list[0];
                    username = chosen.name;
                    userId = chosen.id;
                    foreach (var u in list) users.Add(new { id = u.id, name = u.name, date = u.date, isCurrent = u.id == chosen.id });
                }
                ActiveUserName = username;

                int cheatCount = 0;
                foreach (var f in Ftp.TryList("/data/GoldHEN/cheats/json/"))
                    if (!f.isDir && f.name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) cheatCount++;

                int pkgCount = 0;
                long pkgSize = 0;
                foreach (var p in Ftp.TryList("/data/pkg/"))
                {
                    if (!p.isDir && p.name.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase)) { pkgCount++; pkgSize += p.size; }
                }

                cached = new
                {
                    username = username,
                    userId = userId,
                    users = users,
                    cheatCount = cheatCount,
                    pkgCount = pkgCount,
                    totalPkgSize = pkgSize
                };
                cachedAt = DateTime.UtcNow;
            }
            catch { }
            finally
            {
                Ftp.BackgroundThread = wasBackground;
                busy = false;
            }
        }
    }

    public class PS4User
    {
        public string id { get; set; }
        public string name { get; set; }
        public string date { get; set; }
        public DateTime lastModified { get; set; }
    }
}
