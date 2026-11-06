using Avalonia;
namespace ONITON;
class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect()
            // 메뉴·콤보박스 목록을 별도 OS 창이 아니라 같은 창 안에 그린다 → 창 순서/활성화 문제로 메뉴가 안 보이는 현상 방지
            .With(new AvaloniaNativePlatformOptions { OverlayPopups = true })
            .With(new Win32PlatformOptions { OverlayPopups = true })
            .With(new X11PlatformOptions { OverlayPopups = true });
}
