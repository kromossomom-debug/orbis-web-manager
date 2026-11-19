using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace OrbisWebManager
{
    /// <summary>Reads the unencrypted parts of a PS4 .pkg (header, param.sfo, icon0.png).</summary>
    public class PkgInfo
    {
        public string contentId = "", titleId = "", title = "", version = "", category = "", sysVer = "";
        public long size;
        public bool hasIcon;
        public string kind = "";   // Jogo, Atualização, DLC, App
        public string error;
    }

    public static class Pkg
    {
        const uint EntrySfo = 0x1000, EntryIcon = 0x1200;
        const int HeadBytes = 0x10000;   // header + entry table (the table sits well inside 64 KB)

        static uint BE(byte[] b, int p) { return (uint)((b[p] << 24) | (b[p + 1] << 16) | (b[p + 2] << 8) | b[p + 3]); }

        public static bool IsPkg(byte[] head) { return head != null && head.Length >= 0x80 && head[0] == 0x7F && head[1] == 0x43 && head[2] == 0x4E && head[3] == 0x54; }

        /// <summary>Finds an entry (offset,size) in the entry table; null when absent or outside head.</summary>
        static long[] FindEntry(byte[] head, uint id)
        {
            uint count = BE(head, 0x10);
            uint table = BE(head, 0x18);
            if (count > 4096) return null;
            for (uint i = 0; i < count; i++)
            {
                long p = table + i * 32;
                if (p + 32 > head.Length) return null;
                if (BE(head, (int)p) == id) return new long[] { BE(head, (int)p + 16), BE(head, (int)p + 20) };
            }
            return null;
        }

        /// <summary>Parses using a reader that returns bytes for (offset, count).</summary>
        public static PkgInfo Read(Func<long, int, byte[]> range, long fileSize, bool wantIcon, out byte[] icon)
        {
            icon = null;
            var info = new PkgInfo { size = fileSize };
            byte[] head = range(0, HeadBytes);
            if (!IsPkg(head)) { info.error = "Não é um PKG de PS4."; return info; }
            info.contentId = Encoding.ASCII.GetString(head, 0x40, 36).TrimEnd('\0');
            if (info.contentId.Length >= 16) info.titleId = info.contentId.Substring(7, 9);

            var sfo = FindEntry(head, EntrySfo);
            if (sfo != null && sfo[1] > 0 && sfo[1] < 1024 * 1024)
            {
                byte[] b = sfo[0] + sfo[1] <= head.Length ? Slice(head, sfo[0], (int)sfo[1]) : range(sfo[0], (int)sfo[1]);
                var d = Library.ParseSfo(b);
                if (d != null)
                {
                    string v;
                    if (d.TryGetValue("TITLE", out v)) info.title = v.Trim();
                    if (d.TryGetValue("APP_VER", out v)) info.version = v;
                    if (d.TryGetValue("CATEGORY", out v)) info.category = v;
                    if (d.TryGetValue("SYSTEM_VER", out v)) info.sysVer = v;
                    if (string.IsNullOrEmpty(info.version) && d.TryGetValue("VERSION", out v)) info.version = v;
                }
            }
            string c = info.category ?? "";
            bool official = info.titleId.StartsWith("CUSA") || info.titleId.StartsWith("PLAS") || info.titleId.StartsWith("PCAS") || info.titleId.StartsWith("PCJS") || info.titleId.StartsWith("PLJS");
            info.kind = c == "gp" ? "Atualização" : c == "ac" ? "DLC" : c.StartsWith("gd") ? (official ? "Jogo" : "App") : c.Length > 0 ? "App" : "PKG";

            var ic = FindEntry(head, EntryIcon);
            info.hasIcon = ic != null && ic[1] > 0;
            if (wantIcon && info.hasIcon && ic[1] < 4 * 1024 * 1024)
                icon = ic[0] + ic[1] <= head.Length ? Slice(head, ic[0], (int)ic[1]) : range(ic[0], (int)ic[1]);
            return info;
        }

        static byte[] Slice(byte[] b, long off, int len)
        {
            var r = new byte[len];
            Buffer.BlockCopy(b, (int)off, r, 0, len);
            return r;
        }

        // ------------------------------------------------------------ local files (PC folder)

        public static PkgInfo ReadLocal(string path, bool wantIcon, out byte[] icon)
        {
            using (var fs = File.OpenRead(path))
            {
                Func<long, int, byte[]> range = (off, count) =>
                {
                    fs.Seek(off, SeekOrigin.Begin);
                    var buf = new byte[count];
                    int got = 0, n;
                    while (got < count && (n = fs.Read(buf, got, count - got)) > 0) got += n;
                    if (got < count) Array.Resize(ref buf, got);
                    return buf;
                };
                return Read(range, fs.Length, wantIcon, out icon);
            }
        }

        static readonly Dictionary<string, PkgInfo> localCache = new Dictionary<string, PkgInfo>(StringComparer.OrdinalIgnoreCase);

        public static PkgInfo InfoLocalCached(string path)
        {
            var fi = new FileInfo(path);
            string key = path + "|" + fi.Length + "|" + fi.LastWriteTimeUtc.Ticks;
            lock (localCache)
            {
                PkgInfo p;
                if (localCache.TryGetValue(key, out p)) return p;
            }
            PkgInfo info;
            byte[] ic;
            try { info = ReadLocal(path, false, out ic); }
            catch (Exception ex) { info = new PkgInfo { size = fi.Length, error = ex.Message }; }
            lock (localCache) localCache[key] = info;
            return info;
        }

        // ------------------------------------------------------------ console files (/data/pkg)

        static Dictionary<string, PkgInfo> remoteCache;
        static string RemoteCachePath { get { return Path.Combine(Program.CacheDir, "pkginfo.json"); } }

        public static PkgInfo InfoRemoteCached(string remotePath, long size)
        {
            string key = remotePath + "|" + size;
            lock (typeof(Pkg))
            {
                if (remoteCache == null)
                {
                    remoteCache = new Dictionary<string, PkgInfo>();
                    try
                    {
                        if (File.Exists(RemoteCachePath))
                            remoteCache = new JavaScriptSerializer().Deserialize<Dictionary<string, PkgInfo>>(File.ReadAllText(RemoteCachePath)) ?? remoteCache;
                    }
                    catch { }
                }
                PkgInfo p;
                if (remoteCache.TryGetValue(key, out p)) return p;
            }
            PkgInfo info;
            byte[] ic;
            try
            {
                // The GoldHEN FTP doesn't reliably honour REST, so ranges near the start are read from byte 0.
                info = Read((off, count) =>
                {
                    if (off > 0 && off + count <= 16 * 1024 * 1024)
                    {
                        byte[] all = Ftp.Run(c => c.RetrieveRange(remotePath, 0, (int)(off + count)));
                        var part = new byte[Math.Max(0, Math.Min(count, all.Length - (int)off))];
                        if (part.Length > 0) Buffer.BlockCopy(all, (int)off, part, 0, part.Length);
                        return part;
                    }
                    return Ftp.Run(c => c.RetrieveRange(remotePath, off, count));
                }, size, false, out ic);
            }
            catch (Exception ex) { return new PkgInfo { size = size, error = ex.Message }; }
            lock (typeof(Pkg))
            {
                remoteCache[key] = info;
                try { File.WriteAllText(RemoteCachePath, new JavaScriptSerializer().Serialize(remoteCache)); } catch { }
            }
            return info;
        }

        /// <summary>Compares "01.05" style versions. Returns &lt;0, 0, &gt;0.</summary>
        public static int CompareVersion(string a, string b)
        {
            double x, y;
            double.TryParse((a ?? "").Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x);
            double.TryParse((b ?? "").Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y);
            return x.CompareTo(y);
        }

        /// <summary>What happens if this PKG is installed, given what's on the console.</summary>
        public static string Status(PkgInfo p, out string note)
        {
            note = "";
            if (p == null || !string.IsNullOrEmpty(p.error) || string.IsNullOrEmpty(p.titleId)) return "unknown";
            var meta = Library.MetaOf(p.titleId);
            bool installed = meta != null && Library.IsInstalled(p.titleId);
            if (p.category == "ac")
            {
                note = installed ? "Jogo instalado" : "O jogo dessa DLC não está instalado";
                return installed ? "dlc" : "dlc-nogame";
            }
            if (!installed) { note = "Não instalado"; return "new"; }
            string cur = meta.version ?? "";
            int cmp = CompareVersion(p.version, cur);
            if (cmp > 0) { note = "Atualiza v" + cur + " → v" + p.version; return "upgrade"; }
            if (cmp == 0) { note = "Já instalado (v" + cur + ")"; return "installed"; }
            note = "Mais antigo que o instalado (v" + cur + ")";
            return "older";
        }
    }
}
