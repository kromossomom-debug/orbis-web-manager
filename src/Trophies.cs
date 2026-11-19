using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace OrbisWebManager
{
    /// <summary>
    /// Trophy progress, read from the console's own SQLite database
    /// (/user/home/USERID/trophy/db/trophy_local.db). Column names are matched loosely
    /// because they vary a little between firmware versions.
    /// </summary>
    public static class Trophies
    {
        static readonly Regex UserRx = new Regex("^[0-9a-fA-F]{8}$");

        static string LocalPath(string user) { return Path.Combine(Program.CacheDir, "trophy_" + user + ".db"); }

        static byte[] Fetch(string user, bool refresh)
        {
            string local = LocalPath(user);
            string remote = "/user/home/" + user + "/trophy/db/trophy_local.db";
            long size = -1;
            try { size = Ftp.Run(c => c.Size(remote)); } catch { }
            if (size < 0) throw new Exception("Não encontrei o banco de troféus deste usuário no PS4.");
            if (refresh || !File.Exists(local) || new FileInfo(local).Length != size) Ftp.DownloadToFile(remote, local);
            return File.ReadAllBytes(local);
        }

        static string Col(Dictionary<string, object> row, params string[] names)
        {
            foreach (var n in names) if (row.ContainsKey(n)) return SqliteDb.S(row, n);
            return "";
        }

        static long Num(Dictionary<string, object> row, params string[] names)
        {
            foreach (var n in names) if (row.ContainsKey(n)) return SqliteDb.L(row, n);
            return 0;
        }

        public static object Schema(string user)
        {
            if (!UserRx.IsMatch(user ?? "")) throw new Exception("Usuário inválido.");
            var db = new SqliteDb(Fetch(user, false));
            var tables = new List<object>();
            foreach (var t in db.Tables)
            {
                var rows = db.Rows(t.Key);
                tables.Add(new { name = t.Key, columns = t.Value.Value, rows = rows.Count, sample = rows.Count > 0 ? rows[0] : null });
            }
            return tables;
        }

        public static object Get(string user, bool refresh)
        {
            if (!UserRx.IsMatch(user ?? "")) throw new Exception("Usuário inválido.");
            var db = new SqliteDb(Fetch(user, refresh));
            string titleTable = null;
            foreach (var name in db.TableNames())
                if (name.IndexOf("trophy_title", StringComparison.OrdinalIgnoreCase) >= 0 && name.IndexOf("entry", StringComparison.OrdinalIgnoreCase) < 0) { titleTable = name; break; }
            if (titleTable == null) throw new Exception("Formato do banco de troféus não reconhecido.");

            var games = new List<object>();
            long plat = 0, gold = 0, silver = 0, bronze = 0;
            foreach (var r in db.Rows(titleTable))
            {
                string name = Col(r, "title", "title_name", "name", "trophy_title_name").Trim();
                if (name.Length == 0) continue;
                long total = Num(r, "trophy_num", "total_trophy_num", "trophy_count");
                long unlocked = Num(r, "unlocked_trophy_num", "unlocked_num", "unlocked_count");
                long progress = Num(r, "progress");
                if (progress == 0 && total > 0) progress = unlocked * 100 / total;
                long p = Num(r, "unlocked_platinum_num"), g = Num(r, "unlocked_gold_num"), s = Num(r, "unlocked_silver_num"), b = Num(r, "unlocked_bronze_num");
                plat += p; gold += g; silver += s; bronze += b;
                string npwr = Col(r, "trophy_title_id", "np_comm_id", "npcommid");
                string titleId = "";
                string last = Col(r, "time_last_unlocked", "last_unlocked_time", "time_last_update_uc", "time_last_update");
                // Match the trophy set to an installed game by name (the db doesn't store the CUSA id)
                foreach (var gi in Library.Games())
                    if (string.Equals(gi.name, name, StringComparison.OrdinalIgnoreCase)) { titleId = gi.id; break; }
                games.Add(new
                {
                    name = name,
                    npCommId = npwr,
                    titleId = titleId,
                    iconUrl = NpRx.IsMatch(npwr) ? "/api/trophies/icon?np=" + npwr : (titleId.Length > 0 ? Library.IconUrl(titleId) : ""),
                    total = total,
                    unlocked = unlocked,
                    progress = Math.Min(100, progress),
                    platinum = p > 0,
                    lastUnlocked = FormatDate(last),
                    sortKey = last
                });
            }
            games.Sort((a, c) => string.CompareOrdinal(Convert.ToString(c.GetType().GetProperty("sortKey").GetValue(c, null)), Convert.ToString(a.GetType().GetProperty("sortKey").GetValue(a, null))));
            return new { games = games, totals = new { platinum = plat, gold = gold, silver = silver, bronze = bronze } };
        }

        static readonly Regex NpRx = new Regex("^NPWR\\d{5}_\\d{2}$");

        /// <summary>ICON0.PNG from /user/trophy/conf/NPWRxxxxx_00/TROPHY.TRP (only the header and the icon are read).</summary>
        public static byte[] Icon(string np)
        {
            if (!NpRx.IsMatch(np ?? "")) return null;
            string dir = Path.Combine(Program.CacheDir, "trophy-icons");
            Directory.CreateDirectory(dir);
            string local = Path.Combine(dir, np + ".png");
            if (File.Exists(local)) return File.ReadAllBytes(local);
            string remote = "/user/trophy/conf/" + np + "/TROPHY.TRP";
            byte[] head = Ftp.Run(c => c.RetrieveRange(remote, 0, 0x60));
            if (head.Length < 0x60 || head[0] != 0xDC || head[1] != 0xA2 || head[2] != 0x4D || head[3] != 0x00) throw new Exception("cabeçalho inválido (" + head.Length + " bytes)");
            int version = (head[4] << 24) | (head[5] << 16) | (head[6] << 8) | head[7];
            int count = (head[0x10] << 24) | (head[0x11] << 16) | (head[0x12] << 8) | head[0x13];
            int entrySize = (head[0x14] << 24) | (head[0x15] << 16) | (head[0x16] << 8) | head[0x17];
            if (count <= 0 || count > 2000 || entrySize < 48 || entrySize > 256) return null;
            int tableStart = version >= 3 ? 0x60 : 0x40;   // v3 (PS4) has a longer header
            // Read from the start instead of REST+RETR: some FTP servers ignore REST on aborted reads.
            byte[] all = Ftp.Run(c => c.RetrieveRange(remote, 0, tableStart + count * entrySize));
            if (all.Length < tableStart + count * entrySize) throw new Exception("tabela incompleta");
            byte[] table = new byte[count * entrySize];
            Buffer.BlockCopy(all, tableStart, table, 0, table.Length);
            for (int i = 0; i < count; i++)
            {
                int p = i * entrySize;
                if (p + 48 > table.Length) break;
                string name = System.Text.Encoding.ASCII.GetString(table, p, 32).TrimEnd('\0');
                if (!name.Equals("ICON0.PNG", StringComparison.OrdinalIgnoreCase)) continue;
                long off = 0, size = 0;
                for (int k = 0; k < 8; k++) { off = (off << 8) | table[p + 32 + k]; size = (size << 8) | table[p + 40 + k]; }
                if (size <= 0 || size > 4 * 1024 * 1024) return null;
                byte[] png = Ftp.Run(c => c.RetrieveRange(remote, off, (int)size));
                if (png.Length < 8 || png[1] != 'P')
                {
                    // REST not honoured: read up to the end of the icon (icons sit near the start of the file)
                    if (off + size > 16 * 1024 * 1024) throw new Exception("ícone muito longe no arquivo");
                    byte[] upto = Ftp.Run(c => c.RetrieveRange(remote, 0, (int)(off + size)));
                    png = new byte[size];
                    Buffer.BlockCopy(upto, (int)off, png, 0, (int)size);
                }
                if (png.Length < 8 || png[1] != 'P' || png[2] != 'N' || png[3] != 'G') throw new Exception("ícone inválido (" + png.Length + " bytes)");
                File.WriteAllBytes(local, png);
                return png;
            }
            return null;
        }

        static string FormatDate(string s)
        {
            DateTime d;
            if (DateTime.TryParse(s, out d) && d.Year > 2000) return d.ToString("dd/MM/yyyy");
            return "";
        }
    }
}
