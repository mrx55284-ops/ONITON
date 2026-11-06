using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
namespace ONITON;
[System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = false)]
public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
        {
            if (InstanceLock.TryAcquire()) d.MainWindow = new MainWindow();
            else { L.LoadPacks(); L.Load(); var w = new TextWindow("ONITON", L.T("err_running")); w.WindowStartupLocation = WindowStartupLocation.CenterScreen; d.MainWindow = w; }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
