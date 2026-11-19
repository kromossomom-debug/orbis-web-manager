using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace OrbisWebManager
{
    /// <summary>
    /// Save-data backups. The console keeps each user's saves in /user/home/USERID/savedata/TITLEID/
    /// (encrypted image + sealed key). Copying that folder back restores the save on the same
    /// console and user, which is what a "backup before I mess something up" needs.
    /// </summary>
    public static class Saves
    {
        static readonly Regex UserRx = new Regex("^[0-9a-fA-F]{8}$");
        static readonly Regex TitleRx = new Regex("^[A-Z]{4}\\d{5}$");
        static readonly Regex StampRx = new Regex("^\\d{8}_\\d{6}$");

        public static string Root
        {
            get { string d = Path.Combine(Program.AppDir, "backups", "saves"); Directory.CreateDirectory(d); return d; }
        }

        static string Remote(string user, string id) { return "/user/home/" + user + "/savedata/" + id + "/"; }

        public static void Validate(string user, string id)
        {
            if (!UserRx.IsMatch(user ?? "")) throw new Exception("Usuário inválido.");
            if (id != null && !TitleRx.IsMatch(id)) throw new Exception("Title ID inválido.");
        }

        public static object List(string user)
        {
            Validate(user, null);
            var games = new List<object>();
            foreach (var d in Ftp.List("/user/home/" + user + "/savedata/"))
            {
                string id = d.name.ToUpperInvariant();
                if (!d.isDir || !TitleRx.IsMatch(id)) continue;
                var local = Backups(user, id);
                games.Add(new
                {
                    id = id,
                    name = Library.TitleOf(id),
                    iconUrl = Library.IconUrl(id),
                    date = d.date,
                    installed = Library.IsInstalled(id),
                    backups = local.Count,
                    lastBackup = local.Count > 0 ? ((BackupInfo)local[0]).when : ""
                });
            }
            games.Sort((a, b) => string.Compare(NameOf(a), NameOf(b), StringComparison.OrdinalIgnoreCase));
            return new { user = user, games = games, folder = Root };
        }

        static string NameOf(object o) { return Convert.ToString(o.GetType().GetProperty("name").GetValue(o, null)); }

        public class BackupInfo { public string stamp { get; set; } public string when { get; set; } public long size { get; set; } public int files { get; set; } public string note { get; set; } }

        public static List<object> Backups(string user, string id)
        {
            var list = new List<object>();
            string dir = Path.Combine(Root, user, id);
            if (!Directory.Exists(dir)) return list;
            var stamps = new List<string>(Directory.GetDirectories(dir));
            stamps.Sort();
            stamps.Reverse();
            foreach (var s in stamps)
            {
                string name = Path.GetFileName(s);
                if (!StampRx.IsMatch(name)) continue;
                long size = 0;
                int files = 0;
                foreach (var f in Directory.GetFiles(s, "*", SearchOption.AllDirectories))
                {
                    if (Path.GetFileName(f) == "orbis-backup.json") continue;
                    size += new FileInfo(f).Length;
                    files++;
                }
                string note = "";
                try
                {
                    string meta = Path.Combine(s, "orbis-backup.json");
                    if (File.Exists(meta))
                    {
                        var d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(meta));
                        if (d.ContainsKey("note")) note = Convert.ToString(d["note"]);
                    }
                }
                catch { }
                var when = DateTime.ParseExact(name, "yyyyMMdd_HHmmss", null);
                list.Add(new BackupInfo { stamp = name, when = when.ToString("dd/MM/yyyy HH:mm"), size = size, files = files, note = note });
            }
            return list;
        }

        static void CopyDown(string remoteDir, string localDir, ref long bytes, ref int files)
        {
            Directory.CreateDirectory(localDir);
            foreach (var f in Ftp.List(remoteDir))
            {
                if (f.name == "." || f.name == "..") continue;
                if (f.isDir) { CopyDown(remoteDir + f.name + "/", Path.Combine(localDir, f.name), ref bytes, ref files); continue; }
                Ftp.DownloadToFile(remoteDir + f.name, Path.Combine(localDir, f.name));
                bytes += f.size;
                files++;
            }
        }

        public static BackupInfo Backup(string user, string id, string note)
        {
            Validate(user, id);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string dir = Path.Combine(Root, user, id, stamp);
            long bytes = 0;
            int files = 0;
            try
            {
                CopyDown(Remote(user, id), dir, ref bytes, ref files);
            }
            catch
            {
                try { Directory.Delete(dir, true); } catch { }
                throw;
            }
            File.WriteAllText(Path.Combine(dir, "orbis-backup.json"), new JavaScriptSerializer().Serialize(new
            {
                user = user, titleId = id, title = Library.TitleOf(id), when = DateTime.Now.ToString("s"), note = note ?? "", files = files, bytes = bytes
            }));
            Activity.Add("save", "Backup do save de " + Library.TitleOf(id) + " (" + files + " arquivos)");
            return new BackupInfo { stamp = stamp, when = DateTime.Now.ToString("dd/MM/yyyy HH:mm"), size = bytes, files = files, note = note ?? "" };
        }

        static void CopyUp(string localDir, string remoteDir, ref int files)
        {
            try { Ftp.Run(c => c.Simple("MKD " + remoteDir.TrimEnd('/'), 257, 250, 550)); } catch { }
            foreach (var f in Directory.GetFiles(localDir))
            {
                if (Path.GetFileName(f) == "orbis-backup.json") continue;
                Ftp.Upload(remoteDir + Path.GetFileName(f), File.ReadAllBytes(f));
                files++;
            }
            foreach (var d in Directory.GetDirectories(localDir))
                CopyUp(d, remoteDir + Path.GetFileName(d) + "/", ref files);
        }

        public static int Restore(string user, string id, string stamp)
        {
            Validate(user, id);
            if (!StampRx.IsMatch(stamp ?? "")) throw new Exception("Backup inválido.");
            string dir = Path.Combine(Root, user, id, stamp);
            if (!Directory.Exists(dir)) throw new Exception("Backup não encontrado.");
            // Safety net: snapshot what is on the console right now before overwriting it
            try { Backup(user, id, "Automático, antes de restaurar " + stamp); } catch { }
            int files = 0;
            CopyUp(dir, Remote(user, id), ref files);
            Activity.Add("save", "Save de " + Library.TitleOf(id) + " restaurado (backup de " + stamp + ")");
            return files;
        }

        public static void Delete(string user, string id, string stamp)
        {
            Validate(user, id);
            if (!StampRx.IsMatch(stamp ?? "")) throw new Exception("Backup inválido.");
            string dir = Path.Combine(Root, user, id, stamp);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        public static void Zip(string user, string id, string stamp, Stream output)
        {
            Validate(user, id);
            if (!StampRx.IsMatch(stamp ?? "")) throw new Exception("Backup inválido.");
            string dir = Path.Combine(Root, user, id, stamp);
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
            {
                foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    string rel = f.Substring(dir.Length).TrimStart('\\', '/').Replace('\\', '/');
                    var e = zip.CreateEntry(id + "/" + rel, CompressionLevel.Fastest);
                    using (var es = e.Open())
                    using (var fs = File.OpenRead(f)) fs.CopyTo(es);
                }
            }
        }
    }
}
