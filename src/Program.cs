using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace OrbisWebManager
{
    public class Config
    {
        public string ps4_ip { get; set; }
        public int ftp_port { get; set; }
        public int payload_port { get; set; }
        public int http_port { get; set; }
        public string ps4_fw { get; set; }
        public string active_user { get; set; }
        public string steamgriddb_api_key { get; set; }
        public string pc_pkg_folder { get; set; }

        public static Config Load(string path)
        {
            Config cfg = null;
            if (File.Exists(path))
            {
                try { cfg = new JavaScriptSerializer().Deserialize<Config>(File.ReadAllText(path)); }
                catch { cfg = null; }
            }
            if (cfg == null) cfg = new Config();
            if (string.IsNullOrEmpty(cfg.ps4_ip)) cfg.ps4_ip = "192.168.1.100";
            if (cfg.ftp_port <= 0) cfg.ftp_port = 2121;
            if (cfg.payload_port <= 0) cfg.payload_port = 9090;
            if (cfg.http_port <= 0) cfg.http_port = 5000;
            if (string.IsNullOrEmpty(cfg.ps4_fw)) cfg.ps4_fw = "9.00";
            if (cfg.active_user == null) cfg.active_user = "";
            if (cfg.steamgriddb_api_key == null) cfg.steamgriddb_api_key = "";
            if (cfg.pc_pkg_folder == null) cfg.pc_pkg_folder = "";
            cfg.Save(path);
            return cfg;
        }

        public void Save(string path)
        {
            File.WriteAllText(path, new JavaScriptSerializer().Serialize(this));
        }
    }

    class Program
    {
        public const string AppVersion = "2.0";
        public static Config Settings;
        public static string AppDir, CacheDir, CapturesDir, WwwDir, ConfigPath;
        public static bool LanAccess;

        public static DateTime ParseFtpDate(string dateStr)
        {
            if (string.IsNullOrEmpty(dateStr)) return DateTime.MinValue;
            string[] parts = dateStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return DateTime.MinValue;
            int year = DateTime.Now.Year, day = 1, month = 1, hour = 0, minute = 0;
            int.TryParse(parts[1], out day);
            if (parts[2].Contains(":"))
            {
                var hm = parts[2].Split(':');
                int.TryParse(hm[0], out hour);
                int.TryParse(hm[1], out minute);
            }
            else int.TryParse(parts[2], out year);
            string[] months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            for (int i = 0; i < months.Length; i++)
                if (parts[0].StartsWith(months[i], StringComparison.OrdinalIgnoreCase)) { month = i + 1; break; }
            try { return new DateTime(year, month, day, hour, minute, 0); } catch { return DateTime.MinValue; }
        }

        static void Main(string[] args)
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | SecurityProtocolType.Tls;
                ServicePointManager.DefaultConnectionLimit = 16;
            }
            catch { }
            ThreadPool.SetMinThreads(32, 32);

            System.Console.OutputEncoding = Encoding.UTF8;
            AppDir = AppDomain.CurrentDomain.BaseDirectory;
            CacheDir = Path.Combine(AppDir, "cache");
            CapturesDir = Path.Combine(CacheDir, "captures");
            WwwDir = Path.Combine(AppDir, "wwwroot");
            ConfigPath = Path.Combine(AppDir, "config.json");
            foreach (var d in new[] { CacheDir, CapturesDir, Path.Combine(CapturesDir, "thumbs"), Covers.IconsDir, Covers.OriginalsDir })
                Directory.CreateDirectory(d);

            Settings = Config.Load(ConfigPath);

            System.Console.ForegroundColor = ConsoleColor.Cyan;
            System.Console.WriteLine();
            System.Console.WriteLine("   ORBIS WEB MANAGER v" + AppVersion + "  -  PlayStation 4 Web Suite (GoldHEN)");
            System.Console.WriteLine();
            System.Console.ResetColor();

            Library.Load();
            // Warm up caches in the background so the first page load is fast
            ThreadPool.QueueUserWorkItem(_ => { try { Library.Refresh(); } catch { } });
            ThreadPool.QueueUserWorkItem(_ => { try { Summary.Get(false); } catch { } });

            StartHttpServer();
        }

        static void StartHttpServer()
        {
            int port = Settings.http_port;
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://+:" + port + "/");
            try
            {
                listener.Start();
                LanAccess = true;
            }
            catch (HttpListenerException)
            {
                // Listening on all interfaces requires a URL ACL (one-time admin step).
                listener = new HttpListener();
                listener.Prefixes.Add("http://localhost:" + port + "/");
                listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
                try
                {
                    listener.Start();
                }
                catch (Exception ex)
                {
                    System.Console.ForegroundColor = ConsoleColor.Red;
                    System.Console.WriteLine("[ERRO] Não foi possível abrir a porta " + port + ": " + ex.Message);
                    System.Console.WriteLine("       Outra cópia do Orbis Web Manager já está aberta?");
                    System.Console.ResetColor();
                    return;
                }
                LanAccess = false;
            }

            System.Console.ForegroundColor = ConsoleColor.Green;
            System.Console.WriteLine("[+] Servidor iniciado");
            System.Console.WriteLine("    -> No PC:      http://localhost:" + port);
            if (LanAccess)
            {
                foreach (var ip in Ps4Services.LocalIps()) System.Console.WriteLine("    -> No celular: http://" + ip + ":" + port);
            }
            System.Console.WriteLine("    -> PS4:        " + Settings.ps4_ip + " (FTP " + Settings.ftp_port + ")");
            System.Console.ResetColor();
            if (!LanAccess)
            {
                System.Console.ForegroundColor = ConsoleColor.Yellow;
                System.Console.WriteLine();
                System.Console.WriteLine("[!] Acesso pela rede (celular / instalar PKG do PC) está DESATIVADO.");
                System.Console.WriteLine("    Para liberar, execute UMA vez o arquivo 'liberar-rede.bat' (pede permissão de administrador).");
                System.Console.ResetColor();
            }
            System.Console.WriteLine();
            System.Console.WriteLine("Pressione Ctrl+C para encerrar.");

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--no-browser") < 0)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    Thread.Sleep(800);
                    try { Process.Start("http://localhost:" + port); } catch { }
                });
            }

            while (listener.IsListening)
            {
                try
                {
                    var ctx = listener.GetContext();
                    ThreadPool.QueueUserWorkItem(ProcessRequest, ctx);
                }
                catch (HttpListenerException) { break; }
                catch { }
            }
        }

        static void ProcessRequest(object state)
        {
            var ctx = (HttpListenerContext)state;
            string path = ctx.Request.Url.AbsolutePath;
            try
            {
                if (path.StartsWith("/api/")) Api.Handle(ctx);
                else if (path.StartsWith("/pcpkg/")) Api.ServePcPkg(ctx);
                else ServeStatic(ctx, path);
            }
            catch (Exception ex)
            {
                try
                {
                    Http.Error(ctx.Response, 500, ex is FtpException || ex is IOException
                        ? "Falha na comunicação com o PS4: " + ex.Message
                        : ex.Message);
                }
                catch { }
            }
        }

        static void ServeStatic(HttpListenerContext ctx, string path)
        {
            if (path == "/" || path.Length == 0) path = "/index.html";
            string root = Path.GetFullPath(WwwDir);
            string local;
            try { local = Path.GetFullPath(Path.Combine(root, Uri.UnescapeDataString(path).TrimStart('/').Replace('/', Path.DirectorySeparatorChar))); }
            catch { local = ""; }
            // Prevent path traversal outside wwwroot
            if (!local.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(local))
                local = Path.Combine(root, "index.html");

            string ext = Path.GetExtension(local).ToLowerInvariant();
            string type = "application/octet-stream";
            switch (ext)
            {
                case ".html": type = "text/html; charset=utf-8"; break;
                case ".css": type = "text/css; charset=utf-8"; break;
                case ".js": type = "application/javascript; charset=utf-8"; break;
                case ".json": type = "application/json; charset=utf-8"; break;
                case ".png": type = "image/png"; break;
                case ".jpg": case ".jpeg": type = "image/jpeg"; break;
                case ".svg": type = "image/svg+xml"; break;
                case ".ico": type = "image/x-icon"; break;
            }
            ctx.Response.Headers["Cache-Control"] = "no-cache";
            Http.Bytes(ctx.Response, File.ReadAllBytes(local), type);
        }
    }

    static class Http
    {
        public static void Json(HttpListenerResponse res, object data)
        {
            var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            Bytes(res, Encoding.UTF8.GetBytes(ser.Serialize(data)), "application/json; charset=utf-8");
        }

        public static void Error(HttpListenerResponse res, int code, string msg)
        {
            res.StatusCode = code;
            Json(res, new { success = false, error = msg });
        }

        public static void Bytes(HttpListenerResponse res, byte[] b, string type)
        {
            res.ContentType = type;
            res.ContentLength64 = b.Length;
            res.OutputStream.Write(b, 0, b.Length);
            res.Close();
        }

        public static Dictionary<string, object> ReadJson(HttpListenerRequest req)
        {
            using (var r = new StreamReader(req.InputStream, Encoding.UTF8))
            {
                string body = r.ReadToEnd();
                if (string.IsNullOrWhiteSpace(body)) return new Dictionary<string, object>();
                var d = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<Dictionary<string, object>>(body);
                return d ?? new Dictionary<string, object>();
            }
        }

        public static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) && v != null ? v.ToString() : "";
        }

        public static byte[] ReadBody(HttpListenerRequest req, long max)
        {
            using (var ms = new MemoryStream())
            {
                byte[] buf = new byte[65536];
                int n;
                while ((n = req.InputStream.Read(buf, 0, buf.Length)) > 0)
                {
                    ms.Write(buf, 0, n);
                    if (ms.Length > max) throw new Exception("Arquivo muito grande.");
                }
                return ms.ToArray();
            }
        }

        public static string ContentDisposition(string fileName)
        {
            string ascii = Regex.Replace(fileName, @"[^\x20-\x7E]|""", "_");
            return "attachment; filename=\"" + ascii + "\"; filename*=UTF-8''" + Uri.EscapeDataString(fileName);
        }
    }

    static class Api
    {
        static readonly Regex TitleIdRx = new Regex(@"^[A-Z]{4}\d{5}$");
        static readonly Dictionary<string, KeyValuePair<DateTime, object>> captureCache = new Dictionary<string, KeyValuePair<DateTime, object>>();
        static object statusCache;
        static DateTime statusAt = DateTime.MinValue;
        static readonly object statusLock = new object();

        public static void Handle(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var res = ctx.Response;
            string path = req.Url.AbsolutePath.TrimEnd('/');
            var q = req.QueryString;
            bool post = req.HttpMethod == "POST";

            // Mutating requests must come from our own page (blocks drive-by CSRF from other sites)
            if (post)
            {
                string origin = req.Headers["Origin"];
                if (!string.IsNullOrEmpty(origin))
                {
                    Uri o;
                    if (!Uri.TryCreate(origin, UriKind.Absolute, out o) || o.Authority != req.Url.Authority)
                    {
                        Http.Error(res, 403, "Origem não permitida.");
                        return;
                    }
                }
            }

            switch (path)
            {
                // ------------------------------------------------ status / dashboard
                case "/api/status": Status(res); return;
                case "/api/summary":
                    {
                        var s = Summary.Get(q["force"] == "1");
                        if (s == null) Http.Json(res, new { username = "PlayStation 4", userId = "", users = new object[0], cheatCount = 0, pkgCount = 0, totalPkgSize = 0 });
                        else Http.Json(res, s);
                        return;
                    }
                case "/api/ports": Http.Json(res, Ps4Services.Diagnose()); return;

                // ------------------------------------------------ games
                case "/api/games":
                    {
                        if (q["deep"] == "1") { Library.ClearTitleCache(); Covers.ForgetMissing(); }
                        var snap = Library.Get(q["refresh"] == "1" || q["deep"] == "1");
                        Http.Json(res, new
                        {
                            games = snap.games,
                            updated = snap.updated == DateTime.MinValue ? "" : snap.updated.ToLocalTime().ToString("HH:mm:ss"),
                            refreshing = Library.Refreshing,
                            error = Library.LastError
                        });
                        return;
                    }
                case "/api/game/icon": GameIcon(ctx); return;
                case "/api/game/details": GameDetails(res, Covers.SafeId(q["id"])); return;
                case "/api/game/icon/upload":
                    if (!post) break;
                    {
                        string id = Covers.SafeId(q["id"]);
                        if (!TitleIdRx.IsMatch(id)) { Http.Error(res, 400, "Title ID inválido."); return; }
                        byte[] img = Http.ReadBody(req, 30 * 1024 * 1024);
                        Covers.Apply(id, img, q["target"] ?? "icon", q["fit"] ?? "cover");
                        Library.MarkCoverChanged(id);
                        Http.Json(res, new { success = true, iconUrl = Library.IconUrl(id) });
                        return;
                    }

                // ------------------------------------------------ covers
                case "/api/covers/search": CoverSearch(res, Covers.SafeId(q["id"]), q["title"] ?? ""); return;
                case "/api/covers/sgdb":
                    {
                        int gid;
                        int.TryParse(q["gameId"], out gid);
                        if (gid <= 0 || !Covers.HasSgdbKey) { Http.Json(res, new { grids = new object[0] }); return; }
                        Http.Json(res, new { grids = Covers.SgdbArt(gid, q["kind"] ?? "square") });
                        return;
                    }
                case "/api/covers/apply":
                    if (!post) break;
                    {
                        var d = Http.ReadJson(req);
                        string id = Covers.SafeId(Http.Str(d, "id"));
                        string url = Http.Str(d, "url");
                        if (!TitleIdRx.IsMatch(id) || url.Length == 0) { Http.Error(res, 400, "Parâmetros 'id' e 'url' são obrigatórios."); return; }
                        byte[] img = Covers.HttpGetBytes(url);
                        string target = Http.Str(d, "target");
                        Covers.Apply(id, img, target == "background" ? "background" : "icon", Http.Str(d, "fit") == "contain" ? "contain" : "cover");
                        Library.MarkCoverChanged(id);
                        Http.Json(res, new { success = true, iconUrl = Library.IconUrl(id) });
                        return;
                    }
                case "/api/covers/restore":
                    if (!post) break;
                    {
                        var d = Http.ReadJson(req);
                        string id = Covers.SafeId(Http.Str(d, "id"));
                        bool ok = Covers.Restore(id, Http.Str(d, "target") == "background" ? "background" : "icon");
                        Library.MarkCoverChanged(id);
                        if (!ok) { Http.Error(res, 404, "Nenhum backup da arte original encontrado para " + id + "."); return; }
                        Http.Json(res, new { success = true, iconUrl = Library.IconUrl(id) });
                        return;
                    }
                case "/api/covers/preview":
                    if (!post) break;
                    {
                        // Renders exactly what will be sent to the console (512x512 or 1920x1080)
                        var d = Http.ReadJson(req);
                        byte[] img = Covers.HttpGetBytes(Http.Str(d, "url"));
                        bool bg = Http.Str(d, "target") == "background";
                        using (var src = Covers.LoadImage(img))
                        using (var bmp = Covers.Render(src, bg ? 960 : 512, bg ? 540 : 512, Http.Str(d, "fit") == "contain" ? "contain" : "cover"))
                        using (var ms = new MemoryStream())
                        {
                            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                            Http.Bytes(res, ms.ToArray(), "image/jpeg");
                        }
                        return;
                    }

                // ------------------------------------------------ captures
                case "/api/captures/games": CaptureGames(res); return;
                case "/api/captures": Captures(res, Covers.SafeId(q["game"]), q["refresh"] == "1"); return;
                case "/api/capture/image": CaptureImage(ctx, q["path"], q["thumb"] == "1"); return;

                // ------------------------------------------------ files
                case "/api/files":
                    {
                        string dir = NormalizeDir(q["path"]);
                        Http.Json(res, new { currentPath = dir, items = Ftp.List(dir) });
                        return;
                    }
                case "/api/files/download": Download(ctx, q["path"]); return;
                case "/api/files/upload":
                    if (!post) break;
                    {
                        string dir = NormalizeDir(q["path"]);
                        string name = q["filename"] ?? "";
                        if (name.Length == 0 || name.IndexOfAny(new[] { '/', '\\' }) >= 0) { Http.Error(res, 400, "Nome de arquivo inválido."); return; }
                        string remote = dir + name;
                        Ftp.RunOnce("Enviando " + name, c => c.Store(remote, req.InputStream, null));
                        Summary.Invalidate();
                        Http.Json(res, new { success = true, path = remote });
                        return;
                    }
                case "/api/files/delete":
                    if (!post) break;
                    {
                        string target = q["path"] ?? "";
                        if (target.Length < 2) { Http.Error(res, 400, "Caminho inválido."); return; }
                        bool isDir = q["isDir"] == "true";
                        try
                        {
                            Ftp.Run(c => c.Simple((isDir ? "RMD " : "DELE ") + target, 250, 200));
                        }
                        catch (FtpException ex)
                        {
                            Http.Error(res, 400, isDir ? "Não foi possível remover a pasta (ela precisa estar vazia)." : "Falha ao excluir: " + ex.Message);
                            return;
                        }
                        Summary.Invalidate();
                        Http.Json(res, new { success = true });
                        return;
                    }
                case "/api/files/mkdir":
                    if (!post) break;
                    {
                        string target = q["path"] ?? "";
                        Ftp.Run(c => c.Simple("MKD " + target, 257, 250));
                        Http.Json(res, new { success = true });
                        return;
                    }
                case "/api/files/rename":
                    if (!post) break;
                    {
                        string from = q["from"] ?? "", to = q["to"] ?? "";
                        if (from.Length < 2 || to.Length < 2) { Http.Error(res, 400, "Caminho inválido."); return; }
                        Ftp.Run(c => { c.Simple("RNFR " + from, 350); c.Simple("RNTO " + to, 250); });
                        Http.Json(res, new { success = true });
                        return;
                    }

                // ------------------------------------------------ cheats
                case "/api/cheats": Cheats(res); return;
                case "/api/cheat/read":
                    {
                        string p = q["path"] ?? "";
                        string content = Ftp.TryDownloadText(p) ?? "";
                        object parsed = null;
                        try { parsed = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(content); } catch { }
                        Http.Json(res, new { raw = content, data = parsed });
                        return;
                    }
                case "/api/cheat/convert":
                    if (!post) break;
                    {
                        var d = Http.ReadJson(req);
                        string sourceId = Http.Str(d, "sourceId").Trim().ToUpperInvariant();
                        string targetId = Http.Str(d, "targetId").Trim().ToUpperInvariant();
                        string version = Http.Str(d, "version").Trim();
                        string raw = Http.Str(d, "json");
                        if (!TitleIdRx.IsMatch(sourceId) || !TitleIdRx.IsMatch(targetId)) { Http.Error(res, 400, "Title IDs inválidos (formato CUSA00000)."); return; }
                        if (!Regex.IsMatch(version, @"^\d{2}\.\d{2}$")) { Http.Error(res, 400, "Versão inválida (formato 01.00)."); return; }
                        try { new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(raw); }
                        catch { Http.Error(res, 400, "O arquivo selecionado não é um JSON válido."); return; }
                        string adapted = Regex.Replace(raw, Regex.Escape(sourceId), targetId, RegexOptions.IgnoreCase);
                        string fileName = targetId + "_" + version + ".json";
                        string remote = "/data/GoldHEN/cheats/json/" + fileName;
                        Ftp.Upload(remote, Encoding.UTF8.GetBytes(adapted));
                        Summary.Invalidate();
                        Http.Json(res, new { success = true, filename = fileName, path = remote });
                        return;
                    }

                // ------------------------------------------------ pkgs
                case "/api/pkgs":
                    {
                        var list = new List<object>();
                        foreach (var f in Ftp.TryList("/data/pkg/"))
                            if (!f.isDir && f.name.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase))
                                list.Add(new { name = f.name, size = f.size, date = f.date, path = "/data/pkg/" + f.name });
                        Http.Json(res, list);
                        return;
                    }
                case "/api/pc/pkgs": PcPkgs(res); return;

                // ------------------------------------------------ console downloads (PSN / RPI)
                case "/api/downloads": Http.Json(res, Downloads.Get()); return;
                case "/api/downloads/icon":
                    {
                        string task = q["task"] ?? "";
                        byte[] png = Downloads.IsTaskId(task) ? Downloads.Icon(task) : null;
                        if (png == null)
                        {
                            string id = Covers.SafeId(q["id"]);
                            res.Redirect("/api/game/icon?id=" + Uri.EscapeDataString(id));
                            res.Close();
                            return;
                        }
                        res.AddHeader("Cache-Control", "max-age=3600");
                        Http.Bytes(res, png, "image/png");
                        return;
                    }

                // ------------------------------------------------ console services
                case "/api/payload":
                    if (!post) break;
                    {
                        byte[] payload = Http.ReadBody(req, 64 * 1024 * 1024);
                        if (payload.Length == 0) { Http.Error(res, 400, "Arquivo vazio."); return; }
                        try
                        {
                            Ps4Services.SendPayload(payload);
                            Http.Json(res, new { success = true, message = "Payload enviado (" + payload.Length + " bytes) para a porta " + Program.Settings.payload_port + "." });
                        }
                        catch (Exception ex) { Http.Error(res, 502, ex.Message); }
                        return;
                    }
                case "/api/system/notify":
                    if (!post) break;
                    {
                        string msg = Http.Str(Http.ReadJson(req), "message").Trim();
                        if (msg.Length == 0) { Http.Error(res, 400, "Digite uma mensagem."); return; }
                        if (msg.Length > 400) msg = msg.Substring(0, 400);
                        try
                        {
                            Ps4Services.Notify(msg);
                            Http.Json(res, new { success = true });
                        }
                        catch (Exception ex)
                        {
                            Http.Json(res, new
                            {
                                success = false,
                                needsPs4Debug = ex.Message == "NO_PS4DEBUG",
                                error = ex.Message == "NO_PS4DEBUG"
                                    ? "O ps4debug não está rodando no PS4 (porta 744 fechada). Carregue o payload ps4debug e tente de novo."
                                    : "Falha ao enviar: " + ex.Message
                            });
                        }
                        return;
                    }
                case "/api/system/ps4debug":
                    if (!post) break;
                    try { Http.Json(res, new { success = true, message = Ps4Services.LoadPs4Debug() }); }
                    catch (Exception ex) { Http.Error(res, 409, ex.Message); }
                    return;
                case "/api/system/rpi":
                    if (!post) break;
                    {
                        var d = Http.ReadJson(req);
                        string url = Http.Str(d, "url");
                        string file = Http.Str(d, "file");
                        if (file.Length > 0)
                        {
                            if (!LanCheck(res)) return;
                            url = "http://" + Ps4Services.LocalIpTowardsConsole() + ":" + Program.Settings.http_port + "/pcpkg/" + Uri.EscapeDataString(file);
                        }
                        if (!url.StartsWith("http://") && !url.StartsWith("https://")) { Http.Error(res, 400, "Informe uma URL http:// válida para o .pkg."); return; }
                        try
                        {
                            string r = Ps4Services.RpiCall("install", new { type = "direct", packages = new[] { url } });
                            var parsed = new JavaScriptSerializer().DeserializeObject(r) as Dictionary<string, object>;
                            bool ok = parsed != null && parsed.ContainsKey("status") && Convert.ToString(parsed["status"]) == "success";
                            Http.Json(res, new
                            {
                                success = ok,
                                taskId = parsed != null && parsed.ContainsKey("task_id") ? parsed["task_id"] : null,
                                title = parsed != null && parsed.ContainsKey("title") ? parsed["title"] : null,
                                url = url,
                                error = ok ? null : "O Package Installer recusou: " + r
                            });
                        }
                        catch (Exception ex) { Http.Error(res, 502, ex.Message); }
                        return;
                    }
                case "/api/system/rpi/progress":
                    {
                        long task;
                        long.TryParse(q["task"], out task);
                        try { Http.Bytes(res, Encoding.UTF8.GetBytes(Ps4Services.RpiCall("get_task_progress", new { task_id = task })), "application/json; charset=utf-8"); }
                        catch (Exception ex) { Http.Error(res, 502, ex.Message); }
                        return;
                    }

                // ------------------------------------------------ settings / misc
                case "/api/config":
                    {
                        if (post)
                        {
                            var d = Http.ReadJson(req);
                            string oldIp = Program.Settings.ps4_ip;
                            int oldPort = Program.Settings.ftp_port;
                            string ip = Http.Str(d, "ps4_ip").Trim();
                            IPAddress parsedIp;
                            if (ip.Length > 0)
                            {
                                if (!IPAddress.TryParse(ip, out parsedIp)) { Http.Error(res, 400, "IP inválido."); return; }
                                Program.Settings.ps4_ip = ip;
                            }
                            int n;
                            if (int.TryParse(Http.Str(d, "ftp_port"), out n) && n > 0 && n < 65536) Program.Settings.ftp_port = n;
                            if (int.TryParse(Http.Str(d, "payload_port"), out n) && n > 0 && n < 65536) Program.Settings.payload_port = n;
                            if (d.ContainsKey("ps4_fw") && Http.Str(d, "ps4_fw").Length > 0) Program.Settings.ps4_fw = Http.Str(d, "ps4_fw").Trim();
                            if (d.ContainsKey("active_user")) Program.Settings.active_user = Http.Str(d, "active_user");
                            if (d.ContainsKey("steamgriddb_api_key")) Program.Settings.steamgriddb_api_key = Http.Str(d, "steamgriddb_api_key").Trim();
                            if (d.ContainsKey("pc_pkg_folder")) Program.Settings.pc_pkg_folder = Http.Str(d, "pc_pkg_folder").Trim().Trim('"');
                            Program.Settings.Save(Program.ConfigPath);
                            if (oldIp != Program.Settings.ps4_ip || oldPort != Program.Settings.ftp_port)
                            {
                                Ftp.Reset();
                                Library.RefreshAsync();
                            }
                            Summary.Invalidate();
                            lock (statusLock) statusAt = DateTime.MinValue;
                        }
                        var s = Program.Settings;
                        Http.Json(res, new
                        {
                            ps4_ip = s.ps4_ip,
                            ftp_port = s.ftp_port,
                            payload_port = s.payload_port,
                            http_port = s.http_port,
                            ps4_fw = s.ps4_fw,
                            active_user = s.active_user,
                            pc_pkg_folder = s.pc_pkg_folder,
                            // never send the key back to the browser, only whether it is set
                            has_sgdb_key = Covers.HasSgdbKey
                        });
                        return;
                    }
                case "/api/network":
                    {
                        var ips = Ps4Services.LocalIps();
                        Http.Json(res, new { pcIps = ips, httpPort = Program.Settings.http_port, ps4Ip = Program.Settings.ps4_ip, lanAccess = Program.LanAccess });
                        return;
                    }
                case "/api/backup/appdb":
                    {
                        res.SendChunked = true;
                        res.ContentType = "application/octet-stream";
                        res.Headers["Content-Disposition"] = Http.ContentDisposition("app_db_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".db");
                        Ftp.RunOnce("Backup app.db", c => c.Retrieve("/system_data/priv/mms/app.db", res.OutputStream, 0));
                        res.Close();
                        return;
                    }
            }

            Http.Error(res, 404, "Endpoint desconhecido: " + path);
        }

        // ================================================================ handlers

        static void Status(HttpListenerResponse res)
        {
            object cached = null;
            lock (statusLock)
            {
                if ((DateTime.UtcNow - statusAt).TotalSeconds < 3) cached = statusCache;
            }
            if (cached != null) { Http.Json(res, cached); return; }

            long ping = Ps4Services.Ping(700);
            bool? ftp = Ftp.TryPing(1500);
            bool ftpOk = ftp != false; // null == busy, which means it is up
            string banner = Ftp.LastBanner ?? "";
            string hen = "GoldHEN";
            var m = Regex.Match(banner, @"GoldHEN[^\r\n]*?v?(\d+(\.\d+)+\w*)", RegexOptions.IgnoreCase);
            if (m.Success) hen = "GoldHEN v" + m.Groups[1].Value;

            var s = new
            {
                online = ping >= 0 || ftpOk,
                ping = ping,
                ip = Program.Settings.ps4_ip,
                ftpPort = Program.Settings.ftp_port,
                ftpOk = ftpOk,
                ftpBusy = ftp == null,
                transfer = Ftp.CurrentTransfer,
                banner = banner,
                goldhenVer = hen,
                fw = Program.Settings.ps4_fw,
                username = string.IsNullOrEmpty(Summary.ActiveUserName) ? "PlayStation 4" : Summary.ActiveUserName,
                lanAccess = Program.LanAccess,
                version = Program.AppVersion
            };
            lock (statusLock) { statusCache = s; statusAt = DateTime.UtcNow; }
            Http.Json(res, s);
        }

        static void GameIcon(HttpListenerContext ctx)
        {
            string id = Covers.SafeId(ctx.Request.QueryString["id"]);
            string file = Covers.GetIcon(id);
            if (file == null)
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.Close();
                return;
            }
            byte[] b = File.ReadAllBytes(file);
            // URL carries ?v=<timestamp>, so the browser may cache it for a long time
            ctx.Response.Headers["Cache-Control"] = "public, max-age=604800";
            Http.Bytes(ctx.Response, b, Covers.SniffImageType(b));
        }

        static void GameDetails(HttpListenerResponse res, string id)
        {
            if (!TitleIdRx.IsMatch(id)) { Http.Error(res, 400, "Title ID inválido."); return; }
            var meta = Library.MetaOf(id);
            long appSize = 0, patchSize = 0;
            foreach (var f in Ftp.TryList("/user/app/" + id + "/"))
                if (f.name.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase)) appSize += f.size;
            foreach (var f in Ftp.TryList("/user/patch/" + id + "/"))
                if (f.name.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase)) patchSize += f.size;
            var cheats = new List<string>();
            foreach (var f in Ftp.TryList("/data/GoldHEN/cheats/json/"))
                if (f.name.StartsWith(id, StringComparison.OrdinalIgnoreCase)) cheats.Add(f.name);
            var appmeta = Ftp.TryList("/user/appmeta/" + id + "/");
            bool hasPic1 = appmeta.Exists(f => f.name == "pic1.png");
            Http.Json(res, new
            {
                id = id,
                name = Library.TitleOf(id),
                version = meta != null ? meta.version : "",
                contentId = meta != null ? meta.contentId : "",
                category = meta != null ? meta.category : "",
                appSize = appSize,
                patchSize = patchSize,
                cheats = cheats,
                hasPic1 = hasPic1,
                customCover = Covers.HasOriginalBackup(id),
                iconUrl = Library.IconUrl(id)
            });
        }

        static void CoverSearch(HttpListenerResponse res, string id, string title)
        {
            object sony = null;
            if (id.StartsWith("CUSA"))
            {
                var si = Covers.SonyInfo(id);
                if (si != null && !string.IsNullOrEmpty(si.icon))
                    sony = new { icon = si.icon, background = si.background, name = si.name };
            }

            List<object> games = new List<object>();
            List<object> grids = new List<object>();
            object selected = null;
            string sgdbError = null;
            if (Covers.HasSgdbKey)
            {
                try
                {
                    games = Covers.SgdbSearch(string.IsNullOrWhiteSpace(title) ? Library.TitleOf(id) : title);
                    if (games.Count > 0)
                    {
                        dynamicId(games[0], out selected);
                        grids = Covers.SgdbArt((int)selected, "square");
                        if (grids.Count == 0) grids = Covers.SgdbArt((int)selected, "all");
                    }
                }
                catch (Exception ex) { sgdbError = ex.Message; }
            }

            Http.Json(res, new
            {
                id = id,
                title = title,
                sony = sony,
                steamgriddb = new { hasKey = Covers.HasSgdbKey, games = games, selectedGameId = selected, grids = grids, error = sgdbError }
            });
        }

        // Anonymous types from SgdbSearch: read their "id" via reflection
        static void dynamicId(object game, out object id)
        {
            id = Convert.ToInt32(game.GetType().GetProperty("id").GetValue(game, null));
        }

        static void CaptureGames(HttpListenerResponse res)
        {
            var list = new List<object>();
            foreach (var f in Ftp.TryList("/user/av_contents/photo/NPXS20001/"))
            {
                if (!f.isDir) continue;
                string gid = f.name.ToUpperInvariant();
                list.Add(new { id = gid, name = Library.TitleOf(gid), iconUrl = Library.IconUrl(gid), date = f.date, sort = Program.ParseFtpDate(f.date).Ticks });
            }
            // most recent first
            list.Sort((a, b) => ((long)b.GetType().GetProperty("sort").GetValue(b, null)).CompareTo((long)a.GetType().GetProperty("sort").GetValue(a, null)));
            Http.Json(res, list);
        }

        static void Captures(HttpListenerResponse res, string game, bool refresh)
        {
            if (game.Length == 0) { Http.Json(res, new object[0]); return; }
            lock (captureCache)
            {
                KeyValuePair<DateTime, object> c;
                if (!refresh && captureCache.TryGetValue(game, out c) && (DateTime.UtcNow - c.Key).TotalMinutes < 3)
                {
                    Http.Json(res, c.Value);
                    return;
                }
            }
            string baseDir = "/user/av_contents/photo/NPXS20001/" + game + "/";
            var subs = Ftp.TryList(baseDir).FindAll(f => f.isDir);
            subs.Sort((a, b) => Program.ParseFtpDate(b.date).CompareTo(Program.ParseFtpDate(a.date)));
            if (subs.Count > 40) subs = subs.GetRange(0, 40);

            var items = new List<object>();
            foreach (var sf in subs)
            {
                string dir = baseDir + sf.name + "/";
                foreach (var file in Ftp.TryList(dir))
                {
                    if (!file.name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)) continue;
                    string full = dir + file.name;
                    string enc = Uri.EscapeDataString(full);
                    items.Add(new
                    {
                        titleId = game,
                        fileName = file.name,
                        fullPath = full,
                        size = file.size,
                        imageUrl = "/api/capture/image?path=" + enc,
                        thumbUrl = "/api/capture/image?thumb=1&path=" + enc
                    });
                }
            }
            // File names start with yyyyMMdd_HHmmss -> sort newest first
            items.Sort((a, b) => string.CompareOrdinal(
                (string)b.GetType().GetProperty("fileName").GetValue(b, null),
                (string)a.GetType().GetProperty("fileName").GetValue(a, null)));
            lock (captureCache) captureCache[game] = new KeyValuePair<DateTime, object>(DateTime.UtcNow, items);
            Http.Json(res, items);
        }

        static string Hash(string s)
        {
            using (var md5 = MD5.Create())
                return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-", "").Substring(0, 16);
        }

        static void CaptureImage(HttpListenerContext ctx, string shotPath, bool thumb)
        {
            if (string.IsNullOrEmpty(shotPath) || !shotPath.StartsWith("/user/av_contents/") || shotPath.Contains(".."))
            {
                Http.Error(ctx.Response, 400, "Caminho inválido.");
                return;
            }
            string key = Hash(shotPath);
            string full = Path.Combine(Program.CapturesDir, Path.GetFileNameWithoutExtension(shotPath) + "_" + key + ".jpg");
            if (!File.Exists(full)) Ftp.DownloadToFile(shotPath, full);
            string file = full;
            if (thumb)
            {
                string t = Path.Combine(Program.CapturesDir, "thumbs", key + ".jpg");
                if (!File.Exists(t))
                {
                    try { File.WriteAllBytes(t, Covers.Thumbnail(File.ReadAllBytes(full), 480)); }
                    catch { t = full; }
                }
                file = t;
            }
            ctx.Response.Headers["Cache-Control"] = "public, max-age=2592000";
            Http.Bytes(ctx.Response, File.ReadAllBytes(file), "image/jpeg");
        }

        static string NormalizeDir(string dir)
        {
            if (string.IsNullOrEmpty(dir)) dir = "/data/";
            if (!dir.StartsWith("/")) dir = "/" + dir;
            if (!dir.EndsWith("/")) dir += "/";
            return dir;
        }

        static void Download(HttpListenerContext ctx, string filePath)
        {
            var res = ctx.Response;
            if (string.IsNullOrEmpty(filePath)) { Http.Error(res, 400, "Caminho obrigatório."); return; }
            string name = filePath.Substring(filePath.LastIndexOf('/') + 1);
            Ftp.RunOnce("Baixando " + name, c =>
            {
                long size = c.Size(filePath);
                res.ContentType = "application/octet-stream";
                res.Headers["Content-Disposition"] = Http.ContentDisposition(name);
                if (size >= 0) res.ContentLength64 = size; else res.SendChunked = true;
                c.Retrieve(filePath, res.OutputStream, 0);
            });
            res.Close();
        }

        static void Cheats(HttpListenerResponse res)
        {
            var list = new List<object>();
            foreach (var f in Ftp.List("/data/GoldHEN/cheats/json/"))
            {
                if (f.isDir || !f.name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                var m = Regex.Match(f.name, @"^([A-Z]{4}\d{5})_([0-9\.]+)\.json$", RegexOptions.IgnoreCase);
                string tid = m.Success ? m.Groups[1].Value.ToUpperInvariant() : "";
                list.Add(new
                {
                    filename = f.name,
                    path = "/data/GoldHEN/cheats/json/" + f.name,
                    titleId = tid,
                    version = m.Success ? m.Groups[2].Value : "",
                    gameName = tid.Length > 0 ? Library.TitleOf(tid) : f.name,
                    installed = tid.Length > 0 && Library.MetaOf(tid) != null,
                    size = f.size,
                    date = f.date
                });
            }
            Http.Json(res, list);
        }

        static bool LanCheck(HttpListenerResponse res)
        {
            if (Program.LanAccess) return true;
            Http.Error(res, 409, "O PS4 não consegue baixar do PC porque o acesso pela rede está desativado. Feche o programa, execute 'liberar-rede.bat' uma vez e abra de novo.");
            return false;
        }

        static void PcPkgs(HttpListenerResponse res)
        {
            string folder = Program.Settings.pc_pkg_folder;
            var list = new List<object>();
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                foreach (var f in new DirectoryInfo(folder).GetFiles("*.pkg"))
                    list.Add(new { name = f.Name, size = f.Length, date = f.LastWriteTime.ToString("dd/MM/yyyy HH:mm") });
            }
            Http.Json(res, new
            {
                folder = folder,
                exists = !string.IsNullOrEmpty(folder) && Directory.Exists(folder),
                lanAccess = Program.LanAccess,
                serverUrl = "http://" + Ps4Services.LocalIpTowardsConsole() + ":" + Program.Settings.http_port,
                files = list
            });
        }

        /// <summary>Serves a .pkg from the configured PC folder with HTTP Range support,
        /// which the Remote Package Installer needs to download it.</summary>
        public static void ServePcPkg(HttpListenerContext ctx)
        {
            var res = ctx.Response;
            string folder = Program.Settings.pc_pkg_folder;
            string name = Uri.UnescapeDataString(ctx.Request.Url.AbsolutePath.Substring("/pcpkg/".Length));
            if (string.IsNullOrEmpty(folder) || name.IndexOfAny(new[] { '/', '\\' }) >= 0 || name.Contains("..") || !name.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase))
            {
                res.StatusCode = 404; res.Close(); return;
            }
            string file = Path.Combine(folder, name);
            if (!File.Exists(file)) { res.StatusCode = 404; res.Close(); return; }

            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
            {
                long len = fs.Length, start = 0, end = len - 1;
                string range = ctx.Request.Headers["Range"];
                res.ContentType = "application/octet-stream";
                res.Headers["Accept-Ranges"] = "bytes";
                if (!string.IsNullOrEmpty(range))
                {
                    var m = Regex.Match(range, @"bytes=(\d*)-(\d*)");
                    if (m.Success)
                    {
                        if (m.Groups[1].Value.Length > 0) start = long.Parse(m.Groups[1].Value);
                        if (m.Groups[2].Value.Length > 0) end = Math.Min(len - 1, long.Parse(m.Groups[2].Value));
                        if (m.Groups[1].Value.Length == 0 && m.Groups[2].Value.Length > 0) { start = Math.Max(0, len - long.Parse(m.Groups[2].Value)); end = len - 1; }
                        if (start > end || start >= len)
                        {
                            res.StatusCode = 416;
                            res.Headers["Content-Range"] = "bytes */" + len;
                            res.Close();
                            return;
                        }
                        res.StatusCode = 206;
                        res.Headers["Content-Range"] = "bytes " + start + "-" + end + "/" + len;
                    }
                }
                long count = end - start + 1;
                res.ContentLength64 = count;
                if (ctx.Request.HttpMethod == "HEAD") { res.Close(); return; }
                fs.Position = start;
                byte[] buf = new byte[1 << 20];
                try
                {
                    while (count > 0)
                    {
                        int n = fs.Read(buf, 0, (int)Math.Min(buf.Length, count));
                        if (n <= 0) break;
                        res.OutputStream.Write(buf, 0, n);
                        count -= n;
                    }
                    res.Close();
                }
                catch { try { res.Abort(); } catch { } }
            }
        }
    }
}
