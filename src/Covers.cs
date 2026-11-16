using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace OrbisWebManager
{
    /// <summary>
    /// Cover art: local icon cache, online search (Sony TMDB + SteamGridDB),
    /// conversion to the formats the PS4 actually uses, and backup/restore of originals.
    ///
    /// IMPORTANT: the PS4 home screen draws /user/appmeta/ID/icon0.dds (DXT1 512x512),
    /// not only icon0.png. The old version only overwrote icon0.png (sometimes with a
    /// JPEG renamed to .png), so the console often kept showing the old cover.
    /// Now we write a real PNG + a matching DXT1 .dds, and back up both originals.
    /// </summary>
    public static class Covers
    {
        public static string IconsDir { get { return Path.Combine(Program.CacheDir, "icons"); } }
        public static string OriginalsDir { get { return Path.Combine(Program.CacheDir, "originals"); } }

        static readonly Dictionary<string, DateTime> missingIcons = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        public static string CachedIconPath(string id)
        {
            return Path.Combine(IconsDir, SafeId(id) + ".png");
        }

        public static string SafeId(string id)
        {
            if (id == null) return "";
            return Regex.Replace(id.ToUpperInvariant(), @"[^A-Z0-9_]", "");
        }

        public static bool HasOriginalBackup(string id)
        {
            return File.Exists(Path.Combine(OriginalsDir, SafeId(id), "icon0.png"))
                || File.Exists(Path.Combine(OriginalsDir, SafeId(id), "pic1.png"));
        }

        // ---------------------------------------------------------------- icon cache

        /// <summary>Returns the local cached icon path, fetching from the PS4 (or Sony as fallback) if needed.
        /// Returns null if unavailable. Failures are remembered for 10 minutes so a broken icon
        /// doesn't cost an FTP round-trip on every page load.</summary>
        public static string GetIcon(string id)
        {
            id = SafeId(id);
            if (id.Length == 0) return null;
            string local = CachedIconPath(id);
            if (File.Exists(local)) return local;

            lock (missingIcons)
            {
                DateTime t;
                if (missingIcons.TryGetValue(id, out t) && (DateTime.UtcNow - t).TotalMinutes < 10) return null;
            }

            try
            {
                Ftp.DownloadToFile("/user/appmeta/" + id + "/icon0.png", local);
            }
            catch { }

            if (!File.Exists(local) && id.StartsWith("CUSA"))
            {
                try
                {
                    var info = SonyInfo(id);
                    if (info != null && !string.IsNullOrEmpty(info.icon)) File.WriteAllBytes(local, HttpGetBytes(info.icon));
                }
                catch { }
            }

            if (File.Exists(local)) return local;
            lock (missingIcons) missingIcons[id] = DateTime.UtcNow;
            return null;
        }

        public static void ForgetMissing()
        {
            lock (missingIcons) missingIcons.Clear();
        }

        // ---------------------------------------------------------------- Sony TMDB

        static readonly byte[] TmdbKey = {
            0xF5, 0xDE, 0x66, 0xD2, 0x68, 0x0E, 0x25, 0x5B, 0x2D, 0xF7, 0x9E, 0x74, 0xF8, 0x90, 0xEB, 0xF3,
            0x49, 0x26, 0x2F, 0x61, 0x8B, 0xCA, 0xE2, 0xA9, 0xAC, 0xCD, 0xEE, 0x51, 0x56, 0xCE, 0x8D, 0xF2,
            0xCD, 0xF2, 0xD4, 0x8C, 0x71, 0x17, 0x3C, 0xDC, 0x25, 0x94, 0x46, 0x5B, 0x87, 0x40, 0x5D, 0x19,
            0x7C, 0xF1, 0xAE, 0xD3, 0xB7, 0xE9, 0x67, 0x1E, 0xEB, 0x56, 0xCA, 0x67, 0x53, 0xC2, 0xE6, 0xB0
        };

        public class SonyResult
        {
            public string icon;
            public string background;
            public string name;
        }

        static readonly Dictionary<string, SonyResult> sonyCache = new Dictionary<string, SonyResult>(StringComparer.OrdinalIgnoreCase);

        public static SonyResult SonyInfo(string titleId)
        {
            titleId = SafeId(titleId);
            lock (sonyCache)
            {
                SonyResult cached;
                if (sonyCache.TryGetValue(titleId, out cached)) return cached;
            }
            SonyResult r = null;
            try
            {
                string target = titleId + "_00";
                string sig;
                using (var h = new HMACSHA1(TmdbKey))
                {
                    sig = BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(target))).Replace("-", "");
                }
                string url = "https://tmdb.np.dl.playstation.net/tmdb2/" + target + "_" + sig + "/" + target + ".json";
                string json = HttpGetString(url, null);
                var d = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
                if (d != null)
                {
                    r = new SonyResult();
                    object o;
                    if (d.TryGetValue("icons", out o) && o is object[])
                    {
                        // prefer the biggest icon
                        foreach (var it in (object[])o)
                        {
                            var ic = it as Dictionary<string, object>;
                            if (ic != null && ic.ContainsKey("icon")) r.icon = ic["icon"].ToString();
                        }
                    }
                    if (d.TryGetValue("icon", out o) && o != null && string.IsNullOrEmpty(r.icon)) r.icon = o.ToString();
                    if (d.TryGetValue("backgroundImage", out o) && o != null) r.background = o.ToString();
                    if (d.TryGetValue("names", out o) && o is object[] && ((object[])o).Length > 0)
                    {
                        var n0 = ((object[])o)[0] as Dictionary<string, object>;
                        if (n0 != null && n0.ContainsKey("name")) r.name = n0["name"].ToString();
                    }
                }
            }
            catch { r = null; }
            lock (sonyCache) sonyCache[titleId] = r;
            return r;
        }

        // ---------------------------------------------------------------- SteamGridDB

        static string SgdbKey { get { return (Program.Settings.steamgriddb_api_key ?? "").Trim(); } }
        public static bool HasSgdbKey { get { return SgdbKey.Length > 0; } }

        static Dictionary<string, object> SgdbGet(string path)
        {
            string json = HttpGetString("https://www.steamgriddb.com/api/v2/" + path, "Bearer " + SgdbKey);
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(json) as Dictionary<string, object>;
        }

        public static List<object> SgdbSearch(string term)
        {
            var list = new List<object>();
            term = Regex.Replace(term ?? "", @"[®™©]", "").Trim();
            if (term.Length == 0) return list;
            var d = SgdbGet("search/autocomplete/" + Uri.EscapeDataString(term));
            object data;
            if (d != null && d.TryGetValue("data", out data) && data is object[])
            {
                foreach (var it in (object[])data)
                {
                    var g = it as Dictionary<string, object>;
                    if (g == null) continue;
                    string year = "";
                    object rd;
                    if (g.TryGetValue("release_date", out rd) && rd != null)
                    {
                        long secs;
                        if (long.TryParse(rd.ToString(), out secs) && secs > 0) year = new DateTime(1970, 1, 1).AddSeconds(secs).Year.ToString();
                    }
                    list.Add(new { id = g["id"], name = g.ContainsKey("name") ? g["name"] : "", year = year });
                    if (list.Count >= 12) break;
                }
            }
            return list;
        }

        /// <summary>kind: "square" (best for the PS4 icon), "all", "heroes" (backgrounds)</summary>
        public static List<object> SgdbArt(int gameId, string kind)
        {
            var result = new List<object>();
            string path;
            if (kind == "heroes") path = "heroes/game/" + gameId;
            else if (kind == "square") path = "grids/game/" + gameId + "?dimensions=512x512,1024x1024";
            else path = "grids/game/" + gameId;

            var d = SgdbGet(path);
            object data;
            if (d == null || !d.TryGetValue("data", out data) || !(data is object[])) return result;
            foreach (var it in (object[])data)
            {
                var g = it as Dictionary<string, object>;
                if (g == null) continue;
                string author = "";
                object a;
                if (g.TryGetValue("author", out a) && a is Dictionary<string, object>)
                {
                    var ad = (Dictionary<string, object>)a;
                    if (ad.ContainsKey("name") && ad["name"] != null) author = ad["name"].ToString();
                }
                result.Add(new
                {
                    url = g.ContainsKey("url") ? g["url"] : "",
                    thumb = g.ContainsKey("thumb") ? g["thumb"] : "",
                    width = g.ContainsKey("width") ? g["width"] : 0,
                    height = g.ContainsKey("height") ? g["height"] : 0,
                    style = g.ContainsKey("style") ? g["style"] : "",
                    author = author
                });
                if (result.Count >= 60) break;
            }
            return result;
        }

        // ---------------------------------------------------------------- apply / restore

        /// <summary>target: "icon" (icon0, 512x512) or "background" (pic1, 1920x1080).
        /// fit: "cover" (fill & crop) or "contain" (whole image over a blurred backdrop).</summary>
        public static void Apply(string id, byte[] source, string target, string fit)
        {
            id = SafeId(id);
            bool bg = target == "background";
            string baseName = bg ? "pic1" : "icon0";
            int w = bg ? 1920 : 512, h = bg ? 1080 : 512;
            string remoteDir = "/user/appmeta/" + id + "/";

            byte[] png, dds;
            using (var src = LoadImage(source))
            using (var bmp = Render(src, w, h, fit))
            {
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    png = ms.ToArray();
                }
                dds = Dxt1.Encode(bmp);
            }

            // Which files exist on the console for this title?
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in Ftp.List(remoteDir)) existing.Add(f.name);
            if (existing.Count == 0) throw new Exception("Pasta " + remoteDir + " não encontrada no PS4.");

            // Back up originals once (never overwrite an existing backup)
            string backupDir = Path.Combine(OriginalsDir, id);
            Directory.CreateDirectory(backupDir);
            foreach (string ext in new[] { ".png", ".dds" })
            {
                string bk = Path.Combine(backupDir, baseName + ext);
                if (!File.Exists(bk) && existing.Contains(baseName + ext))
                {
                    Ftp.DownloadToFile(remoteDir + baseName + ext, bk);
                }
            }

            Ftp.Upload(remoteDir + baseName + ".png", png);
            if (existing.Contains(baseName + ".dds")) Ftp.Upload(remoteDir + baseName + ".dds", dds);

            if (!bg)
            {
                File.WriteAllBytes(CachedIconPath(id), png);
                lock (missingIcons) missingIcons.Remove(id);
            }
        }

        public static bool Restore(string id, string target)
        {
            id = SafeId(id);
            string baseName = target == "background" ? "pic1" : "icon0";
            string backupDir = Path.Combine(OriginalsDir, id);
            string remoteDir = "/user/appmeta/" + id + "/";
            bool any = false;
            foreach (string ext in new[] { ".png", ".dds" })
            {
                string bk = Path.Combine(backupDir, baseName + ext);
                if (!File.Exists(bk)) continue;
                Ftp.Upload(remoteDir + baseName + ext, File.ReadAllBytes(bk));
                any = true;
            }
            if (!any) return false;
            if (baseName == "icon0")
            {
                string bkPng = Path.Combine(backupDir, "icon0.png");
                if (File.Exists(bkPng)) File.Copy(bkPng, CachedIconPath(id), true);
            }
            foreach (string ext in new[] { ".png", ".dds" })
            {
                string bk = Path.Combine(backupDir, baseName + ext);
                if (File.Exists(bk)) File.Delete(bk);
            }
            try { if (Directory.GetFiles(backupDir).Length == 0) Directory.Delete(backupDir); } catch { }
            return true;
        }

        // ---------------------------------------------------------------- imaging

        public static Bitmap LoadImage(byte[] data)
        {
            if (data == null || data.Length < 8) throw new Exception("Imagem vazia.");
            // WebP is common on image CDNs and is not supported by System.Drawing
            if (data.Length > 12 && data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P')
                throw new Exception("Formato WebP não suportado. Use PNG ou JPG.");
            using (var ms = new MemoryStream(data))
            using (var img = Image.FromStream(ms))
            {
                return new Bitmap(img);
            }
        }

        public static Bitmap Render(Image src, int w, int h, string fit)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;

                Rectangle coverRect = FitRect(src.Width, src.Height, w, h, true);
                if (fit == "contain")
                {
                    // Cheap blur: draw a tiny version scaled up, then darken it
                    using (var tiny = new Bitmap(24, Math.Max(1, 24 * h / w)))
                    {
                        using (var tg = Graphics.FromImage(tiny))
                        {
                            tg.InterpolationMode = InterpolationMode.HighQualityBilinear;
                            Rectangle tr = FitRect(src.Width, src.Height, tiny.Width, tiny.Height, true);
                            tg.DrawImage(src, tr);
                        }
                        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        g.DrawImage(tiny, new Rectangle(-w / 20, -h / 20, w + w / 10, h + h / 10));
                    }
                    using (var shade = new SolidBrush(Color.FromArgb(110, 0, 0, 0))) g.FillRectangle(shade, 0, 0, w, h);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(src, FitRect(src.Width, src.Height, w, h, false));
                }
                else
                {
                    g.DrawImage(src, coverRect);
                }
            }
            return bmp;
        }

        static Rectangle FitRect(int sw, int sh, int w, int h, bool cover)
        {
            double scale = cover ? Math.Max((double)w / sw, (double)h / sh) : Math.Min((double)w / sw, (double)h / sh);
            int nw = (int)Math.Round(sw * scale), nh = (int)Math.Round(sh * scale);
            return new Rectangle((w - nw) / 2, (h - nh) / 2, nw, nh);
        }

        public static byte[] Thumbnail(byte[] jpg, int maxW)
        {
            using (var src = LoadImage(jpg))
            {
                int w = Math.Min(maxW, src.Width);
                int h = Math.Max(1, src.Height * w / src.Width);
                using (var bmp = new Bitmap(w, h))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        g.DrawImage(src, 0, 0, w, h);
                    }
                    var enc = Array.Find(ImageCodecInfo.GetImageEncoders(), c => c.MimeType == "image/jpeg");
                    var ep = new EncoderParameters(1);
                    ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 78L);
                    using (var ms = new MemoryStream())
                    {
                        bmp.Save(ms, enc, ep);
                        return ms.ToArray();
                    }
                }
            }
        }

        public static string SniffImageType(byte[] b)
        {
            if (b.Length > 3 && b[0] == 0x89 && b[1] == 'P') return "image/png";
            if (b.Length > 2 && b[0] == 0xFF && b[1] == 0xD8) return "image/jpeg";
            if (b.Length > 11 && b[8] == 'W' && b[9] == 'E') return "image/webp";
            if (b.Length > 3 && b[0] == 'G' && b[1] == 'I') return "image/gif";
            return "application/octet-stream";
        }

        // ---------------------------------------------------------------- HTTP helpers

        public static string HttpGetString(string url, string auth)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "OrbisWebManager/2.0";
            req.Timeout = 10000;
            req.ReadWriteTimeout = 15000;
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            if (auth != null) req.Headers.Add("Authorization", auth);
            try
            {
                using (var res = (HttpWebResponse)req.GetResponse())
                using (var r = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
                {
                    return r.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                var hr = ex.Response as HttpWebResponse;
                if (hr != null && (int)hr.StatusCode == 401) throw new Exception("Chave de API do SteamGridDB inválida (401).");
                throw;
            }
        }

        public static byte[] HttpGetBytes(string url)
        {
            Uri u;
            if (!Uri.TryCreate(url, UriKind.Absolute, out u) || (u.Scheme != "http" && u.Scheme != "https"))
                throw new Exception("URL de imagem inválida.");
            var req = (HttpWebRequest)WebRequest.Create(u);
            req.UserAgent = "OrbisWebManager/2.0";
            req.Timeout = 15000;
            req.ReadWriteTimeout = 20000;
            using (var res = (HttpWebResponse)req.GetResponse())
            using (var s = res.GetResponseStream())
            using (var ms = new MemoryStream())
            {
                byte[] buf = new byte[65536];
                int n;
                while ((n = s.Read(buf, 0, buf.Length)) > 0)
                {
                    ms.Write(buf, 0, n);
                    if (ms.Length > 40 * 1024 * 1024) throw new Exception("Imagem muito grande.");
                }
                return ms.ToArray();
            }
        }
    }

    /// <summary>Minimal DXT1 (BC1) encoder producing the same DDS layout the PS4 stores in appmeta.</summary>
    public static class Dxt1
    {
        public static byte[] Encode(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            int bw = (w + 3) / 4, bh = (h + 3) / 4;
            int linear = bw * bh * 8;
            byte[] outb = new byte[128 + linear];
            WriteHeader(outb, w, h, linear);

            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int stride = data.Stride;
            byte[] px = new byte[stride * h];
            Marshal.Copy(data.Scan0, px, 0, px.Length);
            bmp.UnlockBits(data);

            int[] r = new int[16], g = new int[16], b = new int[16];
            int o = 128;
            for (int by = 0; by < bh; by++)
            {
                for (int bx = 0; bx < bw; bx++)
                {
                    for (int i = 0; i < 16; i++)
                    {
                        int x = Math.Min(w - 1, bx * 4 + (i & 3));
                        int y = Math.Min(h - 1, by * 4 + (i >> 2));
                        int p = y * stride + x * 4; // BGRA
                        b[i] = px[p]; g[i] = px[p + 1]; r[i] = px[p + 2];
                    }
                    EncodeBlock(r, g, b, outb, o);
                    o += 8;
                }
            }
            return outb;
        }

        static void WriteHeader(byte[] o, int w, int h, int linear)
        {
            // Mirrors the header found in original PS4 icon0.dds files:
            // flags 0xA1007, mipMapCount 1, DXT1, caps 0x401008
            Put(o, 0, 0x20534444); // "DDS "
            Put(o, 4, 124);
            Put(o, 8, 0xA1007);
            Put(o, 12, h);
            Put(o, 16, w);
            Put(o, 20, linear);
            Put(o, 24, 0);
            Put(o, 28, 1);
            Put(o, 76, 32);
            Put(o, 80, 0x4);
            Put(o, 84, 0x31545844); // "DXT1"
            Put(o, 108, 0x401008);
        }

        static void Put(byte[] o, int at, int v)
        {
            o[at] = (byte)v; o[at + 1] = (byte)(v >> 8); o[at + 2] = (byte)(v >> 16); o[at + 3] = (byte)(v >> 24);
        }

        static int To565(int r, int g, int b)
        {
            return ((r * 31 + 127) / 255 << 11) | ((g * 63 + 127) / 255 << 5) | ((b * 31 + 127) / 255);
        }

        static void From565(int c, out int r, out int g, out int b)
        {
            r = ((c >> 11) & 31) * 255 / 31;
            g = ((c >> 5) & 63) * 255 / 63;
            b = (c & 31) * 255 / 31;
        }

        static void EncodeBlock(int[] r, int[] g, int[] b, byte[] o, int at)
        {
            // Pick endpoints along the axis with the largest spread (cheap PCA approximation)
            double mr = 0, mg = 0, mb = 0;
            for (int i = 0; i < 16; i++) { mr += r[i]; mg += g[i]; mb += b[i]; }
            mr /= 16; mg /= 16; mb /= 16;
            double crr = 0, cgg = 0, cbb = 0, crg = 0, crb = 0, cgb = 0;
            for (int i = 0; i < 16; i++)
            {
                double dr = r[i] - mr, dg = g[i] - mg, db = b[i] - mb;
                crr += dr * dr; cgg += dg * dg; cbb += db * db; crg += dr * dg; crb += dr * db; cgb += dg * db;
            }
            // power iteration for principal axis
            double ar = 1, ag = 1, ab = 1;
            for (int k = 0; k < 6; k++)
            {
                double nr = crr * ar + crg * ag + crb * ab;
                double ng = crg * ar + cgg * ag + cgb * ab;
                double nb = crb * ar + cgb * ag + cbb * ab;
                double len = Math.Sqrt(nr * nr + ng * ng + nb * nb);
                if (len < 1e-9) { ar = 0.577; ag = 0.577; ab = 0.577; break; }
                ar = nr / len; ag = ng / len; ab = nb / len;
            }
            double minT = double.MaxValue, maxT = double.MinValue;
            for (int i = 0; i < 16; i++)
            {
                double t = (r[i] - mr) * ar + (g[i] - mg) * ag + (b[i] - mb) * ab;
                if (t < minT) minT = t;
                if (t > maxT) maxT = t;
            }
            int c0 = To565(Clamp(mr + ar * maxT), Clamp(mg + ag * maxT), Clamp(mb + ab * maxT));
            int c1 = To565(Clamp(mr + ar * minT), Clamp(mg + ag * minT), Clamp(mb + ab * minT));
            if (c0 < c1) { int t = c0; c0 = c1; c1 = t; }

            uint indices = 0;
            if (c0 != c1)
            {
                int[] pr = new int[4], pg = new int[4], pb = new int[4];
                From565(c0, out pr[0], out pg[0], out pb[0]);
                From565(c1, out pr[1], out pg[1], out pb[1]);
                pr[2] = (2 * pr[0] + pr[1]) / 3; pg[2] = (2 * pg[0] + pg[1]) / 3; pb[2] = (2 * pb[0] + pb[1]) / 3;
                pr[3] = (pr[0] + 2 * pr[1]) / 3; pg[3] = (pg[0] + 2 * pg[1]) / 3; pb[3] = (pb[0] + 2 * pb[1]) / 3;
                for (int i = 0; i < 16; i++)
                {
                    int best = 0, bestD = int.MaxValue;
                    for (int k = 0; k < 4; k++)
                    {
                        int dr = r[i] - pr[k], dg = g[i] - pg[k], db = b[i] - pb[k];
                        int d = dr * dr * 3 + dg * dg * 4 + db * db * 2;
                        if (d < bestD) { bestD = d; best = k; }
                    }
                    indices |= (uint)best << (i * 2);
                }
            }
            o[at] = (byte)c0; o[at + 1] = (byte)(c0 >> 8);
            o[at + 2] = (byte)c1; o[at + 3] = (byte)(c1 >> 8);
            o[at + 4] = (byte)indices; o[at + 5] = (byte)(indices >> 8);
            o[at + 6] = (byte)(indices >> 16); o[at + 7] = (byte)(indices >> 24);
        }

        static int Clamp(double v)
        {
            return v < 0 ? 0 : v > 255 ? 255 : (int)Math.Round(v);
        }

        /// <summary>Decoder (used by the self-test to verify round-trips).</summary>
        public static Bitmap Decode(byte[] dds)
        {
            int h = BitConverter.ToInt32(dds, 12), w = BitConverter.ToInt32(dds, 16);
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            int bw = (w + 3) / 4, bh = (h + 3) / 4, o = 128;
            for (int by = 0; by < bh; by++)
                for (int bx = 0; bx < bw; bx++, o += 8)
                {
                    int c0 = dds[o] | dds[o + 1] << 8, c1 = dds[o + 2] | dds[o + 3] << 8;
                    uint idx = BitConverter.ToUInt32(dds, o + 4);
                    int[] pr = new int[4], pg = new int[4], pb = new int[4];
                    From565(c0, out pr[0], out pg[0], out pb[0]);
                    From565(c1, out pr[1], out pg[1], out pb[1]);
                    if (c0 > c1)
                    {
                        pr[2] = (2 * pr[0] + pr[1]) / 3; pg[2] = (2 * pg[0] + pg[1]) / 3; pb[2] = (2 * pb[0] + pb[1]) / 3;
                        pr[3] = (pr[0] + 2 * pr[1]) / 3; pg[3] = (pg[0] + 2 * pg[1]) / 3; pb[3] = (pb[0] + 2 * pb[1]) / 3;
                    }
                    else
                    {
                        pr[2] = (pr[0] + pr[1]) / 2; pg[2] = (pg[0] + pg[1]) / 2; pb[2] = (pb[0] + pb[1]) / 2;
                    }
                    for (int i = 0; i < 16; i++)
                    {
                        int x = bx * 4 + (i & 3), y = by * 4 + (i >> 2);
                        if (x >= w || y >= h) continue;
                        int k = (int)((idx >> (i * 2)) & 3);
                        bmp.SetPixel(x, y, Color.FromArgb(255, pr[k], pg[k], pb[k]));
                    }
                }
            return bmp;
        }
    }
}
