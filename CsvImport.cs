using System.Text;
namespace ONITON;

/// 크롬/엣지/파이어폭스/사파리에서 내보낸 CSV 를 읽는다. 첫 줄은 머리글(name,url,username,password,note 등)이며
/// 따옴표 안의 쉼표·줄바꿈·"" 이스케이프를 올바르게 처리한다.
public static class CsvImport
{
    public static List<Entry> Parse(string text)
    {
        var rows = ParseRows(text);
        var result = new List<Entry>();
        if (rows.Count < 2) return result;
        var h = rows[0].Select(x => x.Trim().Trim('\uFEFF').ToLowerInvariant()).ToList();
        int Idx(params string[] names) { foreach (var n in names) { var i = h.IndexOf(n); if (i >= 0) return i; } return -1; }
        int iName = Idx("name", "title"), iUrl = Idx("url", "login_uri", "website"),
            iUser = Idx("username", "login_username", "user"), iPass = Idx("password", "login_password"), iNote = Idx("note", "notes");
        if (iPass < 0) return result;
        foreach (var r in rows.Skip(1))
        {
            string G(int i) => i >= 0 && i < r.Count ? r[i] : "";
            var pass = G(iPass); var user = G(iUser);
            if (pass.Length == 0 && user.Length == 0) continue;
            var url = G(iUrl); var site = G(iName);
            if (site.Length == 0 && url.Length > 0 && Uri.TryCreate(url.Contains("://") ? url : "https://" + url, UriKind.Absolute, out var u)) site = u.Host;
            result.Add(new Entry { Site = site, Url = url, Username = user, Password = pass, Note = G(iNote) });
        }
        return result;
    }

    static List<List<string>> ParseRows(string s)
    {
        var rows = new List<List<string>>(); var row = new List<string>(); var f = new StringBuilder(); bool q = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (q)
            {
                if (c == '"') { if (i + 1 < s.Length && s[i + 1] == '"') { f.Append('"'); i++; } else q = false; }
                else f.Append(c);
            }
            else if (c == '"') q = true;
            else if (c == ',') { row.Add(f.ToString()); f.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < s.Length && s[i + 1] == '\n') i++;
                row.Add(f.ToString()); f.Clear();
                if (row.Count > 1 || row[0].Length > 0) rows.Add(row);
                row = new List<string>();
            }
            else f.Append(c);
        }
        if (f.Length > 0 || row.Count > 0) { row.Add(f.ToString()); rows.Add(row); }
        return rows;
    }
}
