using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace OrbisWebManager
{
    // ==================================================================== optional PIN for LAN access
    public static class Auth
    {
        const string Cookie = "owm_auth";
        static readonly Dictionary<string, int> fails = new Dictionary<string, int>();
        static readonly Dictionary<string, DateTime> lockedUntil = new Dictionary<string, DateTime>();

        public static bool Enabled { get { return !string.IsNullOrEmpty(Program.Settings.access_pin_hash); } }

        static string Sha(string s)
        {
            using (var h = SHA256.Create())
            {
                var sb = new StringBuilder();
                foreach (byte b in h.ComputeHash(Encoding.UTF8.GetBytes(s))) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public static string HashPin(string pin) { return Sha(Program.Settings.auth_secret + ":" + pin); }
        static string Token() { return Sha("token:" + Program.Settings.auth_secret + ":" + Program.Settings.access_pin_hash); }

        public static bool IsLocal(HttpListenerRequest req) { return req.RemoteEndPoint != null && IPAddress.IsLoopback(req.RemoteEndPoint.Address); }

        public static bool IsAuthenticated(HttpListenerRequest req)
        {
            if (!Enabled || IsLocal(req)) return true;
            var c = req.Cookies[Cookie];
            return c != null && c.Value == Token();
        }

        /// <summary>Gatekeeper for every request. Returns false when it already answered.</summary>
        public static bool Allow(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            if (IsAuthenticated(req)) return true;
            string path = req.Url.AbsolutePath;
            // The console itself downloads PKGs from us (Remote Package Installer) and can't log in.
            if (path.StartsWith("/pcpkg/") && req.RemoteEndPoint != null && req.RemoteEndPoint.Address.ToString() == Program.Settings.ps4_ip) return true;
            if (path == "/login.html" || path == "/api/auth/login" || path == "/manifest.webmanifest" || path.StartsWith("/icons/") || path == "/css/style.css") return true;
            if (path.StartsWith("/api/")) { Http.Error(ctx.Response, 401, "Digite o PIN de acesso."); return false; }
            ctx.Response.Redirect("/login.html");
            ctx.Response.Close();
            return false;
        }

        public static void Login(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            string ip = req.RemoteEndPoint != null ? req.RemoteEndPoint.Address.ToString() : "?";
            lock (fails)
            {
                DateTime until;
                if (lockedUntil.TryGetValue(ip, out until) && DateTime.UtcNow < until)
                {
                    Http.Error(ctx.Response, 429, "Muitas tentativas. Tente de novo em alguns minutos.");
                    return;
                }
            }
            Thread.Sleep(400); // slows down guessing
            string pin = Http.Str(Http.ReadJson(req), "pin").Trim();
            if (Enabled && pin.Length > 0 && HashPin(pin) == Program.Settings.access_pin_hash)
            {
                lock (fails) fails.Remove(ip);
                ctx.Response.Headers.Add("Set-Cookie", Cookie + "=" + Token() + "; Path=/; Max-Age=2592000; HttpOnly; SameSite=Strict");
                Activity.Add("auth", "Acesso pela rede liberado para " + ip);
                Http.Json(ctx.Response, new { success = true });
                return;
            }
            lock (fails)
            {
                int n;
                fails.TryGetValue(ip, out n);
                fails[ip] = ++n;
                if (n >= 8) { lockedUntil[ip] = DateTime.UtcNow.AddMinutes(5); fails[ip] = 0; }
            }
            Http.Error(ctx.Response, 403, "PIN incorreto.");
        }

        public static void SetPin(HttpListenerContext ctx)
        {
            if (!IsLocal(ctx.Request) && !IsAuthenticated(ctx.Request)) { Http.Error(ctx.Response, 403, "Só dá para mudar o PIN no próprio PC."); return; }
            string pin = Http.Str(Http.ReadJson(ctx.Request), "pin").Trim();
            if (pin.Length > 0 && !Regex.IsMatch(pin, @"^\d{4,8}$")) { Http.Error(ctx.Response, 400, "Use de 4 a 8 números."); return; }
            Program.Settings.access_pin_hash = pin.Length == 0 ? "" : HashPin(pin);
            Program.Settings.Save(Program.ConfigPath);
            Activity.Add("auth", pin.Length == 0 ? "PIN de acesso removido" : "PIN de acesso definido");
            if (pin.Length > 0)
                ctx.Response.Headers.Add("Set-Cookie", Cookie + "=" + Token() + "; Path=/; Max-Age=2592000; HttpOnly; SameSite=Strict");
            Http.Json(ctx.Response, new { success = true, enabled = Enabled });
        }
    }

    // ==================================================================== batch cover replacement
    public static class CoverBatch
    {
        public static volatile bool Running;
        public static int Done, Total, Failed;
        public static string Current = "";
        static readonly List<string> log = new List<string>();

        public static void Start(List<string> ids, string source)
        {
            if (Running) throw new Exception("Já existe uma troca em lote em andamento.");
            Running = true; Done = 0; Failed = 0; Total = ids.Count; Current = "";
            lock (log) log.Clear();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    foreach (var id in ids)
                    {
                        Current = Library.TitleOf(id);
                        string err = null;
                        try
                        {
                            byte[] img = null;
                            if (source == "sony" || source == "auto")
                            {
                                var si = id.StartsWith("CUSA") ? Covers.SonyInfo(id) : null;
                                if (si != null && !string.IsNullOrEmpty(si.icon)) img = Covers.HttpGetBytes(si.icon);
                            }
                            if (img == null && (source == "sgdb" || source == "auto") && Covers.HasSgdbKey)
                            {
                                var games = Covers.SgdbSearch(Library.TitleOf(id));
                                if (games.Count > 0)
                                {
                                    int gid = Convert.ToInt32(games[0].GetType().GetProperty("id").GetValue(games[0], null));
                                    var grids = Covers.SgdbArt(gid, "square");
                                    if (grids.Count > 0) img = Covers.HttpGetBytes(Convert.ToString(grids[0].GetType().GetProperty("url").GetValue(grids[0], null)));
                                }
                            }
                            if (img == null) err = "nenhuma arte encontrada";
                            else
                            {
                                Covers.Apply(id, img, "icon", "cover");
                                Library.MarkCoverChanged(id);
                            }
                        }
                        catch (Exception ex) { err = ex.Message; }
                        if (err != null) { Failed++; lock (log) log.Add(Library.TitleOf(id) + ": " + err); }
                        Done++;
                    }
                    Activity.Add("cover", "Capas trocadas em lote: " + (Done - Failed) + " de " + Total);
                }
                finally { Running = false; Current = ""; }
            });
        }

        public static object Status()
        {
            lock (log) return new { running = Running, done = Done, total = Total, failed = Failed, current = Current, errors = log.ToArray() };
        }
    }

    // ==================================================================== router for the new endpoints
    public static class Features
    {
        static readonly Regex TitleRx = new Regex(@"^[A-Z]{4}\d{5}$");

        static object[] Arr(Dictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) && v is object[] ? (object[])v : new object[0];
        }

        static void Run(HttpListenerResponse res, Func<object> f, int errCode = 502)
        {
            object r;
            try { r = f(); }
            catch (Exception ex) { Http.Error(res, errCode, ex.Message); return; }
            Http.Json(res, r);
        }

        public static bool Handle(HttpListenerContext ctx, string path, NameValueCollection q, bool post)
        {
            var req = ctx.Request;
            var res = ctx.Response;
            string id = (q["id"] ?? "").ToUpperInvariant();

            switch (path)
            {
                // ---------------------------------------------------- auth
                case "/api/auth/login": if (!post) return false; Auth.Login(ctx); return true;
                case "/api/auth/pin": if (!post) return false; Auth.SetPin(ctx); return true;
                case "/api/auth/status": Http.Json(res, new { enabled = Auth.Enabled, local = Auth.IsLocal(req) }); return true;

                // ---------------------------------------------------- activity / app
                case "/api/activity": Http.Json(res, Activity.List(80)); return true;
                case "/api/app/update": Run(res, () => AppUpdate()); return true;

                // ---------------------------------------------------- saves
                case "/api/saves": Run(res, () => Saves.List(q["user"] ?? "")); return true;
                case "/api/saves/backups": Run(res, () => { Saves.Validate(q["user"], id); return Saves.Backups(q["user"], id); }); return true;
                case "/api/saves/backup":
                    if (!post) return false;
                    Run(res, () =>
                    {
                        string user = q["user"] ?? "";
                        if (id == "*")
                        {
                            int ok = 0, fail = 0;
                            var errors = new List<string>();
                            var list = Saves.List(user);
                            foreach (var g in (List<object>)list.GetType().GetProperty("games").GetValue(list, null))
                            {
                                string gid = Convert.ToString(g.GetType().GetProperty("id").GetValue(g, null));
                                try { Saves.Backup(user, gid, "Backup de todos os jogos"); ok++; }
                                catch (Exception ex) { fail++; errors.Add(gid + ": " + ex.Message); }
                            }
                            return new { success = fail == 0, ok = ok, failed = fail, errors = errors };
                        }
                        return new { success = true, backup = Saves.Backup(user, id, q["note"]) };
                    });
                    return true;
                case "/api/saves/restore":
                    if (!post) return false;
                    Run(res, () => new { success = true, files = Saves.Restore(q["user"], id, q["stamp"]) });
                    return true;
                case "/api/saves/delete":
                    if (!post) return false;
                    Run(res, () => { Saves.Delete(q["user"], id, q["stamp"]); return new { success = true }; }, 400);
                    return true;
                case "/api/saves/zip":
                    {
                        try { Saves.Validate(q["user"], id); }
                        catch (Exception ex) { Http.Error(res, 400, ex.Message); return true; }
                        SendZip(res, "save_" + id + "_" + q["stamp"] + ".zip", s => Saves.Zip(q["user"], id, q["stamp"], s));
                        return true;
                    }
                case "/api/saves/open-folder":
                    if (!post) return false;
                    if (!Auth.IsLocal(req)) { Http.Error(res, 403, "Só funciona no próprio PC."); return true; }
                    try { System.Diagnostics.Process.Start("explorer.exe", "\"" + Saves.Root + "\""); } catch { }
                    Http.Json(res, new { success = true });
                    return true;

                // ---------------------------------------------------- updates / storage / history / now playing
                case "/api/updates": Run(res, () => Updates.Get(q["refresh"] == "1")); return true;
                case "/api/storage": Run(res, () => Storage.Get(q["refresh"] == "1")); return true;
                case "/api/history":
                    {
                        int span;
                        if (!int.TryParse(q["days"], out span) || span < 1 || span > 366) span = 14;
                        Http.Json(res, History.Get(span));
                        return true;
                    }
                case "/api/nowplaying": Http.Json(res, NowPlaying.Get()); return true;
                case "/api/trophies": Run(res, () => Trophies.Get(q["user"], q["refresh"] == "1")); return true;
                case "/api/trophies/icon":
                    {
                        byte[] png = null;
                        string err = "não encontrado";
                        try { png = Trophies.Icon(q["np"]); } catch (Exception ex) { err = ex.Message; }
                        if (png == null) { Http.Error(res, 404, err); return true; }
                        res.Headers["Cache-Control"] = "public, max-age=604800";
                        Http.Bytes(res, png, "image/png");
                        return true;
                    }
                case "/api/trophies/schema": Run(res, () => Trophies.Schema(q["user"])); return true;

                // ---------------------------------------------------- game: DLC, uninstall
                case "/api/game/dlc":
                    if (!TitleRx.IsMatch(id)) { Http.Error(res, 400, "Title ID inválido."); return true; }
                    Run(res, () => Storage.For(id, q["refresh"] == "1"));
                    return true;
                case "/api/game/uninstall":
                    if (!post) return false;
                    if (!TitleRx.IsMatch(id)) { Http.Error(res, 400, "Title ID inválido."); return true; }
                    Run(res, () =>
                    {
                        string what = q["what"] == "patch" ? "uninstall_patch" : "uninstall_game";
                        string name = Library.TitleOf(id);
                        string r = Ps4Services.RpiCall(what, new { title_id = id });
                        var parsed = new JavaScriptSerializer().DeserializeObject(r) as Dictionary<string, object>;
                        bool ok = parsed != null && parsed.ContainsKey("status") && Convert.ToString(parsed["status"]) == "success";
                        if (!ok) throw new Exception("O Remote Package Installer recusou: " + r);
                        Activity.Add("uninstall", (what == "uninstall_patch" ? "Atualização desinstalada: " : "Jogo desinstalado: ") + name);
                        if (what == "uninstall_game") Library.RefreshAsync();
                        return new { success = true };
                    });
                    return true;

                // ---------------------------------------------------- downloads control (RPI / BGFT task ids)
                case "/api/downloads/control":
                    if (!post) return false;
                    Run(res, () =>
                    {
                        string task = q["task"] ?? "";
                        if (!Downloads.IsTaskId(task)) throw new Exception("Tarefa inválida.");
                        long tid = Convert.ToInt64(task, 16);
                        string action = q["action"] ?? "";
                        string api = action == "pause" ? "pause_task" : action == "resume" ? "resume_task" : action == "cancel" ? "stop_task" : null;
                        if (api == null) throw new Exception("Ação inválida.");
                        string r = Ps4Services.RpiCall(api, new { task_id = tid });
                        var parsed = new JavaScriptSerializer().DeserializeObject(r) as Dictionary<string, object>;
                        bool ok = parsed != null && parsed.ContainsKey("status") && Convert.ToString(parsed["status"]) == "success";
                        if (ok && action == "cancel") { try { Ps4Services.RpiCall("unregister_task", new { task_id = tid }); } catch { } }
                        if (!ok) throw new Exception("O PS4 recusou: " + r);
                        Activity.Add("download", "Download " + (action == "pause" ? "pausado" : action == "resume" ? "retomado" : "cancelado") + " (tarefa " + task + ")");
                        return new { success = true };
                    });
                    return true;

                // ---------------------------------------------------- PKGs on the console: analysis / cleanup
                case "/api/pkgs/analyze":
                    Run(res, () =>
                    {
                        var list = new List<object>();
                        foreach (var f in Ftp.List("/data/pkg/"))
                        {
                            if (f.isDir || !f.name.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase)) continue;
                            var info = Pkg.InfoRemoteCached("/data/pkg/" + f.name, f.size);
                            string note;
                            string status = Pkg.Status(info, out note);
                            list.Add(new { name = f.name, path = "/data/pkg/" + f.name, size = f.size, title = info.title, titleId = info.titleId, version = info.version, kind = info.kind, status = status, note = note, canDelete = status == "installed" || status == "older" });
                        }
                        return list;
                    });
                    return true;
                case "/api/pc/pkg/icon":
                    {
                        string folder = Program.Settings.pc_pkg_folder;
                        string name = q["name"] ?? "";
                        if (string.IsNullOrEmpty(folder) || name.IndexOfAny(new[] { '/', '\\' }) >= 0 || name.Contains("..")) { res.StatusCode = 404; res.Close(); return true; }
                        string file = Path.Combine(folder, name);
                        byte[] icon = null;
                        try { if (File.Exists(file)) Pkg.ReadLocal(file, true, out icon); } catch { }
                        if (icon == null) { res.StatusCode = 404; res.Close(); return true; }
                        res.Headers["Cache-Control"] = "public, max-age=86400";
                        Http.Bytes(res, icon, "image/png");
                        return true;
                    }

                // ---------------------------------------------------- files: resumable upload support
                case "/api/files/size":
                    {
                        string p = q["path"] ?? "";
                        long size = -1;
                        try { size = Ftp.Run(c => c.Size(p)); } catch { }
                        Http.Json(res, new { size = size });
                        return true;
                    }

                // ---------------------------------------------------- GoldHEN
                case "/api/goldhen/config":
                    if (post)
                    {
                        var d = Http.ReadJson(req);
                        Run(res, () => { int n = GoldHen.WriteConfig(Arr(d, "changes")); Activity.Add("goldhen", "Configurações do GoldHEN alteradas (" + n + ")"); return new { success = true, applied = n }; });
                    }
                    else Run(res, () => GoldHen.ReadConfig());
                    return true;
                case "/api/goldhen/plugins":
                    if (post)
                    {
                        var d = Http.ReadJson(req);
                        Run(res, () => { GoldHen.WritePlugins(Arr(d, "sections")); Activity.Add("goldhen", "Plugins do GoldHEN atualizados"); return new { success = true }; });
                    }
                    else Run(res, () => GoldHen.ReadPlugins());
                    return true;
                case "/api/goldhen/patches": Run(res, () => GoldHen.Patches(q["refresh"] == "1")); return true;
                case "/api/goldhen/patches/install":
                    if (!post) return false;
                    if (!TitleRx.IsMatch(id)) { Http.Error(res, 400, "Title ID inválido."); return true; }
                    Run(res, () =>
                    {
                        if (q["remove"] == "1") { GoldHen.RemovePatch(id); Activity.Add("goldhen", "Patches removidos: " + Library.TitleOf(id)); }
                        else { GoldHen.InstallPatch(id); Activity.Add("goldhen", "Patches instalados: " + Library.TitleOf(id)); }
                        return new { success = true };
                    });
                    return true;
                case "/api/goldhen/cheats-repo": Run(res, () => GoldHen.CheatRepo(q["refresh"] == "1")); return true;
                case "/api/goldhen/cheats-repo/install":
                    if (!post) return false;
                    Run(res, () =>
                    {
                        string file = q["file"] ?? "";
                        GoldHen.InstallCheat(file);
                        Summary.Invalidate();
                        Activity.Add("cheat", "Cheat baixado do repositório: " + file);
                        return new { success = true };
                    });
                    return true;

                // ---------------------------------------------------- covers in batch
                case "/api/covers/batch":
                    if (post)
                    {
                        var d = Http.ReadJson(req);
                        var ids = new List<string>();
                        foreach (var o in Arr(d, "ids")) { string s = Convert.ToString(o).ToUpperInvariant(); if (TitleRx.IsMatch(s)) ids.Add(s); }
                        string source = Http.Str(d, "source");
                        if (source != "sony" && source != "sgdb") source = "auto";
                        Run(res, () => { CoverBatch.Start(ids, source); return CoverBatch.Status(); }, 409);
                    }
                    else Http.Json(res, CoverBatch.Status());
                    return true;

                // ---------------------------------------------------- captures as zip
                case "/api/captures/zip":
                    {
                        if (!post) return false;
                        var d = Http.ReadJson(req);
                        var paths = new List<string>();
                        foreach (var o in Arr(d, "paths"))
                        {
                            string p = Convert.ToString(o);
                            if (p.StartsWith("/user/av_contents/") && !p.Contains("..")) paths.Add(p);
                        }
                        if (paths.Count == 0) { Http.Error(res, 400, "Nenhuma captura selecionada."); return true; }
                        SendZip(res, "capturas_ps4_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".zip", s =>
                        {
                        using (var zip = new ZipArchive(s, ZipArchiveMode.Create, true))
                        {
                            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var p in paths)
                            {
                                string name = p.Substring(p.LastIndexOf('/') + 1);
                                string unique = name;
                                for (int i = 2; used.Contains(unique); i++) unique = Path.GetFileNameWithoutExtension(name) + "_" + i + Path.GetExtension(name);
                                used.Add(unique);
                                var e = zip.CreateEntry(unique, CompressionLevel.NoCompression);
                                using (var es = e.Open())
                                    Ftp.RunOnce("Compactando capturas", c => c.Retrieve(p, es, 0));
                            }
                        }
                        });
                        Activity.Add("capture", paths.Count + " capturas baixadas em .zip");
                        return true;
                    }

                // ---------------------------------------------------- export library
                case "/api/export/library.csv":
                    {
                        var sb = new StringBuilder();
                        sb.Append("﻿Title ID;Nome;Versão;Categoria;Tempo de jogo (min);Vezes iniciado;Cheats;Capa personalizada;Tamanho (GB);Atualização disponível\r\n");
                        var storage = new Dictionary<string, long>();
                        var updates = new Dictionary<string, string>();
                        try
                        {
                            var st = Storage.Get(false);
                            foreach (var g in (List<object>)st.GetType().GetProperty("games").GetValue(st, null))
                                storage[Convert.ToString(g.GetType().GetProperty("id").GetValue(g, null))] = (long)g.GetType().GetProperty("total").GetValue(g, null);
                            var up = Updates.Get(false);
                            foreach (var g in (List<object>)up.GetType().GetProperty("games").GetValue(up, null))
                                if ((bool)g.GetType().GetProperty("hasUpdate").GetValue(g, null))
                                    updates[Convert.ToString(g.GetType().GetProperty("id").GetValue(g, null))] = Convert.ToString(g.GetType().GetProperty("latest").GetValue(g, null));
                        }
                        catch { }
                        foreach (var g in Library.Games())
                        {
                            long size;
                            storage.TryGetValue(g.id, out size);
                            string upd;
                            updates.TryGetValue(g.id, out upd);
                            sb.Append(Csv(g.id)).Append(';').Append(Csv(g.name)).Append(';').Append(Csv(g.version)).Append(';').Append(Csv(g.category)).Append(';')
                              .Append(g.timePlayedMinutes).Append(';').Append(g.gameStarts).Append(';').Append(g.hasCheats ? "Sim" : "Não").Append(';')
                              .Append(g.customCover ? "Sim" : "Não").Append(';').Append(size > 0 ? (size / 1073741824.0).ToString("0.00") : "").Append(';')
                              .Append(Csv(upd ?? "")).Append("\r\n");
                        }
                        res.Headers["Content-Disposition"] = Http.ContentDisposition("biblioteca_ps4_" + DateTime.Now.ToString("yyyyMMdd") + ".csv");
                        Http.Bytes(res, Encoding.UTF8.GetBytes(sb.ToString()), "text/csv; charset=utf-8");
                        return true;
                    }

                // ---------------------------------------------------- play-time limit settings
                case "/api/playlimit":
                    if (post)
                    {
                        var d = Http.ReadJson(req);
                        int m;
                        int.TryParse(Http.Str(d, "minutes"), out m);
                        string curfew = Http.Str(d, "curfew").Trim();
                        TimeSpan ts;
                        if (curfew.Length > 0 && !TimeSpan.TryParse(curfew, out ts)) { Http.Error(res, 400, "Horário inválido (use 22:00)."); return true; }
                        Program.Settings.playlimit_minutes = Math.Max(0, Math.Min(m, 24 * 60));
                        Program.Settings.playlimit_curfew = curfew;
                        Program.Settings.Save(Program.ConfigPath);
                        Activity.Add("limit", "Limite de jogo: " + (m > 0 ? m + " min" : "sem limite") + (curfew.Length > 0 ? ", aviso às " + curfew : ""));
                    }
                    Http.Json(res, new { minutes = Program.Settings.playlimit_minutes, curfew = Program.Settings.playlimit_curfew });
                    return true;
            }
            return false;
        }

        /// <summary>ZipArchive needs a seekable stream, the HTTP response isn't: build in a temp file, then stream it.</summary>
        static void SendZip(HttpListenerResponse res, string fileName, Action<Stream> build)
        {
            string tmp = Path.Combine(Path.GetTempPath(), "orbis_" + Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.ReadWrite)) build(fs);
                res.ContentType = "application/zip";
                res.Headers["Content-Disposition"] = Http.ContentDisposition(fileName);
                using (var fs = File.OpenRead(tmp))
                {
                    res.ContentLength64 = fs.Length;
                    fs.CopyTo(res.OutputStream);
                }
                res.Close();
            }
            finally { try { File.Delete(tmp); } catch { } }
        }

        static string Csv(string s)
        {
            s = s ?? "";
            if (s.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0) return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        // ---------------------------------------------------- new version check (GitHub releases)
        static object updateCache;
        static DateTime updateAt = DateTime.MinValue;

        static object AppUpdate()
        {
            if (updateCache != null && (DateTime.UtcNow - updateAt).TotalHours < 6) return updateCache;
            string latest = "", url = "https://github.com/kromossomom-debug/orbis-web-manager/releases", notes = "";
            try
            {
                string json = Covers.HttpGetString("https://api.github.com/repos/kromossomom-debug/orbis-web-manager/releases/latest", null);
                var d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                latest = Convert.ToString(d["tag_name"]).TrimStart('v', 'V');
                if (d.ContainsKey("html_url")) url = Convert.ToString(d["html_url"]);
                if (d.ContainsKey("name")) notes = Convert.ToString(d["name"]);
            }
            catch { }
            bool newer = latest.Length > 0 && Pkg.CompareVersion(latest, Program.AppVersion) > 0;
            updateCache = new { current = Program.AppVersion, latest = latest, hasUpdate = newer, url = url, name = notes };
            updateAt = DateTime.UtcNow;
            return updateCache;
        }
    }
}
