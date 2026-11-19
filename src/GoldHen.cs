using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Xml;

namespace OrbisWebManager
{
    public static class GoldHen
    {
        public const string ConfigPath = "/data/GoldHEN/config.ini";
        public const string PluginsIni = "/data/GoldHEN/plugins.ini";
        public const string PluginsDir = "/data/GoldHEN/plugins/";
        public const string PatchesDir = "/data/GoldHEN/patches/xml/";
        public const string CheatsDir = "/data/GoldHEN/cheats/json/";

        static string BackupDir
        {
            get { string d = Path.Combine(Program.CacheDir, "goldhen-backups"); Directory.CreateDirectory(d); return d; }
        }

        static void KeepCopy(string name, string content)
        {
            try { File.WriteAllText(Path.Combine(BackupDir, name + "." + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak"), content); } catch { }
        }

        // ================================================================ config.ini

        public static object ReadConfig()
        {
            string text = Ftp.TryDownloadText(ConfigPath);
            if (text == null) return new { found = false, sections = new object[0] };
            var sections = new List<object>();
            string cur = "";
            List<object> keys = null;
            foreach (string raw in text.Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                var sm = Regex.Match(line, @"^\[(.+)\]$");
                if (sm.Success)
                {
                    cur = sm.Groups[1].Value.Trim();
                    keys = new List<object>();
                    sections.Add(new { name = cur, keys = keys });
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                if (keys == null) { keys = new List<object>(); sections.Add(new { name = "", keys = keys }); }
                string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                bool onOff = (v == "0" || v == "1") && Regex.IsMatch(k, "(Enabled|Visible|Enable)$", RegexOptions.IgnoreCase) && !(cur == "DebugSettings" && k == "Enabled");
                keys.Add(new { key = k, value = v, isBool = onOff });
            }
            return new { found = true, sections = sections, raw = text };
        }

        /// <summary>Applies {section,key,value} changes in place, keeping comments and order.</summary>
        public static int WriteConfig(object[] changes)
        {
            string text = Encoding.UTF8.GetString(Ftp.Download(ConfigPath));
            KeepCopy("config.ini", text);
            var lines = new List<string>(text.Replace("\r", "").Split('\n'));
            int applied = 0;
            foreach (var o in changes)
            {
                var c = o as Dictionary<string, object>;
                if (c == null) continue;
                string sec = Convert.ToString(c["section"]), key = Convert.ToString(c["key"]), val = Convert.ToString(c["value"]);
                if (key.Length == 0 || key.IndexOfAny(new[] { '=', '\n', '[' }) >= 0 || val.IndexOf('\n') >= 0) continue;
                string curSec = "";
                bool done = false;
                int lastLineOfSection = -1;
                for (int i = 0; i < lines.Count; i++)
                {
                    string t = lines[i].Trim();
                    var sm = Regex.Match(t, @"^\[(.+)\]$");
                    if (sm.Success) { curSec = sm.Groups[1].Value.Trim(); continue; }
                    if (!string.Equals(curSec, sec, StringComparison.OrdinalIgnoreCase)) continue;
                    lastLineOfSection = i;
                    int eq = t.IndexOf('=');
                    if (eq > 0 && !t.StartsWith(";") && string.Equals(t.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
                    {
                        string spacing = lines[i].Contains(" = ") ? " = " : "=";
                        lines[i] = key + spacing + val;
                        done = true;
                        break;
                    }
                }
                if (!done)
                {
                    if (lastLineOfSection >= 0) lines.Insert(lastLineOfSection + 1, key + " = " + val);
                    else { lines.Add("[" + sec + "]"); lines.Add(key + " = " + val); }
                }
                applied++;
            }
            Ftp.Upload(ConfigPath, Encoding.UTF8.GetBytes(string.Join("\r\n", lines)));
            return applied;
        }

        // ================================================================ plugins

        public static object ReadPlugins()
        {
            var available = new List<object>();
            foreach (var f in Ftp.TryList(PluginsDir))
                if (!f.isDir && f.name.EndsWith(".prx", StringComparison.OrdinalIgnoreCase))
                    available.Add(new { name = f.name, path = PluginsDir + f.name, size = f.size });
            string ini = Ftp.TryDownloadText(PluginsIni) ?? "";
            var sections = new List<object>();
            List<object> entries = null;
            foreach (string raw in ini.Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                var sm = Regex.Match(line, @"^\[(.+)\]$");
                if (sm.Success)
                {
                    entries = new List<object>();
                    string sec = sm.Groups[1].Value.Trim();
                    sections.Add(new { name = sec, title = Regex.IsMatch(sec, @"^[A-Z]{4}\d{5}$") ? Library.TitleOf(sec) : "", entries = entries });
                    continue;
                }
                if (entries == null) continue;
                bool enabled = !(line.StartsWith(";") || line.StartsWith("#"));
                string path = line.TrimStart(';', '#', ' ');
                if (path.EndsWith(".prx", StringComparison.OrdinalIgnoreCase)) entries.Add(new { path = path, enabled = enabled });
            }
            return new { available = available, sections = sections, iniFound = ini.Length > 0 };
        }

        /// <summary>sections: [{name, entries:[{path, enabled}]}]</summary>
        public static void WritePlugins(object[] sections)
        {
            string old = Ftp.TryDownloadText(PluginsIni) ?? "";
            KeepCopy("plugins.ini", old);
            var sb = new StringBuilder();
            foreach (var o in sections)
            {
                var s = o as Dictionary<string, object>;
                if (s == null) continue;
                string name = Convert.ToString(s["name"]).Trim();
                if (!Regex.IsMatch(name, @"^(default|[A-Z]{4}\d{5})$")) continue;
                sb.Append("[").Append(name).Append("]\r\n");
                var list = s["entries"] as object[];
                if (list != null)
                    foreach (var eo in list)
                    {
                        var e = eo as Dictionary<string, object>;
                        if (e == null) continue;
                        string p = Convert.ToString(e["path"]).Trim();
                        if (!p.StartsWith("/") || !p.EndsWith(".prx", StringComparison.OrdinalIgnoreCase) || p.IndexOf('\n') >= 0) continue;
                        bool en = e.ContainsKey("enabled") && Convert.ToBoolean(e["enabled"]);
                        sb.Append(en ? "" : ";").Append(p).Append("\r\n");
                    }
                sb.Append("\r\n");
            }
            Ftp.Upload(PluginsIni, Encoding.UTF8.GetBytes(sb.ToString()));
        }

        // ================================================================ game patches (GoldHEN_Patch_Repository)

        const string PatchZipUrl = "https://raw.githubusercontent.com/GoldHEN/GoldHEN_Patch_Repository/gh-pages/patch1.zip";
        static string PatchZipPath { get { return Path.Combine(Program.CacheDir, "patch1.zip"); } }
        // titleId -> xml entry name inside the zip
        static Dictionary<string, string> patchIndex;
        static DateTime patchIndexAt = DateTime.MinValue;

        static void EnsurePatchZip(bool force)
        {
            bool stale = !File.Exists(PatchZipPath) || (DateTime.Now - File.GetLastWriteTime(PatchZipPath)).TotalHours > 24;
            if (force || stale)
            {
                try
                {
                    byte[] z = Covers.HttpGetBytes(PatchZipUrl);
                    if (z != null && z.Length > 1000) { File.WriteAllBytes(PatchZipPath, z); patchIndex = null; }
                }
                catch { if (!File.Exists(PatchZipPath)) throw; }
            }
            if (patchIndex != null && patchIndexAt == File.GetLastWriteTime(PatchZipPath)) return;
            var idx = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var zip = ZipFile.OpenRead(PatchZipPath))
            {
                foreach (var e in zip.Entries)
                {
                    if (!e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) continue;
                    string xml;
                    using (var r = new StreamReader(e.Open(), Encoding.UTF8)) xml = r.ReadToEnd();
                    foreach (Match m in Regex.Matches(xml, @"<ID>\s*([A-Z]{4}\d{5})\s*</ID>"))
                        if (!idx.ContainsKey(m.Groups[1].Value)) idx[m.Groups[1].Value] = e.FullName;
                }
            }
            patchIndex = idx;
            patchIndexAt = File.GetLastWriteTime(PatchZipPath);
        }

        static string PatchXml(string entry)
        {
            using (var zip = ZipFile.OpenRead(PatchZipPath))
            {
                var e = zip.GetEntry(entry);
                if (e == null) return null;
                using (var r = new StreamReader(e.Open(), Encoding.UTF8)) return r.ReadToEnd();
            }
        }

        static List<object> DescribePatches(string xml, string appVer)
        {
            // The same patch usually exists once per game version: group by name.
            var order = new List<string>();
            var vers = new Dictionary<string, List<string>>();
            var notes = new Dictionary<string, string>();
            var authors = new Dictionary<string, string>();
            var match = new Dictionary<string, bool>();
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            foreach (XmlElement m in doc.GetElementsByTagName("Metadata"))
            {
                string name = m.GetAttribute("Name"), ver = m.GetAttribute("AppVer");
                if (!vers.ContainsKey(name)) { order.Add(name); vers[name] = new List<string>(); notes[name] = m.GetAttribute("Note"); authors[name] = m.GetAttribute("Author"); match[name] = false; }
                if (ver.Length > 0 && !vers[name].Contains(ver)) vers[name].Add(ver);
                if (string.IsNullOrEmpty(appVer) || ver.Length == 0 || ver == "mask" || ver == appVer) match[name] = true;
            }
            var list = new List<object>();
            foreach (var n in order)
            {
                vers[n].Sort(StringComparer.Ordinal);
                list.Add(new { name = n, note = notes[n], author = authors[n], appVer = string.Join(", ", vers[n].ToArray()), matches = match[n] });
            }
            return list;
        }

        public static object Patches(bool refresh)
        {
            lock (typeof(GoldHen)) EnsurePatchZip(refresh);
            var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in Ftp.TryList(PatchesDir))
                if (!f.isDir && f.name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) installed.Add(Path.GetFileNameWithoutExtension(f.name));
            var games = new List<object>();
            foreach (var g in Library.Games())
            {
                string entry;
                bool has = patchIndex.TryGetValue(g.id, out entry);
                if (!has && !installed.Contains(g.id)) continue;
                List<object> patches = new List<object>();
                if (has) { try { patches = DescribePatches(PatchXml(entry), g.version); } catch { } }
                games.Add(new { id = g.id, name = g.name, version = g.version, iconUrl = g.iconUrl, available = has, installed = installed.Contains(g.id), patches = patches });
            }
            return new { games = games, repoSize = patchIndex.Count, updated = File.GetLastWriteTime(PatchZipPath).ToString("dd/MM HH:mm") };
        }

        public static void InstallPatch(string id)
        {
            lock (typeof(GoldHen)) EnsurePatchZip(false);
            string entry;
            if (!patchIndex.TryGetValue(id, out entry)) throw new Exception("Não há patches para " + id + " no repositório.");
            string xml = PatchXml(entry);
            Ftp.Upload(PatchesDir + id + ".xml", Encoding.UTF8.GetBytes(xml));
        }

        public static void RemovePatch(string id)
        {
            Ftp.Run(c => c.Simple("DELE " + PatchesDir + id + ".xml", 250, 200));
        }

        // ================================================================ cheats (GoldHEN_Cheat_Repository)

        const string CheatRaw = "https://raw.githubusercontent.com/GoldHEN/GoldHEN_Cheat_Repository/main/";
        static string CheatIndexPath { get { return Path.Combine(Program.CacheDir, "cheats-index.txt"); } }

        public static object CheatRepo(bool refresh)
        {
            bool stale = !File.Exists(CheatIndexPath) || (DateTime.Now - File.GetLastWriteTime(CheatIndexPath)).TotalHours > 24;
            if (refresh || stale)
            {
                try
                {
                    byte[] b = Covers.HttpGetBytes(CheatRaw + "json.txt");
                    if (b != null && b.Length > 100) File.WriteAllBytes(CheatIndexPath, b);
                }
                catch { if (!File.Exists(CheatIndexPath)) throw; }
            }
            var byId = new Dictionary<string, List<object>>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in File.ReadAllLines(CheatIndexPath, Encoding.UTF8))
            {
                var m = Regex.Match(raw.Trim(), @"^(([A-Z]{4}\d{5})_(\d{2}\.\d{2})[^=]*\.json)=(.*)$");
                if (!m.Success) continue;
                List<object> l;
                if (!byId.TryGetValue(m.Groups[2].Value, out l)) byId[m.Groups[2].Value] = l = new List<object>();
                l.Add(new { file = m.Groups[1].Value, version = m.Groups[3].Value, title = m.Groups[4].Value.Trim() });
            }
            var have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in Ftp.TryList(CheatsDir)) if (!f.isDir) have.Add(f.name);
            var games = new List<object>();
            foreach (var g in Library.Games())
            {
                List<object> l;
                if (!byId.TryGetValue(g.id, out l)) continue;
                var files = new List<object>();
                foreach (var o in l)
                {
                    string file = (string)o.GetType().GetProperty("file").GetValue(o, null);
                    string ver = (string)o.GetType().GetProperty("version").GetValue(o, null);
                    files.Add(new { file = file, version = ver, installed = have.Contains(file), matches = ver == g.version });
                }
                games.Add(new { id = g.id, name = g.name, version = g.version, iconUrl = g.iconUrl, files = files });
            }
            return new { games = games, updated = File.GetLastWriteTime(CheatIndexPath).ToString("dd/MM HH:mm") };
        }

        public static void InstallCheat(string file)
        {
            if (!Regex.IsMatch(file, @"^[A-Z]{4}\d{5}_\d{2}\.\d{2}[A-Za-z0-9_.\-]*\.json$")) throw new Exception("Arquivo inválido.");
            byte[] b = Covers.HttpGetBytes(CheatRaw + "json/" + Uri.EscapeDataString(file));
            if (b == null || b.Length == 0) throw new Exception("Não foi possível baixar o cheat.");
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(Encoding.UTF8.GetString(b));
            Ftp.Upload(CheatsDir + file, b);
        }
    }
}
