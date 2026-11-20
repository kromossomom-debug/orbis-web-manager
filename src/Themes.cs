using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace OrbisWebManager
{
    /// <summary>
    /// Local theme repository + PS4 theme builder. Each theme lives in themes/ID/ with a
    /// theme.json, its source images and, once built, a fake-signed .pkg made with LibOrbisPkg's
    /// PkgTool (tools/pkgtool). The layout follows real PS4 system-software themes:
    /// sce_sys/param.sfo + icon0.png, theme.xml, texture/background, texture/function_icon,
    /// texture/content_icon and texture/preview.png.
    ///
    /// Everything is a static image: no video frames and no background music, so the theme costs
    /// the console nothing beyond the stock wallpaper.
    /// </summary>
    public static class Themes
    {
        public class Theme
        {
            public string id { get; set; }
            public string name { get; set; }
            public string author { get; set; }
            public string accent { get; set; }      // #RRGGBB, icons and glow
            public string accent2 { get; set; }     // #RRGGBB, gradient end
            public string fontColor { get; set; }   // #RRGGBB
            public int dim { get; set; }            // 0-80, how much the top of the home background is darkened
            public string home { get; set; }        // file names inside source/
            public string functions { get; set; }
            public string icon { get; set; }        // logo (transparent PNG works best)
            public bool personal { get; set; }      // uses copyrighted art: never share/publish
            public string contentId { get; set; }
            public string builtAt { get; set; }
            public long pkgSize { get; set; }

            // ---- art direction (optional, all have sensible defaults)
            public int homeZoom { get; set; }       // 100-200 (%), 0 = 100
            public int homeX { get; set; }          // 0-100: horizontal focus when zoomed (50 = center)
            public int homeY { get; set; }          // 0-100: vertical focus when zoomed (50 = center)
            public int homeShift { get; set; }      // 0-50 (% of height): lowers the art below the tile row and extends its sky upwards
            public bool logoOnHome { get; set; }    // logo in the free bottom-right area of the home screen
            public int logoSize { get; set; }       // % of screen width, 0 = 20
            public string logoPos { get; set; }     // "br" (default) or "bl"
            public int grade { get; set; }          // 0-100 colour grade + vignette strength
            public string preview { get; set; }     // art for preview.png / Settings › Themes (default: home)
            public string iconArt { get; set; }     // art behind the logo in icon0 (default: logo on gradient)
            public string iconCrop { get; set; }    // "x,y,s" fractions for icon0 art
            public Dictionary<string, string> tiles { get; set; } // content icon -> "file|x,y,s"
            public string bgm { get; set; }         // ATRAC9 .at9 in source/ (home-screen music), optional
            public string bgmLoud { get; set; }     // integrated loudness of the music in LKFS, e.g. "-22.90" (default -23.00)
        }

        static readonly Regex IdRx = new Regex("^[a-z0-9][a-z0-9-]{1,40}$");
        public static string Root { get { string d = Path.Combine(Program.AppDir, "themes"); Directory.CreateDirectory(d); return d; } }
        static string Dir(string id) { if (!IdRx.IsMatch(id ?? "")) throw new Exception("Tema inválido."); return Path.Combine(Root, id); }
        static string PkgToolPath { get { return Path.Combine(Program.AppDir, "tools", "pkgtool", "PkgTool.exe"); } }

        public static Theme Load(string id)
        {
            string f = Path.Combine(Dir(id), "theme.json");
            if (!File.Exists(f)) throw new Exception("Tema não encontrado.");
            var t = new JavaScriptSerializer().Deserialize<Theme>(File.ReadAllText(f));
            if (t.tiles == null) t.tiles = new Dictionary<string, string>();
            return t;
        }

        static void Save(Theme t)
        {
            File.WriteAllText(Path.Combine(Dir(t.id), "theme.json"), new JavaScriptSerializer().Serialize(t));
        }

        public static object List()
        {
            var list = new List<object>();
            foreach (var d in Directory.GetDirectories(Root))
            {
                string id = Path.GetFileName(d);
                if (!IdRx.IsMatch(id) || !File.Exists(Path.Combine(d, "theme.json"))) continue;
                try
                {
                    var t = Load(id);
                    string pkg = PkgPath(t);
                    list.Add(new
                    {
                        t.id, t.name, t.author, t.accent, t.accent2, t.fontColor, t.dim, t.personal, t.contentId,
                        t.logoOnHome, t.grade, t.homeShift, logoPos = t.logoPos ?? "br",
                        bgm = Has(t, t.bgm) ? t.bgm : "", bgmSize = Has(t, t.bgm) ? new FileInfo(Path.Combine(d, "source", t.bgm)).Length : 0,
                        built = pkg != null && File.Exists(pkg),
                        stale = IsStale(id),
                        builtAt = t.builtAt ?? "",
                        pkgSize = pkg != null && File.Exists(pkg) ? new FileInfo(pkg).Length : 0,
                        hasHome = Has(t, t.home), hasFunctions = Has(t, t.functions), hasIcon = Has(t, t.icon),
                        sources = Sources(id)
                    });
                }
                catch { }
            }
            return new { themes = list, pkgTool = File.Exists(PkgToolPath), folder = Root };
        }

        static bool Has(Theme t, string f) { return !string.IsNullOrEmpty(f) && File.Exists(Path.Combine(Dir(t.id), "source", f)); }

        static List<string> Sources(string id)
        {
            var l = new List<string>();
            string d = Path.Combine(Dir(id), "source");
            if (Directory.Exists(d))
                foreach (var f in Directory.GetFiles(d))
                    if (Regex.IsMatch(f, @"\.(png|jpe?g|webp|bmp)$", RegexOptions.IgnoreCase)) l.Add(Path.GetFileName(f));
            l.Sort();
            return l;
        }

        public static Theme Create(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) name = "Meu tema";
            string slug = Regex.Replace(name.ToLowerInvariant().Normalize(NormalizationForm.FormD), @"[^a-z0-9]+", "-").Trim('-');
            if (slug.Length < 2) slug = "tema";
            if (slug.Length > 30) slug = slug.Substring(0, 30).Trim('-');
            string id = slug;
            for (int i = 2; Directory.Exists(Path.Combine(Root, id)); i++) id = slug + "-" + i;
            Directory.CreateDirectory(Path.Combine(Root, id, "source"));
            var t = new Theme { id = id, name = name, author = "Orbis Web Manager", accent = "#FF4FA3", accent2 = "#FFB45A", fontColor = "#FFFFFF", dim = 25, grade = 35, logoOnHome = true, tiles = new Dictionary<string, string>() };
            Save(t);
            return t;
        }

        public static void Update(string id, Dictionary<string, object> d)
        {
            var t = Load(id);
            Func<string, string> s = k => d.ContainsKey(k) && d[k] != null ? Convert.ToString(d[k]).Trim() : null;
            Func<string, bool> color = v => v != null && Regex.IsMatch(v, "^#[0-9A-Fa-f]{6}$");
            Func<string, int, int, int, int> num = (k, cur, lo, hi) => { int v; return int.TryParse(s(k), out v) ? Math.Max(lo, Math.Min(hi, v)) : cur; };
            if (!string.IsNullOrEmpty(s("name"))) t.name = s("name").Length > 60 ? s("name").Substring(0, 60) : s("name");
            if (s("author") != null) t.author = s("author").Length > 60 ? s("author").Substring(0, 60) : s("author");
            if (color(s("accent"))) t.accent = s("accent");
            if (color(s("accent2"))) t.accent2 = s("accent2");
            if (color(s("fontColor"))) t.fontColor = s("fontColor");
            t.dim = num("dim", t.dim, 0, 80);
            t.grade = num("grade", t.grade, 0, 100);
            t.logoSize = num("logoSize", t.logoSize, 0, 40);
            t.homeShift = num("homeShift", t.homeShift, 0, 50);
            if (s("logoPos") == "bl" || s("logoPos") == "br") t.logoPos = s("logoPos");
            var srcs = Sources(id);
            foreach (var slot in new[] { "home", "functions", "icon", "preview", "iconArt" })
            {
                string v = s(slot);
                if (v == null) continue;
                if (v.Length > 0 && !srcs.Contains(v)) continue;
                switch (slot)
                {
                    case "home": t.home = v; break;
                    case "functions": t.functions = v; break;
                    case "icon": t.icon = v; break;
                    case "preview": t.preview = v; break;
                    default: t.iconArt = v; break;
                }
            }
            if (d.ContainsKey("personal")) t.personal = Convert.ToBoolean(d["personal"]);
            if (d.ContainsKey("logoOnHome")) t.logoOnHome = Convert.ToBoolean(d["logoOnHome"]);
            if (d.ContainsKey("removeBgm") && Convert.ToBoolean(d["removeBgm"])) t.bgm = "";
            Save(t);
        }

        /// <summary>True for an ATRAC9 file in a RIFF/WAVE container (what at9tool / ATRACTool write).</summary>
        static bool IsAt9(byte[] d)
        {
            if (d == null || d.Length < 64 || Encoding.ASCII.GetString(d, 0, 4) != "RIFF" || Encoding.ASCII.GetString(d, 8, 4) != "WAVE") return false;
            // fmt chunk: WAVE_FORMAT_EXTENSIBLE with the ATRAC9 sub-format GUID 47E142D2-36BA-4D8D-88FC-61654F8C836C
            byte[] guid = new Guid("47E142D2-36BA-4D8D-88FC-61654F8C836C").ToByteArray();
            for (int i = 12; i + guid.Length <= Math.Min(d.Length, 200); i++)
            {
                bool ok = true;
                for (int k = 0; k < guid.Length && ok; k++) ok = d[i + k] == guid[k];
                if (ok) return true;
            }
            return false;
        }

        /// <summary>Theme music must loop: real PS4 theme .at9 files carry a RIFF 'smpl' chunk with one
        /// loop from the encoder delay to delay + samples - 1. at9tool only writes it when asked,
        /// so add it when it's missing (container edit only, the audio isn't touched).</summary>
        static byte[] EnsureLoop(byte[] d)
        {
            int p = 12, dataAt = -1, total = 0, delay = 256;
            while (p + 8 <= d.Length)
            {
                string id = Encoding.ASCII.GetString(d, p, 4);
                int len = BitConverter.ToInt32(d, p + 4);
                if (id == "smpl") return d;
                if (id == "fact" && len >= 12) { total = BitConverter.ToInt32(d, p + 8); delay = BitConverter.ToInt32(d, p + 16); }
                if (id == "data") { dataAt = p; break; }
                p += 8 + len + (len & 1);
            }
            if (dataAt < 0 || total <= 0) return d;
            var smpl = new byte[8 + 60];
            Encoding.ASCII.GetBytes("smpl").CopyTo(smpl, 0);
            BitConverter.GetBytes(60).CopyTo(smpl, 4);
            BitConverter.GetBytes(20833).CopyTo(smpl, 8 + 8);      // sample period (ns) at 48 kHz
            BitConverter.GetBytes(60).CopyTo(smpl, 8 + 12);        // MIDI unity note
            BitConverter.GetBytes(1).CopyTo(smpl, 8 + 28);         // one loop
            BitConverter.GetBytes(24).CopyTo(smpl, 8 + 32);        // as written by Sony's tools
            BitConverter.GetBytes(delay).CopyTo(smpl, 8 + 44);
            BitConverter.GetBytes(delay + total - 1).CopyTo(smpl, 8 + 48);
            var o = new byte[d.Length + smpl.Length];
            Buffer.BlockCopy(d, 0, o, 0, dataAt);
            Buffer.BlockCopy(smpl, 0, o, dataAt, smpl.Length);
            Buffer.BlockCopy(d, dataAt, o, dataAt + smpl.Length, d.Length - dataAt);
            BitConverter.GetBytes(o.Length - 8).CopyTo(o, 4);
            return o;
        }

        public static string AddSource(string id, byte[] data, string slot)
        {
            var t = Load(id);
            if (slot == "bgm")
            {
                if (!IsAt9(data)) throw new Exception("O arquivo não é ATRAC9 (.at9). Converta a música com o ATRACTool (WAV → AT9) e envie o .at9.");
                // Tested on a real PS4: the theme player stays silent at 168 kbps; 48 kHz stereo at
                // 144 kbps (18000 bytes/s, what Sony's themes use) plays.
                int rate = BitConverter.ToInt32(data, 24), bps = BitConverter.ToInt32(data, 28), ch = BitConverter.ToUInt16(data, 22);
                if (Encoding.ASCII.GetString(data, 12, 4) == "fmt " && (rate != 48000 || ch != 2 || bps != 18000))
                    throw new Exception("A música precisa ser ATRAC9 48 kHz, estéreo, 144 kbps (esta tem " + rate + " Hz, " + ch + " canal(is), " + (bps * 8 / 1000) + " kbps). No ATRACTool: 設定 › ビットレート 144, サンプリング周波数 48000.");
                data = EnsureLoop(data);
                if (data.Length > 30 * 1024 * 1024) throw new Exception("Música muito grande (máx. 30 MB). Use um trecho de 1 a 2 minutos.");
                string an = "bgm_" + DateTime.Now.ToString("HHmmss") + ".at9";
                File.WriteAllBytes(Path.Combine(Dir(id), "source", an), data);
                t.bgm = an;
                Save(t);
                return an;
            }
            using (var img = Covers.LoadImage(data)) { }   // validates it's an image
            string ext = Covers.SniffImageType(data) == "image/png" ? ".png" : ".jpg";
            string name = (slot ?? "img") + "_" + DateTime.Now.ToString("HHmmss") + ext;
            File.WriteAllBytes(Path.Combine(Dir(id), "source", name), data);
            if (slot == "home") t.home = name; else if (slot == "functions") t.functions = name; else if (slot == "icon") t.icon = name;
            Save(t);
            return name;
        }

        public static void Delete(string id)
        {
            Directory.Delete(Dir(id), true);
        }

        public static byte[] SourceImage(string id, string file)
        {
            if (string.IsNullOrEmpty(file) || file.IndexOfAny(new[] { '/', '\\' }) >= 0 || file.Contains("..")) return null;
            string f = Path.Combine(Dir(id), "source", file);
            return File.Exists(f) ? File.ReadAllBytes(f) : null;
        }

        public static byte[] Preview(string id)
        {
            string f = Path.Combine(Dir(id), "build", "texture", "preview.png");
            if (File.Exists(f)) return File.ReadAllBytes(f);
            var t = Load(id);
            using (var bmp = PreviewImage(t)) return Png(bmp, 960, 540);
        }

        /// <summary>Approximation of the PS4 home screen over the theme (used by the Orbis UI).</summary>
        public static byte[] MockPng(string id, string screen)
        {
            var t = Load(id);
            using (var bmp = screen == "functions" ? MockFunctions(t) : MockHome(t)) return Png(bmp, 1280, 720);
        }

        static string PkgPath(Theme t)
        {
            if (string.IsNullOrEmpty(t.contentId)) return null;
            return Path.Combine(Dir(t.id), "pkg", t.contentId + "-A0100-V0100.pkg");
        }

        public static string BuiltPkg(string id)
        {
            var t = Load(id);
            string p = PkgPath(t);
            return p != null && File.Exists(p) ? p : null;
        }

        // ================================================================ image helpers

        static Color C(string hex, int alpha = 255)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length != 7) hex = "#FFFFFF";
            return Color.FromArgb(alpha, Convert.ToInt32(hex.Substring(1, 2), 16), Convert.ToInt32(hex.Substring(3, 2), 16), Convert.ToInt32(hex.Substring(5, 2), 16));
        }

        static Bitmap LoadSource(Theme t, string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            string f = Path.Combine(Dir(t.id), "source", file);
            return File.Exists(f) ? Covers.LoadImage(File.ReadAllBytes(f)) : null;
        }

        static Graphics G(Bitmap b)
        {
            var g = Graphics.FromImage(b);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.CompositingQuality = CompositingQuality.HighQuality;
            return g;
        }

        static Bitmap New(int w, int h) { return new Bitmap(w, h, PixelFormat.Format32bppArgb); }

        /// <summary>Draws src covering rect; zoom &gt;= 1 crops further around (fx, fy) in 0..1.</summary>
        static void DrawCover(Graphics g, Image src, RectangleF r, float zoom, float fx, float fy)
        {
            float scale = Math.Max(r.Width / src.Width, r.Height / src.Height) * Math.Max(1f, zoom);
            float sw = src.Width * scale, sh = src.Height * scale;
            float x = r.X + (r.Width - sw) * fx, y = r.Y + (r.Height - sh) * fy;
            using (var ia = new ImageAttributes())
            {
                ia.SetWrapMode(WrapMode.TileFlipXY);   // no dark fringe at the edges
                g.DrawImage(src, new Rectangle((int)Math.Round(x), (int)Math.Round(y), (int)Math.Round(sw), (int)Math.Round(sh)), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
            }
        }

        /// <summary>Crops a square from "x,y,s" (fractions: left, top, side as part of the height).</summary>
        static void DrawCrop(Graphics g, Image src, Rectangle dst, string crop)
        {
            float x = 0.5f, y = 0, s = 1;
            var p = (crop ?? "").Split(',');
            if (p.Length == 3)
            {
                float.TryParse(p[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x);
                float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y);
                float.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out s);
            }
            else { s = 1; x = (src.Width - src.Height) / 2f / src.Width; y = 0; }
            float side = Math.Min(src.Height * s, Math.Min(src.Width, src.Height));
            float sx = Math.Max(0, Math.Min(src.Width - side, src.Width * x));
            float sy = Math.Max(0, Math.Min(src.Height - side, src.Height * y));
            using (var ia = new ImageAttributes())
            {
                ia.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(src, dst, sx, sy, side, side, GraphicsUnit.Pixel, ia);
            }
        }

        /// <summary>Cheap, smooth blur: shrink then enlarge (twice).</summary>
        static Bitmap Blur(Image src, int w, int h, int factor)
        {
            int sw = Math.Max(4, w / factor), sh = Math.Max(4, h / factor);
            using (var small = New(sw, sh))
            {
                using (var g = G(small)) { g.InterpolationMode = InterpolationMode.HighQualityBilinear; DrawCover(g, src, new RectangleF(0, 0, sw, sh), 1, .5f, .5f); }
                using (var mid = New(sw * 2, sh * 2))
                {
                    using (var g = G(mid)) DrawCover(g, small, new RectangleF(0, 0, sw * 2, sh * 2), 1, .5f, .5f);
                    var outp = New(w, h);
                    using (var g = G(outp)) DrawCover(g, mid, new RectangleF(0, 0, w, h), 1, .5f, .5f);
                    return outp;
                }
            }
        }

        /// <summary>Vice-City style grade: gentle contrast, a little saturation, purple shadows,
        /// warm highlights, and a vignette. strength 0..100.</summary>
        static void Grade(Bitmap b, int strength, Color shadow, Color highlight, float vignette)
        {
            if (strength <= 0 && vignette <= 0) return;
            float k = Math.Max(0, Math.Min(100, strength)) / 100f;
            var rect = new Rectangle(0, 0, b.Width, b.Height);
            var data = b.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            int bytes = data.Stride * b.Height;
            var px = new byte[bytes];
            Marshal.Copy(data.Scan0, px, 0, bytes);
            float cx = b.Width / 2f, cy = b.Height / 2f, maxd = (float)Math.Sqrt(cx * cx + cy * cy);
            for (int y = 0; y < b.Height; y++)
            {
                int row = y * data.Stride;
                for (int x = 0; x < b.Width; x++)
                {
                    int i = row + x * 4;
                    float bl = px[i] / 255f, gr = px[i + 1] / 255f, rd = px[i + 2] / 255f;
                    float lum = 0.2126f * rd + 0.7152f * gr + 0.0722f * bl;
                    // saturation +18%
                    float sat = 1 + 0.18f * k;
                    rd = lum + (rd - lum) * sat; gr = lum + (gr - lum) * sat; bl = lum + (bl - lum) * sat;
                    // soft S-curve
                    float c = 0.10f * k;
                    rd = rd + c * (rd - 0.5f) * (1 - Math.Abs(2 * rd - 1));
                    gr = gr + c * (gr - 0.5f) * (1 - Math.Abs(2 * gr - 1));
                    bl = bl + c * (bl - 0.5f) * (1 - Math.Abs(2 * bl - 1));
                    // split toning
                    float sw = (1 - lum) * (1 - lum) * 0.22f * k, hw = lum * lum * 0.16f * k;
                    rd = rd * (1 - sw - hw) + shadow.R / 255f * sw + highlight.R / 255f * hw;
                    gr = gr * (1 - sw - hw) + shadow.G / 255f * sw + highlight.G / 255f * hw;
                    bl = bl * (1 - sw - hw) + shadow.B / 255f * sw + highlight.B / 255f * hw;
                    // vignette
                    if (vignette > 0)
                    {
                        float dx = x - cx, dy = (y - cy) * 1.15f;
                        float d = (float)Math.Sqrt(dx * dx + dy * dy) / maxd;
                        float v = 1 - vignette * Math.Max(0, d - 0.55f) * 1.6f;
                        rd *= v; gr *= v; bl *= v;
                    }
                    px[i] = (byte)Math.Max(0, Math.Min(255, bl * 255));
                    px[i + 1] = (byte)Math.Max(0, Math.Min(255, gr * 255));
                    px[i + 2] = (byte)Math.Max(0, Math.Min(255, rd * 255));
                }
            }
            Marshal.Copy(px, 0, data.Scan0, bytes);
            b.UnlockBits(data);
        }

        static void VerticalShade(Graphics g, int w, int y0, int y1, int a0, int a1)
        {
            if (y1 <= y0) return;
            using (var br = new LinearGradientBrush(new Rectangle(0, y0 - 1, w, y1 - y0 + 2), Color.FromArgb(a0, 8, 4, 18), Color.FromArgb(a1, 8, 4, 18), 90f))
                g.FillRectangle(br, 0, y0, w, y1 - y0);
        }

        /// <summary>Draws the logo with a soft shadow so it reads on any background.</summary>
        static void DrawLogo(Graphics g, Image logo, RectangleF box, bool alignLeft = false)
        {
            float scale = Math.Min(box.Width / logo.Width, box.Height / logo.Height);
            float w = logo.Width * scale, h = logo.Height * scale;
            float x = alignLeft ? box.X : box.Right - w, y = box.Bottom - h;
            using (var shadow = New((int)w + 80, (int)h + 80))
            {
                using (var sg = G(shadow))
                {
                    var cm = new ColorMatrix(new[] { new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0.55f, 0 }, new float[] { 0, 0, 0, 0, 1 } });
                    using (var ia = new ImageAttributes()) { ia.SetColorMatrix(cm); sg.DrawImage(logo, new Rectangle(40, 40, (int)w, (int)h), 0, 0, logo.Width, logo.Height, GraphicsUnit.Pixel, ia); }
                }
                using (var soft = Blur(shadow, shadow.Width, shadow.Height, 10)) g.DrawImage(soft, x - 40 + 6, y - 40 + 10);
            }
            g.DrawImage(logo, x, y, w, h);
        }

        static byte[] Png(Bitmap b, int w = 0, int h = 0)
        {
            using (var ms = new MemoryStream())
            {
                if (w > 0 && (b.Width != w || b.Height != h))
                    using (var r = New(w, h)) { using (var g = G(r)) g.DrawImage(b, 0, 0, w, h); r.Save(ms, ImageFormat.Png); }
                else b.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }

        static void SavePng(Bitmap b, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            b.Save(path, ImageFormat.Png);
        }

        // ================================================================ theme images

        static readonly Color ShadowTone = Color.FromArgb(46, 18, 88);   // deep Vice City purple
        static Color HighTone(Theme t) { var a = C(t.accent2); return Color.FromArgb(Math.Min(255, a.R + 20), Math.Min(255, a.G + 40), Math.Min(255, a.B + 40)); }

        // PS4 home layout at 1080p (measured from a console screenshot): function bar ~44-150,
        // tile row ~190-610 across the whole width, free area below ~620.
        const int FnBarBottom = 160, TilesBottom = 615;

        static Bitmap Home(Theme t, int w, int h)
        {
            var b = New(w, h);
            using (var g = G(b))
            using (var src = LoadSource(t, t.home))
            {
                float zoom = (t.homeZoom <= 0 ? 100 : t.homeZoom) / 100f, fx = (t.homeX <= 0 ? 50 : t.homeX) / 100f, fy = (t.homeY <= 0 ? 50 : t.homeY) / 100f;
                if (src == null)
                    using (var br = new LinearGradientBrush(new Rectangle(0, 0, w, h), C(t.accent), C(t.accent2), 35f)) g.FillRectangle(br, 0, 0, w, h);
                else if (t.homeShift <= 0)
                    DrawCover(g, src, new RectangleF(0, 0, w, h), zoom, fx, fy);
                else
                    DrawShifted(g, src, w, h, Math.Min(50, t.homeShift) / 100f, zoom, fx);
            }
            Grade(b, t.grade, ShadowTone, HighTone(t), t.grade > 0 ? 0.55f : 0);
            using (var g = G(b))
            {
                float sy = h / 1080f;
                // readability: dark band behind the function bar, lighter behind the tiles, clear below
                int a = Math.Min(230, 40 + t.dim * 3);
                VerticalShade(g, w, 0, (int)(FnBarBottom * sy), a, a / 2);
                VerticalShade(g, w, (int)(FnBarBottom * sy), (int)(TilesBottom * sy), a / 2, 0);
                VerticalShade(g, w, (int)(h * 0.86f), h, 0, 90);
                if (t.logoOnHome)
                    using (var logo = LoadSource(t, t.icon))
                        if (logo != null)
                        {
                            float lw = w * (t.logoSize <= 0 ? 20 : t.logoSize) / 100f;
                            float lx = t.logoPos == "bl" ? 80 * sy : w - lw - 70 * sy;
                            DrawLogo(g, logo, new RectangleF(lx, h - lw - 60 * sy, lw, lw - 40 * sy), t.logoPos == "bl");
                        }
            }
            return b;
        }

        /// <summary>Places the art lower (so faces sit under the PS4 tile row) and fills the space
        /// above with a soft, blurred continuation of the art's own sky, cross-faded in.</summary>
        static void DrawShifted(Graphics g, Image src, int w, int h, float shift, float zoom, float fx)
        {
            int shiftPx = (int)(h * shift);
            int feather = Math.Max(40, (int)(h * 0.16f));
            // 1) sky: the top band of the art stretched up and blurred into a smooth gradient
            int skyH = shiftPx + feather;
            {
                // Average the art's top band down to a tiny grid (halving each step, so every pixel
                // counts) and enlarge it: a smooth sky with the art's colours and no streaks from
                // anything that touches the top edge (hair, palm trunks...).
                float scale = Math.Max((float)w / src.Width, (float)h / src.Height) * Math.Max(1f, zoom);
                float srcW = w / scale, srcX = (src.Width - srcW) * fx;
                int bandH = (int)(src.Height * 0.10f);
                Bitmap cur = New((int)srcW, bandH);
                using (var cg = G(cur))
                using (var ia = new ImageAttributes())
                {
                    ia.SetWrapMode(WrapMode.TileFlipXY);
                    cg.DrawImage(src, new Rectangle(0, 0, cur.Width, cur.Height), srcX, 0, srcW, bandH, GraphicsUnit.Pixel, ia);
                }
                while (cur.Width > 12 || cur.Height > 4)
                {
                    var next = New(Math.Max(12, cur.Width / 2), Math.Max(4, cur.Height / 2));
                    using (var ng = G(next)) { ng.InterpolationMode = InterpolationMode.HighQualityBilinear; ng.DrawImage(cur, new Rectangle(0, 0, next.Width, next.Height)); }
                    cur.Dispose();
                    cur = next;
                }
                // Robust sky: per row, cells much darker than the row's median (a head or a trunk
                // touching the top edge) are replaced by the median colour.
                for (int y = 0; y < cur.Height; y++)
                {
                    var cells = new List<Color>();
                    for (int x = 0; x < cur.Width; x++) cells.Add(cur.GetPixel(x, y));
                    var sorted = new List<Color>(cells);
                    sorted.Sort((a, c) => (a.R * 3 + a.G * 6 + a.B).CompareTo(c.R * 3 + c.G * 6 + c.B));
                    Color med = sorted[sorted.Count / 2];
                    int medL = med.R * 3 + med.G * 6 + med.B;
                    for (int x = 0; x < cur.Width; x++)
                    {
                        var c = cells[x];
                        int l = c.R * 3 + c.G * 6 + c.B;
                        if (l < medL * 0.85) cur.SetPixel(x, y, Color.FromArgb(255, (c.R + med.R * 3) / 4, (c.G + med.G * 3) / 4, (c.B + med.B * 3) / 4));
                    }
                }
                using (cur)
                using (var sky = New(w, skyH))
                {
                    using (var sg = G(sky))
                    using (var ia = new ImageAttributes())
                    {
                        ia.SetWrapMode(WrapMode.TileFlipXY);
                        sg.DrawImage(cur, new Rectangle(0, 0, w, skyH), 0, 0, cur.Width, cur.Height, GraphicsUnit.Pixel, ia);
                    }
                    using (var soft = Blur(sky, w, skyH, 16)) g.DrawImage(soft, 0, 0, w, skyH);
                }
            }
            // 2) the art, lowered, with its top edge faded into the sky
            using (var art = New(w, h))
            {
                using (var ag = G(art)) DrawCover(ag, src, new RectangleF(0, shiftPx, w, h), zoom, fx, 0f);
                var rect = new Rectangle(0, 0, w, h);
                var data = art.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                var px = new byte[data.Stride * h];
                Marshal.Copy(data.Scan0, px, 0, px.Length);
                for (int y = 0; y < Math.Min(h, shiftPx + feather); y++)
                {
                    float a = y < shiftPx ? 0 : (y - shiftPx) / (float)feather;
                    a = a * a * (3 - 2 * a);   // smoothstep
                    int row = y * data.Stride;
                    for (int x = 0; x < w; x++) px[row + x * 4 + 3] = (byte)(px[row + x * 4 + 3] * a);
                }
                Marshal.Copy(px, 0, data.Scan0, px.Length);
                art.UnlockBits(data);
                g.DrawImage(art, 0, 0, w, h);
            }
        }

        static Bitmap Functions(Theme t, int w, int h)
        {
            var b = New(w, h);
            using (var g = G(b))
            using (var src = LoadSource(t, string.IsNullOrEmpty(t.functions) ? t.home : t.functions))
            {
                if (src != null) DrawCover(g, src, new RectangleF(0, 0, w, h), 1, .5f, .5f);
                else using (var br = new LinearGradientBrush(new Rectangle(0, 0, w, h), C(t.accent), C(t.accent2), 35f)) g.FillRectangle(br, 0, 0, w, h);
            }
            Grade(b, t.grade, ShadowTone, HighTone(t), t.grade > 0 ? 0.7f : 0);
            using (var g = G(b))
            {
                // the function screen is mostly panels and text: keep it calm and dark at the top
                VerticalShade(g, w, 0, h, Math.Min(220, 110 + t.dim * 2), Math.Min(160, 50 + t.dim));
            }
            return b;
        }

        static Bitmap PreviewImage(Theme t)
        {
            var b = New(1920, 1080);
            using (var g = G(b))
            using (var src = LoadSource(t, string.IsNullOrEmpty(t.preview) ? t.home : t.preview))
            {
                if (src != null) DrawCover(g, src, new RectangleF(0, 0, 1920, 1080), 1, .5f, .5f);
                else using (var br = new LinearGradientBrush(new Rectangle(0, 0, 1920, 1080), C(t.accent), C(t.accent2), 35f)) g.FillRectangle(br, 0, 0, 1920, 1080);
            }
            Grade(b, t.grade, ShadowTone, HighTone(t), t.grade > 0 ? 0.5f : 0);
            using (var g = G(b))
            {
                VerticalShade(g, 1920, 700, 1080, 0, 150);
                using (var logo = LoadSource(t, t.icon))
                    if (logo != null) DrawLogo(g, logo, new RectangleF(1920 - 520 - 90, 1080 - 380 - 70, 520, 380));
            }
            return b;
        }

        static Bitmap Icon0(Theme t)
        {
            const int S = 512;
            var b = New(S, S);
            using (var g = G(b))
            {
                using (var art = LoadSource(t, t.iconArt))
                {
                    if (art != null) DrawCrop(g, art, new Rectangle(0, 0, S, S), t.iconCrop);
                    else using (var br = new LinearGradientBrush(new Rectangle(0, 0, S, S), C(t.accent), C(t.accent2), 45f)) g.FillRectangle(br, 0, 0, S, S);
                }
            }
            if (!string.IsNullOrEmpty(t.iconArt)) Grade(b, t.grade, ShadowTone, HighTone(t), 0.5f);
            using (var g = G(b))
            using (var logo = LoadSource(t, t.icon))
            {
                if (logo != null)
                {
                    if (!string.IsNullOrEmpty(t.iconArt))
                    {
                        VerticalShade(g, S, S / 3, S, 0, 200);
                        DrawLogo(g, logo, new RectangleF(S * 0.22f, S * 0.50f, S * 0.70f, S * 0.44f));
                    }
                    else
                    {
                        float scale = Math.Min(430f / logo.Width, 430f / logo.Height);
                        g.DrawImage(logo, (S - logo.Width * scale) / 2f, (S - logo.Height * scale) / 2f, logo.Width * scale, logo.Height * scale);
                    }
                }
                else
                    using (var f = new Font("Segoe UI", 64, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString(t.name, f, Brushes.White, new RectangleF(24, 24, 464, 464), sf);
            }
            return b;
        }

        // Segoe MDL2 Assets ships with Windows 10/11
        static readonly Dictionary<string, string> FunctionGlyphs = new Dictionary<string, string>
        {
            { "community", "" }, { "event", "" }, { "friend", "" }, { "message", "" }, { "notification", "" },
            { "party", "" }, { "power", "" }, { "setting", "" }, { "trophy", "" }
        };
        static readonly Dictionary<string, string> ContentGlyphs = new Dictionary<string, string>
        {
            { "browser", "" }, { "disc", "" }, { "discoverlay", "" }, { "folder", "" }, { "gallery", "" },
            { "library", "" }, { "livefromps", "" }, { "shareplay", "" }, { "tvvideo", "" }, { "usbmusic", "" }
        };

        /// <summary>Neon glyph: drawn 4x larger, then reduced, with a coloured glow and a
        /// white→tint gradient fill. Transparent background.</summary>
        static Bitmap Neon(string glyph, int size, float glyphFrac, Color glow, Color tint, float glowStrength)
        {
            int S = size * 4;
            var big = New(S, S);
            using (var g = G(big))
            using (var f = new Font("Segoe MDL2 Assets", S * glyphFrac, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                var rect = new RectangleF(0, S * 0.02f, S, S);
                if (glowStrength > 0)
                {
                    using (var mask = New(S, S))
                    {
                        using (var mg = G(mask))
                        using (var gb = new SolidBrush(Color.FromArgb(255, glow)))
                            mg.DrawString(glyph, f, gb, rect, sf);
                        using (var soft = Blur(mask, S, S, 14))
                        {
                            var cm = new ColorMatrix { Matrix33 = glowStrength };
                            using (var ia = new ImageAttributes())
                            {
                                ia.SetColorMatrix(cm);
                                g.DrawImage(soft, new Rectangle(0, 0, S, S), 0, 0, S, S, GraphicsUnit.Pixel, ia);
                                g.DrawImage(soft, new Rectangle(0, 0, S, S), 0, 0, S, S, GraphicsUnit.Pixel, ia);
                            }
                        }
                    }
                }
                using (var br = new LinearGradientBrush(new Rectangle(0, (int)(S * 0.15f), S, (int)(S * 0.7f)), Color.White, tint, 90f))
                    g.DrawString(glyph, f, br, rect, sf);
            }
            var b = New(size, size);
            using (var g = G(b)) g.DrawImage(big, 0, 0, size, size);
            big.Dispose();
            return b;
        }

        static Bitmap GlowDisc(Theme t)
        {
            var b = New(152, 152);
            using (var g = G(b))
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(4, 4, 144, 144);
                using (var br = new PathGradientBrush(path))
                {
                    br.CenterColor = C(t.accent, 200);
                    br.SurroundColors = new[] { C(t.accent2, 0) };
                    br.FocusScales = new PointF(0.15f, 0.15f);
                    g.FillEllipse(br, 4, 4, 144, 144);
                }
            }
            return b;
        }

        /// <summary>Content tile: a square crop of the theme's art with a small neon badge, or a
        /// transparent neon glyph when no art is assigned (and always for the disc overlays, which
        /// the console draws on top of the disc's own artwork).</summary>
        static Bitmap ContentIcon(Theme t, string name, string glyph)
        {
            const int S = 512;
            string spec;
            bool art = name != "disc" && name != "discoverlay" && t.tiles != null && t.tiles.TryGetValue(name, out spec) && !string.IsNullOrEmpty(spec);
            if (!art) return Neon(glyph, S, 0.42f, C(t.accent), Color.FromArgb(255, 220, 236), 0.9f);
            t.tiles.TryGetValue(name, out spec);
            var parts = spec.Split('|');
            var b = New(S, S);
            using (var src = LoadSource(t, parts[0]))
            {
                if (src == null) { b.Dispose(); return Neon(glyph, S, 0.42f, C(t.accent), Color.FromArgb(255, 220, 236), 0.9f); }
                using (var g = G(b)) DrawCrop(g, src, new Rectangle(0, 0, S, S), parts.Length > 1 ? parts[1] : null);
            }
            Grade(b, t.grade, ShadowTone, HighTone(t), 0.35f);
            using (var g = G(b))
            {
                VerticalShade(g, S, (int)(S * 0.55f), S, 0, 170);
                using (var disc = new GraphicsPath())
                {
                    disc.AddEllipse(22, S - 168, 146, 146);
                    using (var br = new PathGradientBrush(disc) { CenterColor = Color.FromArgb(150, 10, 6, 24), SurroundColors = new[] { Color.FromArgb(0, 10, 6, 24) } })
                        g.FillEllipse(br, 22, S - 168, 146, 146);
                }
                using (var badge = Neon(glyph, 150, 0.52f, C(t.accent), Color.FromArgb(255, 236, 246), 1f))
                    g.DrawImage(badge, 20, S - 170, 150, 150);
                // thin neon frame along the bottom edge
                using (var br = new LinearGradientBrush(new Rectangle(0, S - 8, S, 8), C(t.accent), C(t.accent2), 0f)) g.FillRectangle(br, 0, S - 6, S, 6);
            }
            return b;
        }

        // ================================================================ home-screen simulation

        static List<string> GameIconFiles(int max)
        {
            var list = new List<string>();
            foreach (var gi in Library.Games())
            {
                string f = Covers.CachedIconPath(gi.id);
                if (f != null && File.Exists(f)) list.Add(f);
                if (list.Count >= max) break;
            }
            return list;
        }

        static void Tile(Graphics g, Image img, RectangleF r)
        {
            using (var sh = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillRectangle(sh, r.X + 4, r.Y + 6, r.Width, r.Height);
            if (img != null) DrawCover(g, img, r, 1, .5f, .5f);
            else using (var br = new SolidBrush(Color.FromArgb(160, 30, 30, 50))) g.FillRectangle(br, r);
        }

        static Bitmap MockHome(Theme t)
        {
            var b = Home(t, 1920, 1080);
            using (var g = G(b))
            using (var font = new Font("Segoe UI", 26, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var title = new Font("Segoe UI Light", 52, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var small = new Font("Segoe UI", 22, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                // function bar
                string[] fn = { "notification", "friend", "message", "party", "trophy", "setting", "power" };
                float x = 220;
                foreach (var n in fn)
                {
                    using (var ic = Neon(FunctionGlyphs[n], 64, 0.6f, C(t.accent), Color.FromArgb(255, 230, 242), 0.6f)) g.DrawImage(ic, x, 70, 64, 64);
                    x += 150;
                }
                g.DrawString("22:21", font, Brushes.White, 1740, 86);
                using (var pen = new Pen(Color.FromArgb(70, 255, 255, 255), 2)) g.DrawLine(pen, 90, 152, 1830, 152);

                // tile row: system tile, selected game, other games
                var icons = GameIconFiles(8);
                using (var lib = ContentIcon(t, "library", ContentGlyphs["library"])) Tile(g, lib, new RectangleF(96, 224, 218, 218));
                Image sel = icons.Count > 0 ? Covers.LoadImage(File.ReadAllBytes(icons[0])) : null;
                Tile(g, sel, new RectangleF(335, 189, 355, 355));
                if (sel != null) sel.Dispose();
                using (var band = new SolidBrush(Color.FromArgb(200, 12, 10, 24))) g.FillRectangle(band, 335, 544, 355, 66);
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString("Iniciar", font, Brushes.White, new RectangleF(335, 544, 355, 66), sf);
                using (var pen = new Pen(Color.White, 4)) g.DrawRectangle(pen, 333, 187, 359, 425);
                string selName = Library.Games().Count > 0 ? Library.Games()[0].name : "Jogo";
                using (var sh = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) g.DrawString(selName, title, sh, 711, 468);
                g.DrawString(selName, title, Brushes.White, 708, 465);
                x = 708;
                for (int i = 1; i < icons.Count && x < 1920; i++)
                {
                    using (var im = Covers.LoadImage(File.ReadAllBytes(icons[i]))) Tile(g, im, new RectangleF(x, 224, 218, 218));
                    x += 227;
                }
                foreach (var n in new[] { "tvvideo", "usbmusic" })
                {
                    if (x >= 1920) break;
                    using (var ci = ContentIcon(t, n, ContentGlyphs[n])) Tile(g, ci, new RectangleF(x, 224, 218, 218));
                    x += 227;
                }
                g.DrawString("Simulação aproximada da tela inicial do PS4", small, new SolidBrush(Color.FromArgb(150, 255, 255, 255)), 40, 1030);
            }
            return b;
        }

        static Bitmap MockFunctions(Theme t)
        {
            var b = Functions(t, 1920, 1080);
            using (var g = G(b))
            using (var font = new Font("Segoe UI", 24, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                string[] fn = { "notification", "friend", "message", "party", "community", "event", "trophy", "setting", "power" };
                float x = 170;
                using (var glow = GlowDisc(t)) g.DrawImage(glow, x - 12, 180 - 12, 152, 152);
                foreach (var n in fn)
                {
                    using (var ic = Neon(FunctionGlyphs[n], 128, 0.6f, C(t.accent), Color.FromArgb(255, 230, 242), 1f)) g.DrawImage(ic, x, 192, 128, 128);
                    x += 180;
                }
                using (var panel = new SolidBrush(Color.FromArgb(120, 10, 8, 20)))
                    for (int i = 0; i < 3; i++) g.FillRectangle(panel, 170 + i * 540, 400, 500, 460);
                g.DrawString("Simulação aproximada da área de funções", font, new SolidBrush(Color.FromArgb(150, 255, 255, 255)), 40, 1030);
            }
            return b;
        }

        // ================================================================ param.sfo writer

        static byte[] Sfo(List<KeyValuePair<string, object>> entries)
        {
            entries.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            var keys = new MemoryStream();
            var data = new MemoryStream();
            var index = new MemoryStream();
            var iw = new BinaryWriter(index);
            foreach (var e in entries)
            {
                int keyOff = (int)keys.Length;
                byte[] k = Encoding.ASCII.GetBytes(e.Key + "\0");
                keys.Write(k, 0, k.Length);
                int dataOff = (int)data.Length;
                ushort fmt; int len, max;
                byte[] val;
                if (e.Value is int)
                {
                    fmt = 0x0404; val = BitConverter.GetBytes((int)e.Value); len = 4; max = 4;
                }
                else
                {
                    var sv = (string[])e.Value;           // { value, maxLen }
                    byte[] s = Encoding.UTF8.GetBytes(sv[0] + "\0");
                    fmt = 0x0204; len = s.Length; max = Math.Max(int.Parse(sv[1]), (len + 3) / 4 * 4);
                    val = new byte[max];
                    Buffer.BlockCopy(s, 0, val, 0, s.Length);
                }
                data.Write(val, 0, val.Length);
                iw.Write((ushort)keyOff); iw.Write(fmt); iw.Write(len); iw.Write(max); iw.Write(dataOff);
            }
            while (keys.Length % 4 != 0) keys.WriteByte(0);
            int keyStart = 20 + (int)index.Length;
            int dataStart = keyStart + (int)keys.Length;
            var o = new MemoryStream();
            var w = new BinaryWriter(o);
            w.Write(new byte[] { 0, (byte)'P', (byte)'S', (byte)'F' });
            w.Write(0x00000101);
            w.Write(keyStart); w.Write(dataStart); w.Write(entries.Count);
            w.Write(index.ToArray()); w.Write(keys.ToArray()); w.Write(data.ToArray());
            return o.ToArray();
        }

        static KeyValuePair<string, object> S(string k, string v, int max) { return new KeyValuePair<string, object>(k, new[] { v, max.ToString() }); }
        static KeyValuePair<string, object> I(string k, int v) { return new KeyValuePair<string, object>(k, v); }

        static string Hex(string s, int n)
        {
            using (var sha = SHA1.Create())
            {
                var sb = new StringBuilder();
                foreach (byte x in sha.ComputeHash(Encoding.UTF8.GetBytes(s))) sb.Append(x.ToString("X2"));
                return sb.ToString().Substring(0, n);
            }
        }

        // ================================================================ build

        public static object Build(string id)
        {
            var t = Load(id);
            if (!File.Exists(PkgToolPath)) throw new Exception("O PkgTool não está em tools\\pkgtool\\. Ele monta o .pkg do tema (LibOrbisPkg, código aberto).");
            string root = Dir(id);
            string build = Path.Combine(root, "build");
            if (Directory.Exists(build)) Directory.Delete(build, true);
            Directory.CreateDirectory(build);

            // A stable ID per theme, so rebuilding replaces the installed theme instead of adding a copy.
            // ("orbis-theme2": themes built before the IRO_TAG fix installed as add-ons; a new ID keeps
            // a leftover from those from blocking the reinstall)
            string h = Hex("orbis-theme2:" + t.id, 13);
            string titleId = "CUSA" + (10000 + Convert.ToInt32(h.Substring(0, 4), 16) % 89999).ToString("00000");
            string label = ("OT" + h).Substring(0, 15) + "T";
            t.contentId = "UP9000-" + titleId + "_00-" + label;

            using (var home = Home(t, 1920, 1080)) SavePng(home, Path.Combine(build, "texture", "background", "homescreen.png"));
            using (var fn = Functions(t, 1920, 1080)) SavePng(fn, Path.Combine(build, "texture", "background", "functionscreen.png"));
            using (var pv = PreviewImage(t)) SavePng(pv, Path.Combine(build, "texture", "preview.png"));
            using (var ic = Icon0(t)) SavePng(ic, Path.Combine(build, "sce_sys", "icon0.png"));
            using (var glow = GlowDisc(t))
                foreach (var kv in FunctionGlyphs)
                {
                    using (var bmp = Neon(kv.Value, 128, 0.6f, C(t.accent), Color.FromArgb(255, 230, 242), 1f)) SavePng(bmp, Path.Combine(build, "texture", "function_icon", kv.Key + ".png"));
                    SavePng(glow, Path.Combine(build, "texture", "function_icon", kv.Key + "_glow.png"));
                }
            foreach (var kv in ContentGlyphs)
                using (var bmp = ContentIcon(t, kv.Key, kv.Value)) SavePng(bmp, Path.Combine(build, "texture", "content_icon", kv.Key + ".png"));
            bool hasBgm = !string.IsNullOrEmpty(t.bgm) && File.Exists(Path.Combine(root, "source", t.bgm));
            if (hasBgm)
            {
                Directory.CreateDirectory(Path.Combine(build, "sound"));
                File.Copy(Path.Combine(root, "source", t.bgm), Path.Combine(build, "sound", "bgm_home.at9"));
            }

            string font = (t.fontColor ?? "#FFFFFF").Substring(1).ToUpperInvariant();
            string accent = (t.accent ?? "#FFFFFF").Substring(1).ToUpperInvariant();
            File.WriteAllText(Path.Combine(build, "theme.xml"),
                "<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>\r\n<theme>\r\n" +
                "  <format-version>4.0</format-version>\r\n" +
                "  <themecolor>2</themecolor>\r\n" +
                "  <fontcolor>#FF" + font + "</fontcolor>\r\n" +
                "  <fontshadowcolor>#80000000</fontshadowcolor>\r\n" +
                "  <focuscolor>#FF" + accent + "</focuscolor>\r\n" +
                "  <homescreen-dimmer>#00000000</homescreen-dimmer>\r\n" +
                "  <functionscreen-dimmer>#20000000</functionscreen-dimmer>\r\n" +
                "  <titlename-dimmer>#60000000</titlename-dimmer>\r\n" +
                "  <homebgm-enable>" + (hasBgm ? "True" : "False") + "</homebgm-enable>\r\n" +
                "  <label>" + System.Security.SecurityElement.Escape(t.id) + "</label>\r\n</theme>\r\n", new UTF8Encoding(true));

            string title = t.name.Length > 60 ? t.name.Substring(0, 60) : t.name;
            File.WriteAllBytes(Path.Combine(build, "sce_sys", "param.sfo"), Sfo(new List<KeyValuePair<string, object>>
            {
                I("ATTRIBUTE", 0),
                S("CATEGORY", "ac", 4),
                S("CONTENT_ID", t.contentId, 48),
                S("FORMAT", "obs", 4),
                I("IRO_TAG", 2),
                S("PROVIDER", string.IsNullOrEmpty(t.author) ? "Orbis Web Manager" : t.author, 128),
                S("PUBTOOLINFO", "c_date=" + DateTime.Now.ToString("yyyyMMdd") + ",img0_l0_size=0,img0_l1_size=0,img0_sc_ksize=2048,img0_pc_ksize=576", 512),
                I("PUBTOOLVER", 0x02890000),
                S("TITLE", title, 128),
                S("TITLE_ID", titleId, 12),
                S("VERSION", "01.00", 8)
            }));

            // GP4 project for PkgTool
            var files = new StringBuilder();
            var dirs = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var f in Directory.GetFiles(build, "*", SearchOption.AllDirectories))
            {
                string rel = f.Substring(build.Length + 1).Replace('\\', '/');
                files.Append("    <file targ_path=\"").Append(rel).Append("\" orig_path=\"").Append(System.Security.SecurityElement.Escape(f)).Append("\" />\r\n");
                string d = Path.GetDirectoryName(rel.Replace('/', '\\'));
                while (!string.IsNullOrEmpty(d)) { dirs.Add(d.Replace('\\', '/')); d = Path.GetDirectoryName(d); }
            }
            var tree = new StringBuilder();
            Action<string, int> emit = null;
            emit = (parent, depth) =>
            {
                foreach (var d in dirs)
                {
                    string p = d.Contains("/") ? d.Substring(0, d.LastIndexOf('/')) : "";
                    if (p != parent) continue;
                    string nm = d.Contains("/") ? d.Substring(d.LastIndexOf('/') + 1) : d;
                    tree.Append(new string(' ', 4 + depth * 2)).Append("<dir targ_name=\"").Append(nm).Append("\">\r\n");
                    emit(d, depth + 1);
                    tree.Append(new string(' ', 4 + depth * 2)).Append("</dir>\r\n");
                }
            };
            emit("", 0);
            string gp4Path = Path.Combine(root, "theme.gp4");
            File.WriteAllText(gp4Path,
                "<?xml version=\"1.0\"?>\r\n<psproject fmt=\"gp4\" version=\"1000\">\r\n" +
                "  <volume>\r\n    <volume_type>pkg_ps4_theme</volume_type>\r\n    <volume_id>PS4VOLUME</volume_id>\r\n" +
                "    <volume_ts>" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "</volume_ts>\r\n" +
                "    <package content_id=\"" + t.contentId + "\" passcode=\"00000000000000000000000000000000\" storage_type=\"digital25\" app_type=\"full\" c_date=\"" + DateTime.Now.ToString("yyyy-MM-dd") + "\" />\r\n" +
                "  </volume>\r\n  <files img_no=\"0\">\r\n" + files + "  </files>\r\n  <rootdir>\r\n" + tree + "  </rootdir>\r\n</psproject>\r\n",
                new UTF8Encoding(false));

            string outDir = Path.Combine(root, "pkg");
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);
            var psi = new ProcessStartInfo(PkgToolPath, "pkg_build \"" + gp4Path + "\" \"" + outDir + "\"")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
            };
            string log;
            using (var p = Process.Start(psi))
            {
                log = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                if (!p.WaitForExit(180000)) { try { p.Kill(); } catch { } throw new Exception("O PkgTool demorou demais."); }
                if (p.ExitCode != 0) throw new Exception("O PkgTool falhou: " + log.Trim());
            }
            string pkg = null;
            foreach (var f in Directory.GetFiles(outDir, "*.pkg")) pkg = f;
            if (pkg == null) throw new Exception("O PkgTool não gerou o .pkg: " + log.Trim());
            string final = PkgPath(t);
            if (!string.Equals(pkg, final, StringComparison.OrdinalIgnoreCase)) File.Move(pkg, final);
            FinalizeThemePkg(final, hasBgm ? (string.IsNullOrEmpty(t.bgmLoud) ? "-23.00" : t.bgmLoud) : null);
            t.builtAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            t.pkgSize = new FileInfo(final).Length;
            Save(t);
            File.SetLastWriteTimeUtc(final, DateTime.UtcNow.AddSeconds(1));   // newer than theme.json: "up to date"
            Activity.Add("theme", "Tema gerado: " + t.name);
            return new { success = true, pkg = Path.GetFileName(final), size = t.pkgSize, contentId = t.contentId, log = log.Trim() };
        }

        /// <summary>
        /// LibOrbisPkg builds pkg_ps4_theme projects with iro_tag = None, so the console installs
        /// them as plain add-on content and they never show up in Settings › Themes
        /// (maxton/LibOrbisPkg issue #41). Set IRO_TAG = SS_THEME (2) at 0x98 and redo what depends
        /// on it: the header digest at 0xFE0 (SHA-256 of 0..0xFE0) and the header signature at
        /// 0x1000, signed with LibOrbisPkg's own fake-PKG key. The general digests only cover
        /// 0x00-0x40 and 0x400-0x480, so nothing else changes.
        /// </summary>
        static uint BE32(byte[] b, long p) { return (uint)(b[p] << 24 | b[p + 1] << 16 | b[p + 2] << 8 | b[p + 3]); }
        static ulong BE64(byte[] b, long p) { return ((ulong)BE32(b, p) << 32) | BE32(b, p + 4); }

        /// <summary>
        /// Finishes what LibOrbisPkg leaves out for themes (it only fully supports apps and DLC):
        ///  - IRO_TAG = SS_THEME at 0x98, or the console installs it as plain add-on content;
        ///  - when the theme has music, "bgmhome_loud=&lt;LKFS&gt;" in param.sfo's PUBTOOLINFO, which
        ///    Sony's publishing tool writes for themes with BGM (LibOrbisPkg rewrites that field).
        /// PUBTOOLINFO has a fixed 512-byte slot, so the package layout doesn't move; then every
        /// digest that depends on it is recomputed in the same order PkgBuilder uses: ParamDigest
        /// in GENERAL_DIGESTS, the per-entry DIGESTS table, body digest, digest-table hash, SC entry
        /// hashes 1/2, header digest (0xFE0) and the header signature (0x1000, LibOrbisPkg's key).
        /// </summary>
        static void FinalizeThemePkg(string pkgPath, string bgmLoud)
        {
            byte[] b = File.ReadAllBytes(pkgPath);
            if (b.Length < 0x2000 || b[0] != 0x7F || b[1] != (byte)'C') throw new Exception("PKG inválido.");
            using (var sha = SHA256.Create())
            {
                uint count = BE32(b, 0x10);
                int scCount = (b[0x14] << 8) | b[0x15];
                uint table = BE32(b, 0x18);
                ulong bodyOff = BE64(b, 0x20), bodySize = BE64(b, 0x28);
                var ids = new uint[count]; var offs = new uint[count]; var sizes = new uint[count];
                var at = new Dictionary<uint, int>();
                for (int i = 0; i < count; i++)
                {
                    long m = table + i * 32;
                    ids[i] = BE32(b, m); offs[i] = BE32(b, m + 16); sizes[i] = BE32(b, m + 20);
                    at[ids[i]] = i;
                }
                const uint DIGESTS = 0x1, ENTRY_KEYS = 0x10, IMAGE_KEY = 0x20, GENERAL_DIGESTS = 0x80, METAS = 0x100, PARAM_SFO = 0x1000;
                foreach (var need in new[] { DIGESTS, ENTRY_KEYS, IMAGE_KEY, GENERAL_DIGESTS, METAS, PARAM_SFO })
                    if (!at.ContainsKey(need)) throw new Exception("Entrada 0x" + need.ToString("X") + " não encontrada no PKG.");

                // 1) param.sfo: add bgmhome_loud to PUBTOOLINFO (same slot size, nothing else moves)
                int so = (int)offs[at[PARAM_SFO]];
                if (!string.IsNullOrEmpty(bgmLoud))
                {
                    int keyTab = so + BitConverter.ToInt32(b, so + 8), dataTab = so + BitConverter.ToInt32(b, so + 12), n = BitConverter.ToInt32(b, so + 16);
                    for (int i = 0; i < n; i++)
                    {
                        int e = so + 20 + i * 16;
                        int k = keyTab + BitConverter.ToUInt16(b, e);
                        int kl = 0; while (b[k + kl] != 0) kl++;
                        if (Encoding.ASCII.GetString(b, k, kl) != "PUBTOOLINFO") continue;
                        int len = BitConverter.ToInt32(b, e + 4), max = BitConverter.ToInt32(b, e + 8), d = dataTab + BitConverter.ToInt32(b, e + 12);
                        string cur = Encoding.UTF8.GetString(b, d, Math.Max(0, len - 1));
                        if (cur.Contains("bgmhome_loud=")) break;
                        int comma = cur.IndexOf(',');
                        string val = comma > 0 ? cur.Substring(0, comma + 1) + "bgmhome_loud=" + bgmLoud + cur.Substring(comma) : cur + ",bgmhome_loud=" + bgmLoud;
                        byte[] nv = Encoding.UTF8.GetBytes(val + "\0");
                        if (nv.Length > max) throw new Exception("PUBTOOLINFO não cabe no param.sfo.");
                        for (int z = 0; z < max; z++) b[d + z] = 0;
                        Buffer.BlockCopy(nv, 0, b, d, nv.Length);
                        BitConverter.GetBytes(nv.Length).CopyTo(b, e + 4);
                        break;
                    }
                }

                // 2) ParamDigest (GENERAL_DIGESTS + 0xC0) = SHA-256 of the whole param.sfo entry
                Buffer.BlockCopy(sha.ComputeHash(b, so, (int)sizes[at[PARAM_SFO]]), 0, b, (int)offs[at[GENERAL_DIGESTS]] + 0xC0, 32);

                // 3) per-entry digest table (index 0 is the DIGESTS entry itself)
                int dg = (int)offs[at[DIGESTS]];
                for (int i = 1; i < count; i++)
                    Buffer.BlockCopy(sha.ComputeHash(b, (int)offs[i], (int)sizes[i]), 0, b, dg + 32 * i, 32);

                // 4) header hashes
                Buffer.BlockCopy(sha.ComputeHash(b, (int)bodyOff, (int)bodySize), 0, b, 0x160, 32);              // body digest
                Buffer.BlockCopy(sha.ComputeHash(b, dg, (int)sizes[at[DIGESTS]]), 0, b, 0x140, 32);              // digest table hash
                using (var ms = new MemoryStream())
                {
                    foreach (var id in new[] { ENTRY_KEYS, IMAGE_KEY, GENERAL_DIGESTS, METAS, DIGESTS }) ms.Write(b, (int)offs[at[id]], (int)sizes[at[id]]);
                    Buffer.BlockCopy(sha.ComputeHash(ms.ToArray()), 0, b, 0x100, 32);                            // SC entries 1
                    ms.SetLength(0);
                    foreach (var id in new[] { ENTRY_KEYS, IMAGE_KEY, GENERAL_DIGESTS, METAS })
                        ms.Write(b, (int)offs[at[id]], id == METAS ? scCount * 0x20 : (int)sizes[at[id]]);
                    Buffer.BlockCopy(sha.ComputeHash(ms.ToArray()), 0, b, 0x120, 32);                            // SC entries 2
                }

                // 5) theme tag, header digest and signature
                b[0x98] = 0; b[0x99] = 0; b[0x9A] = 0; b[0x9B] = 2;
                Buffer.BlockCopy(sha.ComputeHash(b, 0, 0xFE0), 0, b, 0xFE0, 32);
                byte[] headerHash = sha.ComputeHash(b, 0, 0x1000);
                var asm = System.Reflection.Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(PkgToolPath), "LibOrbisPkg.dll"));
                var signKey = (byte[])asm.GetType("LibOrbisPkg.Util.Keys", true).GetProperty("PkgSignKey").GetValue(null, null);
                var rsa = asm.GetType("LibOrbisPkg.Util.Crypto", true).GetMethod("RSA2048EncryptKey", new[] { typeof(byte[]), typeof(byte[]) });
                var sig = (byte[])rsa.Invoke(null, new object[] { signKey, headerHash });
                Buffer.BlockCopy(sig, 0, b, 0x1000, sig.Length);
            }
            File.WriteAllBytes(pkgPath, b);
        }

        /// <summary>True when the settings or any source file changed after the last build.</summary>
        public static bool IsStale(string id)
        {
            string pkg = BuiltPkg(id);
            if (pkg == null) return true;
            DateTime built = File.GetLastWriteTimeUtc(pkg);
            string root = Dir(id);
            if (File.GetLastWriteTimeUtc(Path.Combine(root, "theme.json")) > built) return true;
            foreach (var f in Directory.GetFiles(Path.Combine(root, "source")))
                if (File.GetLastWriteTimeUtc(f) > built) return true;
            return false;
        }

        public static object Send(string id)
        {
            bool rebuilt = false;
            if (IsStale(id)) { Build(id); rebuilt = true; }   // never send a package older than the settings
            var t = Load(id);
            string pkg = BuiltPkg(id);
            if (pkg == null) throw new Exception("Gere o .pkg do tema primeiro.");
            string remote = "/data/pkg/" + Path.GetFileName(pkg);
            using (var fs = File.OpenRead(pkg)) Ftp.RunOnce("Enviando tema " + t.name, c => c.Store(remote, fs, null));
            Summary.Invalidate();
            Activity.Add("theme", "Tema enviado ao PS4: " + t.name);
            return new { success = true, path = remote, rebuilt = rebuilt, contentId = t.contentId };
        }
    }
}
