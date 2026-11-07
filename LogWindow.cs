using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
namespace ONITON;

/// 내 보안 로그: 접속/실패/잠금/변조 기록 (암호화된 금고 안에 저장됨)
public class LogWindow : Window
{
    public LogWindow(VaultStore store)
    {
        Title = L.T("log_title"); Width = 640; Height = 420;
        CanResize = false; SystemDecorations = SystemDecorations.None;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brushes.White; Foreground = Brushes.Black;
        FontFamily = new FontFamily("Tahoma, Segoe UI, Malgun Gothic, Apple SD Gothic Neo, Arial"); FontSize = 12; FontWeight = FontWeight.Light;

        var fails = store.Log.Count(e => e.Kind == "login_fail");
        var oks = store.Log.Count(e => e.Kind == "login_ok");
        var sb = new StringBuilder();
        sb.AppendLine(L.F("log_summary", oks, fails)); sb.AppendLine();
        foreach (var e in store.Log.OrderByDescending(x => x.Utc).Take(300))
        {
            var detail = e.Kind == "lock" ? L.T(e.Detail) : e.Detail;
            sb.AppendLine($"{e.Utc.ToLocalTime():yyyy-MM-dd HH:mm:ss}   {L.T("log_" + e.Kind)}   {detail}");
        }
        if (store.Log.Count == 0) sb.Append(L.T("log_empty"));

        var tg = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        tg.Children.Add(new TextBlock { Text = L.T("log_title"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0) });
        var x = new Button { Content = "X", Width = 46, Height = 29, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        x.Classes.Add("flat"); x.Click += (_, _) => Close();
        Grid.SetColumn(x, 1); tg.Children.Add(x);
        var title = new Border { Height = 30, BorderBrush = new SolidColorBrush(Color.Parse("#D0D0D0")), BorderThickness = new Thickness(0, 0, 0, 1), Child = tg };
        title.PointerPressed += (_, e) => { if (!Dlg.OnButton(e) && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e); };

        var text = new SelectableTextBlock { Text = sb.ToString(), Margin = new Thickness(8), FontFamily = new FontFamily("Consolas, Menlo, Courier New, monospace") };
        var scroll = new ScrollViewer { Content = text, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        var ok = new Button { Content = L.T("ok"), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8), Padding = new Thickness(24, 3) };
        ok.Click += (_, _) => Close();

        var root = new Grid { RowDefinitions = new RowDefinitions("30,*,Auto") };
        root.Children.Add(title);
        Grid.SetRow(scroll, 1); root.Children.Add(scroll);
        Grid.SetRow(ok, 2); root.Children.Add(ok);
        Content = new Border { BorderBrush = new SolidColorBrush(Color.Parse("#5A5A5A")), BorderThickness = new Thickness(1), Child = root };
    }
}
