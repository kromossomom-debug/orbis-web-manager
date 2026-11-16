using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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

        public static Config Load(string path)
        {
            var serializer = new JavaScriptSerializer();
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var cfg = serializer.Deserialize<Config>(json);
                    if (string.IsNullOrEmpty(cfg.ps4_fw)) cfg.ps4_fw = "9.00";
                    if (cfg.active_user == null) cfg.active_user = "";
                    return cfg;
                }
                catch { }
            }
            var def = new Config
            {
                ps4_ip = "192.168.18.54",
                ftp_port = 2121,
                payload_port = 9090,
                http_port = 5000,
                ps4_fw = "9.00",
                active_user = ""
            };
            def.Save(path);
            return def;
        }

        public void Save(string path)
        {
            var serializer = new JavaScriptSerializer();
            string json = serializer.Serialize(this);
            File.WriteAllText(path, json);
        }
    }

    public class PS4User
    {
        public string id { get; set; }
        public string name { get; set; }
        public string date { get; set; }
        public DateTime lastModified { get; set; }
    }

    public class FtpItem
    {
        public string name { get; set; }
        public bool isDir { get; set; }
        public long size { get; set; }
        public string date { get; set; }
        public string permissions { get; set; }
    }

    public class GameItem
    {
        public string id { get; set; }
        public string name { get; set; }
        public string contentId { get; set; }
        public int timePlayedMinutes { get; set; }
        public int gameStarts { get; set; }
        public bool hasCheats { get; set; }
        public string iconUrl { get; set; }
    }

    public class CaptureItem
    {
        public string titleId { get; set; }
        public string fileName { get; set; }
        public string fullPath { get; set; }
        public string imageUrl { get; set; }
    }

    class Program
    {
        public static Config Settings;
        public static string AppDir;
        public static string CacheDir;
        public static string IconsDir;
        public static string CapturesDir;
        public static string WwwDir;
        public static string ConfigPath;
        public static Dictionary<string, string> GameTitleCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, string> UserNameCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static DateTime ParseFtpDate(string dateStr)
        {
            if (string.IsNullOrEmpty(dateStr)) return DateTime.MinValue;
            string[] parts = dateStr.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                int year = DateTime.Now.Year;
                int day = 1;
                int.TryParse(parts[1], out day);
                if (!parts[2].Contains(":"))
                {
                    int.TryParse(parts[2], out year);
                }
                int month = 1;
                string[] months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
                for (int i = 0; i < months.Length; i++)
                {
                    if (parts[0].StartsWith(months[i], StringComparison.OrdinalIgnoreCase))
                    {
                        month = i + 1;
                        break;
                    }
                }
                try { return new DateTime(year, month, day); } catch { }
            }
            return DateTime.MinValue;
        }

        // Synchronization lock for PS4 embedded FTP server to prevent concurrent connection drops
        private static readonly object FtpLock = new object();

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            AppDir = AppDomain.CurrentDomain.BaseDirectory;
            CacheDir = Path.Combine(AppDir, "cache");
            IconsDir = Path.Combine(CacheDir, "icons");
            CapturesDir = Path.Combine(CacheDir, "captures");
            WwwDir = Path.Combine(AppDir, "wwwroot");
            ConfigPath = Path.Combine(AppDir, "config.json");

            if (!Directory.Exists(CacheDir)) Directory.CreateDirectory(CacheDir);
            if (!Directory.Exists(IconsDir)) Directory.CreateDirectory(IconsDir);
            if (!Directory.Exists(CapturesDir)) Directory.CreateDirectory(CapturesDir);

            Settings = Config.Load(ConfigPath);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
   ___       _     _      __      __   _       __  __                                 
  / _ \ _ _ | |__ (_)___  \ \    / /__| |__   |  \/  |__ _ _ _  __ _ __ _ ___ _ _     
 | (_) | '_|| '_ \| (_-<   \ \/\/ / -_) '_ \  | |\/| / _` | ' \/ _` / _` / -_) '_|    
  \___/|_|  |_.__/|_/__/    \_/\_/\___|_.__/  |_|  |_\__,_|_||_\__,_\__, \___|_|      
                                                                    |___/             
               PlayStation 4 Web Management Suite (GoldHEN Compatible)
            ");
            Console.ResetColor();

            LoadCachedGameTitles();

            StartHttpServer();
        }

        static void LoadCachedGameTitles()
        {
            string dbCache = Path.Combine(CacheDir, "app.db");
            if (File.Exists(dbCache))
            {
                ParseAppDb(dbCache);
            }
        }

        public static void ParseAppDb(string dbPath)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(dbPath);
                string str = Encoding.UTF8.GetString(bytes);
                var matches = Regex.Matches(str, @"(?:[A-Z]{2}\d{4}-[A-Z0-9]{9}_\d{2}-[A-Z0-9]{16})([^\x00\x01\x02\x03\x04\x05\x06\x07\x08]+)/user/appmeta/([A-Za-z0-9_]{9})");
                foreach (Match m in matches)
                {
                    string id = m.Groups[2].Value.ToUpper();
                    string name = m.Groups[1].Value.Trim();
                    if (!GameTitleCache.ContainsKey(id) && name.Length > 1)
                    {
                        GameTitleCache[id] = name;
                    }
                }
            }
            catch { }
        }

        static void StartHttpServer()
        {
            HttpListener listener = new HttpListener();
            try
            {
                listener.Prefixes.Add("http://localhost:" + Settings.http_port + "/");
                listener.Prefixes.Add("http://127.0.0.1:" + Settings.http_port + "/");
            }
            catch { }

            List<string> localIps = new List<string>();
            try
            {
                foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName()))
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        string ipStr = ip.ToString();
                        localIps.Add(ipStr);
                        try
                        {
                            listener.Prefixes.Add("http://" + ipStr + ":" + Settings.http_port + "/");
                        }
                        catch { }
                    }
                }
            }
            catch { }

            try
            {
                listener.Start();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Erro ao iniciar HttpListener: " + ex.Message);
                Console.ResetColor();
                listener = new HttpListener();
                listener.Prefixes.Add("http://localhost:" + Settings.http_port + "/");
                listener.Start();
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[+] Servidor Web Iniciado com Sucesso!");
            Console.WriteLine("    -> Acesso no PC:     http://localhost:" + Settings.http_port);
            foreach (var ip in localIps)
            {
                Console.WriteLine("    -> Acesso no Celular: http://" + ip + ":" + Settings.http_port);
            }
            Console.WriteLine("    -> Conectado ao PS4:  " + Settings.ps4_ip + ":" + Settings.ftp_port);
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Pressione Ctrl+C para encerrar o servidor.");

            ThreadPool.QueueUserWorkItem(state =>
            {
                Thread.Sleep(1000);
                try { Process.Start("http://localhost:" + Settings.http_port); } catch { }
            });

            while (true)
            {
                try
                {
                    HttpListenerContext ctx = listener.GetContext();
                    ThreadPool.QueueUserWorkItem(ProcessRequest, ctx);
                }
                catch
                {
                    break;
                }
            }
        }

        static void ProcessRequest(object state)
        {
            HttpListenerContext ctx = (HttpListenerContext)state;
            HttpListenerRequest req = ctx.Request;
            HttpListenerResponse res = ctx.Response;

            res.Headers.Add("Access-Control-Allow-Origin", "*");
            res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
            res.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 200;
                res.Close();
                return;
            }

            string path = req.Url.AbsolutePath;

            try
            {
                if (path.StartsWith("/api/"))
                {
                    HandleApi(ctx);
                }
                else
                {
                    ServeStaticFile(ctx, path);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    res.StatusCode = 500;
                    byte[] err = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(new { error = ex.Message }));
                    res.ContentType = "application/json";
                    res.OutputStream.Write(err, 0, err.Length);
                    res.Close();
                }
                catch { }
            }
        }

        static void ServeStaticFile(HttpListenerContext ctx, string path)
        {
            if (path == "/" || string.IsNullOrEmpty(path)) path = "/index.html";
            string localPath = Path.Combine(WwwDir, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(localPath))
            {
                localPath = Path.Combine(WwwDir, "index.html");
            }

            if (File.Exists(localPath))
            {
                string ext = Path.GetExtension(localPath).ToLower();
                string contentType = "application/octet-stream";
                switch (ext)
                {
                    case ".html": contentType = "text/html; charset=utf-8"; break;
                    case ".css": contentType = "text/css; charset=utf-8"; break;
                    case ".js": contentType = "application/javascript; charset=utf-8"; break;
                    case ".json": contentType = "application/json; charset=utf-8"; break;
                    case ".png": contentType = "image/png"; break;
                    case ".jpg":
                    case ".jpeg": contentType = "image/jpeg"; break;
                    case ".svg": contentType = "image/svg+xml"; break;
                    case ".ico": contentType = "image/x-icon"; break;
                }

                ctx.Response.ContentType = contentType;
                byte[] content = File.ReadAllBytes(localPath);
                ctx.Response.ContentLength64 = content.Length;
                ctx.Response.OutputStream.Write(content, 0, content.Length);
                ctx.Response.Close();
            }
            else
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.Close();
            }
        }

        static void SendJson(HttpListenerResponse res, object data)
        {
            var serializer = new JavaScriptSerializer();
            string json = serializer.Serialize(data);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            res.ContentType = "application/json; charset=utf-8";
            res.ContentLength64 = bytes.Length;
            res.OutputStream.Write(bytes, 0, bytes.Length);
            res.Close();
        }

        static void HandleApi(HttpListenerContext ctx)
        {
            HttpListenerRequest req = ctx.Request;
            HttpListenerResponse res = ctx.Response;
            string path = req.Url.AbsolutePath;
            var query = req.QueryString;

            // 1. STATUS
            if (path == "/api/status")
            {
                long pingMs = -1;
                bool pingOk = false;
                try
                {
                    using (Ping p = new Ping())
                    {
                        var reply = p.Send(Settings.ps4_ip, 1000);
                        if (reply.Status == IPStatus.Success)
                        {
                            pingOk = true;
                            pingMs = reply.RoundtripTime;
                        }
                    }
                }
                catch { }

                bool ftpOk = false;
                string banner = "";
                try
                {
                    using (var c = new TcpClient())
                    {
                        var t = c.ConnectAsync(Settings.ps4_ip, Settings.ftp_port).Wait(800);
                        if (t && c.Connected)
                        {
                            ftpOk = true;
                            c.ReceiveTimeout = 800;
                            var s = c.GetStream();
                            byte[] b = new byte[256];
                            int read = s.Read(b, 0, b.Length);
                            banner = Encoding.ASCII.GetString(b, 0, read).Trim();
                        }
                    }
                }
                catch { }

                bool payloadPortOk = false;
                try
                {
                    using (var c = new TcpClient())
                    {
                        payloadPortOk = c.ConnectAsync(Settings.ps4_ip, Settings.payload_port).Wait(400);
                    }
                }
                catch { }

                string username = "PlayStation 4";
                string userId = "";
                List<object> detectedUsers = new List<object>();
                try
                {
                    var userFolders = ListFtpDirectory("/user/home/");
                    var userList = new List<PS4User>();
                    foreach (var uf in userFolders)
                    {
                        if (uf.isDir && uf.name.Length >= 4 && uf.name != "." && uf.name != "..")
                        {
                            string uName = "";
                            if (UserNameCache.ContainsKey(uf.name))
                            {
                                uName = UserNameCache[uf.name];
                            }
                            else
                            {
                                string userDat = DownloadFtpText("/user/home/" + uf.name + "/username.dat");
                                if (!string.IsNullOrEmpty(userDat))
                                {
                                    uName = userDat.Trim('\0', ' ', '\r', '\n');
                                    UserNameCache[uf.name] = uName;
                                }
                            }

                            if (string.IsNullOrEmpty(uName)) uName = uf.name;

                            userList.Add(new PS4User
                            {
                                id = uf.name,
                                name = uName,
                                date = uf.date,
                                lastModified = ParseFtpDate(uf.date)
                            });
                        }
                    }

                    if (userList.Count > 0)
                    {
                        userList.Sort((a, b) => b.lastModified.CompareTo(a.lastModified));

                        PS4User chosen = null;
                        if (!string.IsNullOrEmpty(Settings.active_user))
                        {
                            chosen = userList.Find(u => u.name.Equals(Settings.active_user, StringComparison.OrdinalIgnoreCase) || u.id.Equals(Settings.active_user, StringComparison.OrdinalIgnoreCase));
                        }
                        if (chosen == null)
                        {
                            chosen = userList[0];
                        }

                        username = chosen.name;
                        userId = chosen.id;

                        foreach (var u in userList)
                        {
                            detectedUsers.Add(new
                            {
                                id = u.id,
                                name = u.name,
                                date = u.date,
                                isCurrent = (u.id == chosen.id)
                            });
                        }
                    }
                }
                catch { }

                string goldhenVer = "GoldHEN v2.2";
                if (!string.IsNullOrEmpty(banner))
                {
                    var mHen = Regex.Match(banner, @"(GoldHEN[^\r\n]*)", RegexOptions.IgnoreCase);
                    if (mHen.Success)
                    {
                        string rawVer = mHen.Groups[1].Value.Replace("FTP server", "").Replace("FTP Server", "").Trim();
                        goldhenVer = Regex.Replace(rawVer, @"\s+", " ");
                    }
                }

                int cheatCount = 0;
                try
                {
                    var cheats = ListFtpDirectory("/data/GoldHEN/cheats/json/");
                    cheatCount = cheats.Count;
                }
                catch { }

                int pkgCount = 0;
                long totalPkgSize = 0;
                try
                {
                    var pkgs = ListFtpDirectory("/data/pkg/");
                    foreach (var p in pkgs)
                    {
                        if (!p.isDir && p.name.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase))
                        {
                            pkgCount++;
                            totalPkgSize += p.size;
                        }
                    }
                }
                catch { }

                SendJson(res, new
                {
                    online = pingOk || ftpOk,
                    ping = pingMs,
                    ip = Settings.ps4_ip,
                    ftpPort = Settings.ftp_port,
                    ftpOk = ftpOk,
                    payloadPort = Settings.payload_port,
                    payloadPortOk = payloadPortOk,
                    banner = banner,
                    fw = string.IsNullOrEmpty(Settings.ps4_fw) ? "9.00" : Settings.ps4_fw,
                    goldhenVer = goldhenVer,
                    username = username,
                    userId = userId,
                    users = detectedUsers,
                    cheatCount = cheatCount,
                    pkgCount = pkgCount,
                    totalPkgSize = totalPkgSize
                });
                return;
            }

            // 2. GAMES
            if (path == "/api/games")
            {
                string dbCache = Path.Combine(CacheDir, "app.db");
                if (!File.Exists(dbCache))
                {
                    try
                    {
                        DownloadFtpFile("/system_data/priv/mms/app.db", dbCache);
                        ParseAppDb(dbCache);
                    }
                    catch { }
                }

                var appFolders = ListFtpDirectory("/user/appmeta/");
                List<GameItem> games = new List<GameItem>();

                HashSet<string> cheatIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var cheatFiles = ListFtpDirectory("/data/GoldHEN/cheats/json/");
                    foreach (var f in cheatFiles)
                    {
                        var m = Regex.Match(f.name, @"^([A-Z0-9_]{9})_");
                        if (m.Success) cheatIds.Add(m.Groups[1].Value);
                    }
                }
                catch { }

                // Non-game system folders to ignore
                HashSet<string> ignoreFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "addcont", "push_resource", "NPXS39041", "NPXS20001"
                };

                foreach (var folder in appFolders)
                {
                    if (folder.isDir && !ignoreFolders.Contains(folder.name))
                    {
                        string id = folder.name.ToUpper();
                        string name = id;
                        if (GameTitleCache.ContainsKey(id))
                        {
                            name = GameTitleCache[id];
                        }

                        int timePlayed = 0;
                        int gameStarts = 0;
                        try
                        {
                            string statIni = DownloadFtpText("/data/GoldHEN/stats/" + id + ".ini");
                            if (!string.IsNullOrEmpty(statIni))
                            {
                                var mTime = Regex.Match(statIni, @"Time_Played\s*=\s*(\d+)");
                                if (mTime.Success) timePlayed = int.Parse(mTime.Groups[1].Value);

                                var mStarts = Regex.Match(statIni, @"Game_Starts\s*=\s*(\d+)");
                                if (mStarts.Success) gameStarts = int.Parse(mStarts.Groups[1].Value);
                            }
                        }
                        catch { }

                        games.Add(new GameItem
                        {
                            id = id,
                            name = name,
                            timePlayedMinutes = timePlayed,
                            gameStarts = gameStarts,
                            hasCheats = cheatIds.Contains(id),
                            iconUrl = "/api/game/icon?id=" + id
                        });
                    }
                }

                games.Sort((a, b) =>
                {
                    int c = b.timePlayedMinutes.CompareTo(a.timePlayedMinutes);
                    if (c != 0) return c;
                    return a.name.CompareTo(b.name);
                });

                SendJson(res, games);
                return;
            }

            // 3. GAME ICON
            if (path == "/api/game/icon")
            {
                string id = query["id"];
                if (string.IsNullOrEmpty(id)) { res.StatusCode = 400; res.Close(); return; }

                string iconCache = Path.Combine(IconsDir, id + ".png");
                if (!File.Exists(iconCache))
                {
                    try
                    {
                        DownloadFtpFile("/user/appmeta/" + id + "/icon0.png", iconCache);
                    }
                    catch
                    {
                        // Check if tmp icon exists in /data/
                        try
                        {
                            var tmpFiles = ListFtpDirectory("/data/");
                            foreach (var f in tmpFiles)
                            {
                                if (f.name.Contains(id) && f.name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                                {
                                    DownloadFtpFile("/data/" + f.name, iconCache);
                                    break;
                                }
                            }
                        }
                        catch { }
                    }
                }

                if (File.Exists(iconCache))
                {
                    res.ContentType = "image/png";
                    byte[] b = File.ReadAllBytes(iconCache);
                    res.ContentLength64 = b.Length;
                    res.OutputStream.Write(b, 0, b.Length);
                    res.Close();
                }
                else
                {
                    res.StatusCode = 404;
                    res.Close();
                }
                return;
            }

            // 4. UPLOAD CUSTOM COVER
            if (path == "/api/game/icon/upload" && req.HttpMethod == "POST")
            {
                string id = query["id"];
                if (string.IsNullOrEmpty(id))
                {
                    res.StatusCode = 400;
                    SendJson(res, new { error = "O parâmetro 'id' é obrigatório." });
                    return;
                }

                byte[] imgBytes;
                using (var ms = new MemoryStream())
                {
                    req.InputStream.CopyTo(ms);
                    imgBytes = ms.ToArray();
                }

                if (imgBytes.Length > 0)
                {
                    // Update local cache
                    string iconCache = Path.Combine(IconsDir, id + ".png");
                    File.WriteAllBytes(iconCache, imgBytes);

                    // Upload directly to PS4
                    string remotePath = "/user/appmeta/" + id + "/icon0.png";
                    using (var ms = new MemoryStream(imgBytes))
                    {
                        StreamFtpUpload(remotePath, ms, imgBytes.Length);
                    }

                    SendJson(res, new { success = true, iconUrl = "/api/game/icon?id=" + id + "&t=" + DateTime.Now.Ticks });
                }
                else
                {
                    res.StatusCode = 400;
                    SendJson(res, new { error = "Arquivo vazio." });
                }
                return;
            }

            // 5. CAPTURE GALLERY (SCREENSHOTS)
            if (path == "/api/captures/games")
            {
                // List games that have captures in /user/av_contents/photo/NPXS20001/
                List<object> captureGames = new List<object>();
                try
                {
                    var folders = ListFtpDirectory("/user/av_contents/photo/NPXS20001/");
                    foreach (var f in folders)
                    {
                        if (f.isDir)
                        {
                            string gameId = f.name.ToUpper();
                            string titleName = gameId;
                            if (GameTitleCache.ContainsKey(gameId)) titleName = GameTitleCache[gameId];

                            captureGames.Add(new
                            {
                                id = gameId,
                                name = titleName,
                                iconUrl = "/api/game/icon?id=" + gameId
                            });
                        }
                    }
                }
                catch { }

                SendJson(res, captureGames);
                return;
            }

            if (path == "/api/captures")
            {
                string targetGame = query["game"];
                List<CaptureItem> items = new List<CaptureItem>();
                string baseFolder = "/user/av_contents/photo/NPXS20001/";

                try
                {
                    List<string> gamesToScan = new List<string>();
                    if (!string.IsNullOrEmpty(targetGame))
                    {
                        gamesToScan.Add(targetGame);
                    }
                    else
                    {
                        var folders = ListFtpDirectory(baseFolder);
                        if (folders.Count > 0) gamesToScan.Add(folders[0].name);
                    }

                    foreach (var g in gamesToScan)
                    {
                        string gFolder = baseFolder + g + "/";
                        var subFolders = ListFtpDirectory(gFolder);
                        if (subFolders.Count > 10)
                        {
                            subFolders = subFolders.GetRange(subFolders.Count - 10, 10);
                        }

                        foreach (var sf in subFolders)
                        {
                            if (sf.isDir)
                            {
                                string shotFolder = gFolder + sf.name + "/";
                                var shotFiles = ListFtpDirectory(shotFolder);
                                foreach (var file in shotFiles)
                                {
                                    if (file.name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                                    {
                                        string shotPath = shotFolder + file.name;
                                        items.Add(new CaptureItem
                                        {
                                            titleId = g,
                                            fileName = file.name,
                                            fullPath = shotPath,
                                            imageUrl = "/api/capture/image?path=" + Uri.EscapeDataString(shotPath)
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }

                items.Reverse();
                SendJson(res, items);
                return;
            }

            if (path == "/api/capture/image")
            {
                string shotPath = query["path"];
                if (string.IsNullOrEmpty(shotPath)) { res.StatusCode = 400; res.Close(); return; }

                string fileName = Path.GetFileName(shotPath);
                string localCache = Path.Combine(CapturesDir, fileName);

                if (!File.Exists(localCache))
                {
                    try
                    {
                        DownloadFtpFile(shotPath, localCache);
                    }
                    catch
                    {
                        res.StatusCode = 404;
                        res.Close();
                        return;
                    }
                }

                if (File.Exists(localCache))
                {
                    res.ContentType = "image/jpeg";
                    byte[] b = File.ReadAllBytes(localCache);
                    res.ContentLength64 = b.Length;
                    res.OutputStream.Write(b, 0, b.Length);
                    res.Close();
                }
                else
                {
                    res.StatusCode = 404;
                    res.Close();
                }
                return;
            }

            // 6. FILES
            if (path == "/api/files")
            {
                string dir = query["path"];
                if (string.IsNullOrEmpty(dir)) dir = "/data/";
                if (!dir.EndsWith("/")) dir += "/";

                var list = ListFtpDirectory(dir);
                SendJson(res, new
                {
                    currentPath = dir,
                    items = list
                });
                return;
            }

            if (path == "/api/files/download")
            {
                string filePath = query["path"];
                if (string.IsNullOrEmpty(filePath)) { res.StatusCode = 400; res.Close(); return; }

                string fileName = Path.GetFileName(filePath);
                res.ContentType = "application/octet-stream";
                res.Headers.Add("Content-Disposition", "attachment; filename=\"" + fileName + "\"");

                StreamFtpDownload(filePath, res.OutputStream);
                res.Close();
                return;
            }

            if (path == "/api/files/upload" && req.HttpMethod == "POST")
            {
                string targetPath = query["path"];
                string fileName = query["filename"];

                if (string.IsNullOrEmpty(targetPath) || string.IsNullOrEmpty(fileName))
                {
                    res.StatusCode = 400;
                    SendJson(res, new { error = "Parâmetros 'path' e 'filename' são obrigatórios." });
                    return;
                }

                if (!targetPath.EndsWith("/")) targetPath += "/";
                string fullRemotePath = targetPath + fileName;

                long len = req.ContentLength64;
                StreamFtpUpload(fullRemotePath, req.InputStream, len);

                SendJson(res, new { success = true, path = fullRemotePath });
                return;
            }

            if (path == "/api/files/delete" && req.HttpMethod == "POST")
            {
                string targetPath = query["path"];
                bool isDir = query["isDir"] == "true";

                if (isDir)
                {
                    ExecuteFtpCommand(targetPath, WebRequestMethods.Ftp.RemoveDirectory);
                }
                else
                {
                    ExecuteFtpCommand(targetPath, WebRequestMethods.Ftp.DeleteFile);
                }

                SendJson(res, new { success = true });
                return;
            }

            if (path == "/api/files/mkdir" && req.HttpMethod == "POST")
            {
                string targetPath = query["path"];
                ExecuteFtpCommand(targetPath, WebRequestMethods.Ftp.MakeDirectory);
                SendJson(res, new { success = true });
                return;
            }

            // 7. CHEATS
            if (path == "/api/cheats")
            {
                var files = ListFtpDirectory("/data/GoldHEN/cheats/json/");
                List<object> list = new List<object>();

                foreach (var f in files)
                {
                    if (f.name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        var m = Regex.Match(f.name, @"^([A-Z0-9_]{9})_([0-9\.]+)\.json$");
                        string titleId = m.Success ? m.Groups[1].Value : "";
                        string version = m.Success ? m.Groups[2].Value : "";
                        string gameName = titleId;
                        if (GameTitleCache.ContainsKey(titleId)) gameName = GameTitleCache[titleId];

                        list.Add(new
                        {
                            filename = f.name,
                            path = "/data/GoldHEN/cheats/json/" + f.name,
                            titleId = titleId,
                            version = version,
                            gameName = gameName,
                            size = f.size,
                            date = f.date
                        });
                    }
                }

                SendJson(res, list);
                return;
            }

            if (path == "/api/cheat/read")
            {
                string cheatPath = query["path"];
                string content = DownloadFtpText(cheatPath);
                var serializer = new JavaScriptSerializer();
                object parsed = null;
                try
                {
                    parsed = serializer.DeserializeObject(content);
                }
                catch { }

                SendJson(res, new
                {
                    raw = content,
                    data = parsed
                });
                return;
            }

            if (path == "/api/cheat/convert" && req.HttpMethod == "POST")
            {
                string body = "";
                using (var r = new StreamReader(req.InputStream, Encoding.UTF8))
                {
                    body = r.ReadToEnd();
                }

                var serializer = new JavaScriptSerializer();
                var dict = serializer.Deserialize<Dictionary<string, object>>(body);

                string sourceId = (string)dict["sourceId"];
                string targetId = (string)dict["targetId"];
                string version = (string)dict["version"];
                string rawJson = (string)dict["json"];

                string adaptedJson = Regex.Replace(rawJson, sourceId, targetId, RegexOptions.IgnoreCase);

                string remoteFileName = targetId.ToUpper() + "_" + version + ".json";
                string remotePath = "/data/GoldHEN/cheats/json/" + remoteFileName;

                UploadFtpText(remotePath, adaptedJson);

                SendJson(res, new
                {
                    success = true,
                    filename = remoteFileName,
                    path = remotePath
                });
                return;
            }

            // 8. PKGS
            if (path == "/api/pkgs")
            {
                var files = ListFtpDirectory("/data/pkg/");
                List<object> pkgs = new List<object>();

                foreach (var f in files)
                {
                    if (!f.isDir && f.name.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase))
                    {
                        pkgs.Add(new
                        {
                            name = f.name,
                            size = f.size,
                            date = f.date,
                            path = "/data/pkg/" + f.name
                        });
                    }
                }

                SendJson(res, pkgs);
                return;
            }

            // 9. PAYLOAD
            if (path == "/api/payload" && req.HttpMethod == "POST")
            {
                byte[] payloadBytes;
                using (var ms = new MemoryStream())
                {
                    req.InputStream.CopyTo(ms);
                    payloadBytes = ms.ToArray();
                }

                try
                {
                    using (TcpClient client = new TcpClient())
                    {
                        client.Connect(Settings.ps4_ip, Settings.payload_port);
                        using (NetworkStream stream = client.GetStream())
                        {
                            stream.Write(payloadBytes, 0, payloadBytes.Length);
                        }
                    }
                    SendJson(res, new { success = true, message = "Payload enviado para a porta " + Settings.payload_port + "!" });
                }
                catch (Exception ex)
                {
                    res.StatusCode = 500;
                    SendJson(res, new { success = false, error = "Falha ao enviar payload: " + ex.Message });
                }
                return;
            }

            // 10. CONFIG
            if (path == "/api/config")
            {
                if (req.HttpMethod == "POST")
                {
                    using (var r = new StreamReader(req.InputStream, Encoding.UTF8))
                    {
                        string body = r.ReadToEnd();
                        var serializer = new JavaScriptSerializer();
                        var newCfg = serializer.Deserialize<Config>(body);
                        if (!string.IsNullOrEmpty(newCfg.ps4_ip)) Settings.ps4_ip = newCfg.ps4_ip;
                        if (newCfg.ftp_port > 0) Settings.ftp_port = newCfg.ftp_port;
                        if (newCfg.payload_port > 0) Settings.payload_port = newCfg.payload_port;
                        if (!string.IsNullOrEmpty(newCfg.ps4_fw)) Settings.ps4_fw = newCfg.ps4_fw;
                        if (newCfg.active_user != null) Settings.active_user = newCfg.active_user;
                        Settings.Save(ConfigPath);
                    }
                }

                SendJson(res, Settings);
                return;
            }

            // 11. NETWORK
            if (path == "/api/network")
            {
                List<string> ips = new List<string>();
                try
                {
                    foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName()))
                    {
                        if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                        {
                            ips.Add(ip.ToString());
                        }
                    }
                }
                catch { }

                SendJson(res, new
                {
                    pcIps = ips,
                    httpPort = Settings.http_port,
                    ps4Ip = Settings.ps4_ip
                });
                return;
            }

            // 12. BACKUP APP.DB
            if (path == "/api/backup/appdb")
            {
                res.ContentType = "application/octet-stream";
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                res.Headers.Add("Content-Disposition", "attachment; filename=\"app_db_backup_" + timestamp + ".db\"");
                StreamFtpDownload("/system_data/priv/mms/app.db", res.OutputStream);
                res.Close();
                return;
            }

            // 13. SYSTEM NOTIFY (TV OSD)
            if (path == "/api/system/notify" && req.HttpMethod == "POST")
            {
                string msg = "";
                using (var r = new StreamReader(req.InputStream, Encoding.UTF8))
                {
                    string body = r.ReadToEnd();
                    var serializer = new JavaScriptSerializer();
                    var d = serializer.Deserialize<Dictionary<string, object>>(body);
                    if (d.ContainsKey("message")) msg = d["message"].ToString();
                }

                if (string.IsNullOrEmpty(msg))
                {
                    res.StatusCode = 400;
                    SendJson(res, new { error = "Mensagem não informada." });
                    return;
                }

                // Probe if port 9090 is actually open
                bool portOpen = false;
                try
                {
                    using (var c = new TcpClient())
                    {
                        var ar = c.BeginConnect(Settings.ps4_ip, Settings.payload_port, null, null);
                        portOpen = ar.AsyncWaitHandle.WaitOne(800) && c.Connected;
                        if (portOpen) c.EndConnect(ar);
                    }
                }
                catch { }

                if (!portOpen)
                {
                    SendJson(res, new
                    {
                        success = false,
                        error = "A porta " + Settings.payload_port + " (BinLoader Server) está desativada no seu PS4. No console, vá em 'Configurações > GoldHEN' e marque 'Enable BinLoader Server'."
                    });
                    return;
                }

                bool sent = false;
                try
                {
                    using (var c = new TcpClient())
                    {
                        c.Connect(Settings.ps4_ip, Settings.payload_port);
                        byte[] notifyBytes = Encoding.UTF8.GetBytes(msg + "\0");
                        c.GetStream().Write(notifyBytes, 0, notifyBytes.Length);
                        sent = true;
                    }
                }
                catch (Exception ex)
                {
                    SendJson(res, new { success = false, error = "Falha ao enviar para porta " + Settings.payload_port + ": " + ex.Message });
                    return;
                }

                SendJson(res, new { success = sent, delivered = sent, message = msg });
                return;
            }

            // 14. REMOTE PACKAGE INSTALLER (RPI - Port 12800)
            if (path == "/api/system/rpi" && req.HttpMethod == "POST")
            {
                string pkgUrl = "";
                using (var r = new StreamReader(req.InputStream, Encoding.UTF8))
                {
                    string body = r.ReadToEnd();
                    var serializer = new JavaScriptSerializer();
                    var d = serializer.Deserialize<Dictionary<string, object>>(body);
                    if (d.ContainsKey("url")) pkgUrl = d["url"].ToString();
                }

                try
                {
                    var httpReq = (HttpWebRequest)WebRequest.Create("http://" + Settings.ps4_ip + ":12800/api/install");
                    httpReq.Method = "POST";
                    httpReq.ContentType = "application/json";
                    httpReq.Timeout = 4000;
                    string payload = new JavaScriptSerializer().Serialize(new { type = "direct", packages = new string[] { pkgUrl } });
                    byte[] data = Encoding.UTF8.GetBytes(payload);
                    httpReq.ContentLength = data.Length;
                    using (var s = httpReq.GetRequestStream())
                    {
                        s.Write(data, 0, data.Length);
                    }
                    using (var r = (HttpWebResponse)httpReq.GetResponse())
                    {
                        SendJson(res, new { success = true, status = (int)r.StatusCode });
                        return;
                    }
                }
                catch (Exception ex)
                {
                    SendJson(res, new { success = false, error = "RPI (Porta 12800): " + ex.Message });
                    return;
                }
            }

            res.StatusCode = 404;
            SendJson(res, new { error = "Endpoint desconhecido" });
        }

        #region FTP Operations (Synchronized with FtpLock)

        static string BuildFtpUri(string remotePath)
        {
            if (!remotePath.StartsWith("/")) remotePath = "/" + remotePath;
            return "ftp://" + Settings.ps4_ip + ":" + Settings.ftp_port + remotePath;
        }

        public static List<FtpItem> ListFtpDirectory(string path)
        {
            lock (FtpLock)
            {
                List<FtpItem> items = new List<FtpItem>();
                string uri = BuildFtpUri(path);
                var req = (FtpWebRequest)WebRequest.Create(uri);
                req.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
                req.KeepAlive = false;
                req.UsePassive = true;
                req.Timeout = 10000;
                req.ReadWriteTimeout = 10000;

                try
                {
                    using (var res = (FtpWebResponse)req.GetResponse())
                    using (var reader = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
                    {
                        string line;
                        var regex = new Regex(@"^([drwx\-]{10})\s+(\d+)\s+(\S+)\s+(\S+)\s+(\d+)\s+([A-Za-z]{3}\s+\d+\s+[\d:]+)\s+(.+)$");
                        while ((line = reader.ReadLine()) != null)
                        {
                            var m = regex.Match(line);
                            if (m.Success)
                            {
                                string perm = m.Groups[1].Value;
                                long size = long.Parse(m.Groups[5].Value);
                                string date = m.Groups[6].Value;
                                string name = m.Groups[7].Value.Trim();

                                if (name == "." || name == "..") continue;

                                items.Add(new FtpItem
                                {
                                    name = name,
                                    isDir = perm.StartsWith("d"),
                                    size = size,
                                    date = date,
                                    permissions = perm
                                });
                            }
                        }
                    }
                }
                catch { }

                items.Sort((a, b) =>
                {
                    if (a.isDir && !b.isDir) return -1;
                    if (!a.isDir && b.isDir) return 1;
                    return a.name.CompareTo(b.name);
                });

                return items;
            }
        }

        public static string DownloadFtpText(string remotePath)
        {
            lock (FtpLock)
            {
                string uri = BuildFtpUri(remotePath);
                var req = (FtpWebRequest)WebRequest.Create(uri);
                req.Method = WebRequestMethods.Ftp.DownloadFile;
                req.KeepAlive = false;
                req.UsePassive = true;
                req.Timeout = 8000;

                try
                {
                    using (var res = (FtpWebResponse)req.GetResponse())
                    using (var reader = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        public static void DownloadFtpFile(string remotePath, string localPath)
        {
            lock (FtpLock)
            {
                string uri = BuildFtpUri(remotePath);
                var req = (FtpWebRequest)WebRequest.Create(uri);
                req.Method = WebRequestMethods.Ftp.DownloadFile;
                req.KeepAlive = false;
                req.UsePassive = true;
                req.Timeout = 15000;

                using (var res = (FtpWebResponse)req.GetResponse())
                using (var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write))
                {
                    res.GetResponseStream().CopyTo(fs);
                }
            }
        }

        public static void UploadFtpText(string remotePath, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            using (var ms = new MemoryStream(bytes))
            {
                StreamFtpUpload(remotePath, ms, bytes.Length);
            }
        }

        public static void StreamFtpDownload(string remotePath, Stream destination)
        {
            lock (FtpLock)
            {
                string uri = BuildFtpUri(remotePath);
                var req = (FtpWebRequest)WebRequest.Create(uri);
                req.Method = WebRequestMethods.Ftp.DownloadFile;
                req.KeepAlive = false;
                req.UsePassive = true;

                using (var res = (FtpWebResponse)req.GetResponse())
                using (var stream = res.GetResponseStream())
                {
                    byte[] buffer = new byte[65536];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        destination.Write(buffer, 0, read);
                    }
                }
            }
        }

        public static void StreamFtpUpload(string remotePath, Stream source, long length)
        {
            lock (FtpLock)
            {
                string uri = BuildFtpUri(remotePath);
                var req = (FtpWebRequest)WebRequest.Create(uri);
                req.Method = WebRequestMethods.Ftp.UploadFile;
                req.KeepAlive = false;
                req.UsePassive = true;
                req.UseBinary = true;
                if (length > 0) req.ContentLength = length;

                using (var ftpStream = req.GetRequestStream())
                {
                    byte[] buffer = new byte[65536];
                    int read;
                    while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ftpStream.Write(buffer, 0, read);
                    }
                }
            }
        }

        public static void ExecuteFtpCommand(string remotePath, string method)
        {
            lock (FtpLock)
            {
                string uri = BuildFtpUri(remotePath);
                var req = (FtpWebRequest)WebRequest.Create(uri);
                req.Method = method;
                req.KeepAlive = false;
                req.UsePassive = true;
                using (var res = (FtpWebResponse)req.GetResponse()) { }
            }
        }

        #endregion
    }
}
