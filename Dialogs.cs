using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
namespace ONITON;

/// 클래식 폼 느낌의 대화상자 공통 틀 (사각형 제목 표시줄 + 테두리)
public static class Dlg
{
    public static void Style(Window w, string title, double width, double height)
    {
        w.Title = title; w.Width = width; w.Height = height;
        w.CanResize = false; w.SystemDecorations = SystemDecorations.None;
        w.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        w.Opened += (_, _) => w.Activate();
        w.Background = new SolidColorBrush(Color.Parse("#F0F0F0")); w.Foreground = Brushes.Black;
        w.FontFamily = new FontFamily("Tahoma, Segoe UI, Malgun Gothic, Apple SD Gothic Neo, Arial");
        w.FontSize = 12; w.FontWeight = FontWeight.Light;
        w.FlowDirection = L.Rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }

    public static bool OnButton(Avalonia.Input.PointerPressedEventArgs e) =>
        e.Source is Avalonia.Visual v && Avalonia.VisualTree.VisualExtensions.FindAncestorOfType<Button>(v, true) != null;

    public static Border Frame(Window w, string title, Control body)
    {
        var tg = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        tg.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0) });
        var x = new Button { Content = "X", Width = 46, Height = 29, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        x.Classes.Add("flat"); x.Click += (_, _) => w.Close();
        Grid.SetColumn(x, 1); tg.Children.Add(x);
        var bar = new Border { Height = 30, Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.Parse("#D0D0D0")),
            BorderThickness = new Thickness(0, 0, 0, 1), Child = tg };
        bar.PointerPressed += (_, e) => { if (!OnButton(e) && e.GetCurrentPoint(w).Properties.IsLeftButtonPressed) w.BeginMoveDrag(e); };
        var root = new Grid { RowDefinitions = new RowDefinitions("30,*") };
        root.Children.Add(bar); Grid.SetRow(body, 1); root.Children.Add(body);
        return new Border { BorderBrush = new SolidColorBrush(Color.Parse("#5A5A5A")), BorderThickness = new Thickness(1), Child = root };
    }
}

/// 비밀번호 생성기 (암호학적 난수 사용)
public class GeneratorWindow : Window
{
    readonly TextBox _out = new() { IsReadOnly = true, FontFamily = new FontFamily("Consolas, Menlo, Courier New, monospace") };
    readonly NumericUpDown _len = new() { Minimum = 8, Maximum = 64, Increment = 1, FormatString = "0", Width = 120, HorizontalAlignment = HorizontalAlignment.Left };
    readonly CheckBox _up = new(), _lo = new(), _di = new(), _sy = new();

    public GeneratorWindow(bool canUse)
    {
        _len.Value = 20;
        _up.Content = L.T("gen_up"); _lo.Content = L.T("gen_lo"); _di.Content = L.T("gen_di"); _sy.Content = L.T("gen_sy");
        _up.IsChecked = _lo.IsChecked = _di.IsChecked = _sy.IsChecked = true;
        var make = new Button { Content = L.T("gen_make") }; var copy = new Button { Content = L.T("copy") };
        var use = new Button { Content = L.T("gen_use"), IsVisible = canUse }; var close = new Button { Content = L.T("close") };
        make.Click += (_, _) => Make();
        copy.Click += async (_, _) => await SecretClip.Copy(this, _out.Text ?? "", 15);   // 클립보드 기록에 남기지 않고 15초 뒤 삭제
        use.Click += (_, _) => Close(_out.Text);
        close.Click += (_, _) => Close();
        _len.ValueChanged += (_, _) => Make();
        foreach (var c in new[] { _up, _lo, _di, _sy }) c.IsCheckedChanged += (_, _) => Make();

        var btns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 6, 0, 0) };
        btns.Children.Add(make); btns.Children.Add(copy); btns.Children.Add(use); btns.Children.Add(close);
        var body = new StackPanel { Margin = new Thickness(16), Spacing = 6 };
        body.Children.Add(new TextBlock { Text = L.T("gen_len") }); body.Children.Add(_len);
        body.Children.Add(_up); body.Children.Add(_lo); body.Children.Add(_di); body.Children.Add(_sy);
        body.Children.Add(_out); body.Children.Add(btns);
        Dlg.Style(this, L.T("gen_title"), 400, 380);
        Content = Dlg.Frame(this, L.T("gen_title"), body);
        Make();
    }

    void Make() => _out.Text = Generate((int)(_len.Value ?? 20), _up.IsChecked == true, _lo.IsChecked == true, _di.IsChecked == true, _sy.IsChecked == true);

    public static string Generate(int len, bool up, bool lo, bool di, bool sy)
    {
        var sets = new List<string>();
        if (up) sets.Add("ABCDEFGHJKLMNPQRSTUVWXYZ");
        if (lo) sets.Add("abcdefghijkmnopqrstuvwxyz");
        if (di) sets.Add("23456789");
        if (sy) sets.Add("!@#$%^&*()-_=+[]{}?");
        if (sets.Count == 0) sets.Add("abcdefghijkmnopqrstuvwxyz");
        var all = string.Concat(sets);
        var ch = new char[len];
        for (int i = 0; i < len; i++)
            ch[i] = i < sets.Count ? sets[i][RandomNumberGenerator.GetInt32(sets[i].Length)] : all[RandomNumberGenerator.GetInt32(all.Length)];
        for (int i = len - 1; i > 0; i--) { int j = RandomNumberGenerator.GetInt32(i + 1); (ch[i], ch[j]) = (ch[j], ch[i]); }
        return new string(ch);
    }
}

/// 아이디/비밀번호 변경 대화상자 → string[5] {현재ID, 현재PW, 새ID, 새PW, 새PW확인}
public class ChangeMasterWindow : Window
{
    public ChangeMasterWindow()
    {
        var f = new TextBox[5];
        var labels = new[] { "chg_cur_id", "chg_cur_pw", "chg_new_id", "chg_new_pw", "chg_conf" };
        var body = new StackPanel { Margin = new Thickness(16), Spacing = 3 };
        for (int i = 0; i < 5; i++)
        {
            f[i] = new TextBox { PasswordChar = i == 0 || i == 2 ? '\0' : '●' };
            body.Children.Add(new TextBlock { Text = L.T(labels[i]), Margin = new Thickness(0, i == 2 ? 8 : 0, 0, 0) });
            body.Children.Add(f[i]);
        }
        var ok = new Button { Content = L.T("chg_ok") }; var cancel = new Button { Content = L.T("cancel") };
        ok.Click += (_, _) => Close(f.Select(x => x.Text ?? "").ToArray());
        cancel.Click += (_, _) => Close();
        var btns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 10, 0, 0) };
        btns.Children.Add(ok); btns.Children.Add(cancel); body.Children.Add(btns);
        Dlg.Style(this, L.T("chg_title"), 380, 430);
        Content = Dlg.Frame(this, L.T("chg_title"), body);
    }
}

/// 읽기 전용 안내 창 (사용법 / 정보)
public class TextWindow : Window
{
    public TextWindow(string title, string text)
    {
        var tb = new SelectableTextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(14, 10) };
        var sv = new ScrollViewer { Content = tb, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        var ok = new Button { Content = L.T("ok"), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10), Padding = new Thickness(24, 3) };
        ok.Click += (_, _) => Close();
        var body = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        body.Children.Add(sv); Grid.SetRow(ok, 1); body.Children.Add(ok);
        Dlg.Style(this, title, 620, 540);
        Content = Dlg.Frame(this, title, body);
    }
}

/// 예/아니오 선택 창 (예 → true)
public class ChoiceWindow : Window
{
    public ChoiceWindow(string title, string text, string yes, string no)
    {
        var tb = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 12) };
        var y = new Button { Content = yes, Padding = new Thickness(18, 4) };
        var n = new Button { Content = no, Padding = new Thickness(18, 4) };
        y.Click += (_, _) => Close(true); n.Click += (_, _) => Close(false);
        var btns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12) };
        btns.Children.Add(y); btns.Children.Add(n);
        var body = new StackPanel(); body.Children.Add(tb); body.Children.Add(btns);
        Dlg.Style(this, title, 480, 270);
        Content = Dlg.Frame(this, title, body);
    }
}
