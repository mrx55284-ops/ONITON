using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
namespace ONITON;

/// 빠른 실행: 이름/아이디/한글 초성으로 찾고 Enter = 선택, Ctrl+Enter = 비밀번호 복사
public class LauncherWindow : Window
{
    readonly TextBox _q = new();
    readonly ListBox _list = new();
    readonly List<Entry> _all;
    public Entry? Picked;
    public string Mode = "select";
    bool _ready;

    public LauncherWindow(IEnumerable<Entry> entries)
    {
        _all = entries.ToList();
        _q.Watermark = L.T("launcher_hint2");
        var body = new Grid { Margin = new Thickness(10), RowDefinitions = new RowDefinitions("Auto,*") };
        body.Children.Add(_q);
        _list.Margin = new Thickness(0, 8, 0, 0); Grid.SetRow(_list, 1); body.Children.Add(_list);
        Dlg.Style(this, L.T("m_launcher"), 540, 340);
        Topmost = true; WindowStartupLocation = WindowStartupLocation.Manual;   // 위치는 호출한 쪽에서 "지금 보고 있는 화면"으로 지정
        Content = Dlg.Frame(this, L.T("m_launcher"), body);
        _q.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Filter(); };
        _q.KeyDown += OnKey;
        _list.DoubleTapped += (_, _) => Pick("select");
        Opened += (_, _) => { Native.ExcludeFromCapture(this); Filter(); _q.Focus(); DispatcherTimerOnce(() => _ready = true); };
        Deactivated += (_, _) => { if (_ready) Close(); };   // 다른 곳을 누르면 닫힘
    }

    static void DispatcherTimerOnce(Action a)
    {
        var t = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        t.Tick += (_, _) => { t.Stop(); a(); };
        t.Start();
    }

    void Filter()
    {
        var q = (_q.Text ?? "").Trim();
        var res = _all.Where(e => Match(e, q)).OrderBy(e => e.Site, StringComparer.OrdinalIgnoreCase).Take(8).ToList();
        _list.ItemsSource = res;
        if (res.Count > 0) _list.SelectedIndex = 0;
    }

    static bool Match(Entry e, string q)
    {
        if (q.Length == 0) return true;
        if (e.Site.Contains(q, StringComparison.OrdinalIgnoreCase) || e.Username.Contains(q, StringComparison.OrdinalIgnoreCase)
            || e.Url.Contains(q, StringComparison.OrdinalIgnoreCase)) return true;
        return q.All(c => c >= 0x3131 && c <= 0x314E) && Chosung(e.Site).Contains(q);   // 초성 검색 (예: ㄱㅎㅂ)
    }

    static string Chosung(string s)
    {
        const string T = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
        var sb = new StringBuilder();
        foreach (var c in s) sb.Append(c >= 0xAC00 && c <= 0xD7A3 ? T[(c - 0xAC00) / 588] : c);
        return sb.ToString();
    }

    void Pick(string mode)
    {
        if (_list.SelectedItem is Entry en) { Picked = en; Mode = mode; Close(); }
    }

    void OnKey(object? s, KeyEventArgs e)
    {
        bool ctrl = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        else if (e.Key == Key.Down) { if (_list.ItemCount > 0) _list.SelectedIndex = Math.Min(_list.ItemCount - 1, _list.SelectedIndex + 1); e.Handled = true; }
        else if (e.Key == Key.Up) { if (_list.ItemCount > 0) _list.SelectedIndex = Math.Max(0, _list.SelectedIndex - 1); e.Handled = true; }
        else if (e.Key == Key.Enter) { Pick(ctrl ? "copy" : "select"); e.Handled = true; }
    }
}
