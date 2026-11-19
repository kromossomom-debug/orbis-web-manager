using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace OrbisWebManager
{
    /// <summary>
    /// Tiny read-only SQLite reader (tables only, no indexes/WAL). Enough to read the PS4's own
    /// databases (app.db, trophy db) that we download over FTP, without shipping a native library.
    /// </summary>
    public class SqliteDb
    {
        readonly byte[] db;
        readonly int pageSize, usable;
        public readonly Dictionary<string, KeyValuePair<int, string[]>> Tables =
            new Dictionary<string, KeyValuePair<int, string[]>>(StringComparer.OrdinalIgnoreCase);

        public SqliteDb(byte[] bytes)
        {
            db = bytes;
            if (db.Length < 100 || Encoding.ASCII.GetString(db, 0, 15) != "SQLite format 3")
                throw new Exception("Arquivo não é um banco SQLite.");
            pageSize = (db[16] << 8) | db[17];
            if (pageSize == 1) pageSize = 65536;
            usable = pageSize - db[20];
            foreach (var row in ReadTree(1))
            {
                if (row.Length < 5 || !"table".Equals(row[0] as string)) continue;
                string name = row[1] as string;
                int root = Convert.ToInt32(row[3]);
                Tables[name] = new KeyValuePair<int, string[]>(root, ParseColumns(row[4] as string ?? ""));
            }
        }

        public List<string> TableNames() { return new List<string>(Tables.Keys); }

        /// <summary>Rows as column-name dictionaries. INTEGER PRIMARY KEY columns get the rowid.</summary>
        public List<Dictionary<string, object>> Rows(string table)
        {
            var result = new List<Dictionary<string, object>>();
            KeyValuePair<int, string[]> t;
            if (!Tables.TryGetValue(table, out t)) return result;
            foreach (var r in ReadTree(t.Key, true))
            {
                var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                long rowid = (long)r[r.Length - 1];
                for (int i = 0; i < t.Value.Length; i++)
                {
                    object v = i < r.Length - 1 ? r[i] : null;
                    if (v == null && i == 0) v = rowid;
                    d[t.Value[i]] = v;
                }
                result.Add(d);
            }
            return result;
        }

        static string[] ParseColumns(string sql)
        {
            int a = sql.IndexOf('('), b = sql.LastIndexOf(')');
            if (a < 0 || b <= a) return new string[0];
            string body = sql.Substring(a + 1, b - a - 1);
            var cols = new List<string>();
            int depth = 0, start = 0;
            for (int i = 0; i <= body.Length; i++)
            {
                char c = i < body.Length ? body[i] : ',';
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ',' && depth == 0)
                {
                    string part = body.Substring(start, i - start).Trim();
                    start = i + 1;
                    if (part.Length == 0) continue;
                    string first = Regex.Match(part, "^[\"`\\[]?([^\"`\\]\\s]+)").Groups[1].Value;
                    string up = first.ToUpperInvariant();
                    if (up == "PRIMARY" || up == "UNIQUE" || up == "CHECK" || up == "FOREIGN" || up == "CONSTRAINT") continue;
                    cols.Add(first);
                }
            }
            return cols.ToArray();
        }

        // ---------------------------------------------------------------- b-tree walk

        List<object[]> ReadTree(int page, bool withRowId = false)
        {
            var rows = new List<object[]>();
            var stack = new Stack<int>();
            stack.Push(page);
            int guard = 0;
            while (stack.Count > 0 && guard++ < 1000000)
            {
                int pg = stack.Pop();
                int off = (pg - 1) * pageSize;
                int hdr = pg == 1 ? off + 100 : off;
                if (hdr + 8 > db.Length) continue;
                byte type = db[hdr];
                int cells = (db[hdr + 3] << 8) | db[hdr + 4];
                bool interior = type == 0x05;
                int ptrs = hdr + (interior ? 12 : 8);
                if (type == 0x05)
                {
                    // push right-most first so children come out in order
                    var kids = new List<int>();
                    for (int i = 0; i < cells; i++)
                    {
                        int cell = off + ((db[ptrs + i * 2] << 8) | db[ptrs + i * 2 + 1]);
                        kids.Add(BE32(cell));
                    }
                    kids.Add(BE32(hdr + 8));
                    for (int i = kids.Count - 1; i >= 0; i--) stack.Push(kids[i]);
                }
                else if (type == 0x0D)
                {
                    for (int i = 0; i < cells; i++)
                    {
                        int p = off + ((db[ptrs + i * 2] << 8) | db[ptrs + i * 2 + 1]);
                        long payload = Varint(ref p);
                        long rowid = Varint(ref p);
                        byte[] rec = Payload(p, payload);
                        var vals = Record(rec);
                        if (withRowId) { Array.Resize(ref vals, vals.Length + 1); vals[vals.Length - 1] = rowid; }
                        rows.Add(vals);
                    }
                }
            }
            return rows;
        }

        byte[] Payload(int p, long size)
        {
            int maxLocal = usable - 35;
            if (size <= maxLocal)
            {
                var b = new byte[size];
                Buffer.BlockCopy(db, p, b, 0, (int)size);
                return b;
            }
            int minLocal = (usable - 12) * 32 / 255 - 23;
            int local = (int)(minLocal + (size - minLocal) % (usable - 4));
            if (local > maxLocal) local = minLocal;
            var outp = new byte[size];
            Buffer.BlockCopy(db, p, outp, 0, local);
            int done = local;
            int next = BE32(p + local);
            while (done < size && next > 0)
            {
                int po = (next - 1) * pageSize;
                if (po + 4 > db.Length) break;
                next = BE32(po);
                int n = (int)Math.Min(usable - 4, size - done);
                Buffer.BlockCopy(db, po + 4, outp, done, n);
                done += n;
            }
            return outp;
        }

        static object[] Record(byte[] r)
        {
            int p = 0;
            long hlen = Varint(r, ref p);
            var types = new List<long>();
            while (p < hlen) types.Add(Varint(r, ref p));
            var vals = new object[types.Count];
            int d = (int)hlen;
            for (int i = 0; i < types.Count; i++)
            {
                long t = types[i];
                switch (t)
                {
                    case 0: vals[i] = null; break;
                    case 1: vals[i] = (long)(sbyte)r[d]; d += 1; break;
                    case 2: vals[i] = (long)(short)((r[d] << 8) | r[d + 1]); d += 2; break;
                    case 3: { long v = (r[d] << 16) | (r[d + 1] << 8) | r[d + 2]; if ((v & 0x800000) != 0) v -= 0x1000000; vals[i] = v; d += 3; break; }
                    case 4: vals[i] = (long)(int)((r[d] << 24) | (r[d + 1] << 16) | (r[d + 2] << 8) | r[d + 3]); d += 4; break;
                    case 5: { long v = 0; for (int k = 0; k < 6; k++) v = (v << 8) | r[d + k]; if ((v & 0x800000000000) != 0) v -= 0x1000000000000; vals[i] = v; d += 6; break; }
                    case 6: { long v = 0; for (int k = 0; k < 8; k++) v = (v << 8) | r[d + k]; vals[i] = v; d += 8; break; }
                    case 7: { var b = new byte[8]; for (int k = 0; k < 8; k++) b[k] = r[d + 7 - k]; vals[i] = BitConverter.ToDouble(b, 0); d += 8; break; }
                    case 8: vals[i] = 0L; break;
                    case 9: vals[i] = 1L; break;
                    default:
                        if (t >= 12 && t % 2 == 0) { int n = (int)((t - 12) / 2); var b = new byte[n]; Buffer.BlockCopy(r, d, b, 0, n); vals[i] = b; d += n; }
                        else if (t >= 13) { int n = (int)((t - 13) / 2); vals[i] = Encoding.UTF8.GetString(r, d, n); d += n; }
                        break;
                }
            }
            return vals;
        }

        int BE32(int p) { return (db[p] << 24) | (db[p + 1] << 16) | (db[p + 2] << 8) | db[p + 3]; }

        long Varint(ref int p) { return Varint(db, ref p); }

        static long Varint(byte[] b, ref int p)
        {
            long v = 0;
            for (int i = 0; i < 9; i++)
            {
                byte c = b[p++];
                if (i == 8) return (v << 8) + c;
                v = (v << 7) + (c & 0x7F);
                if ((c & 0x80) == 0) return v;
            }
            return v;
        }

        // ---------------------------------------------------------------- helpers

        public static string S(Dictionary<string, object> row, string col)
        {
            object v;
            if (!row.TryGetValue(col, out v) || v == null) return "";
            var b = v as byte[];
            return b != null ? Encoding.UTF8.GetString(b).TrimEnd('\0') : v.ToString();
        }

        public static long L(Dictionary<string, object> row, string col)
        {
            object v;
            if (!row.TryGetValue(col, out v) || v == null) return 0;
            if (v is long) return (long)v;
            if (v is double) return (long)(double)v;
            long r;
            long.TryParse(v.ToString(), out r);
            return r;
        }
    }
}
