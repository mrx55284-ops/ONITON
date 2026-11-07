using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Controls;
using Avalonia.Threading;
namespace ONITON;

/// Windows 전용 보호 기능 (그 외 OS 에서는 아무 일도 하지 않음). 관리자 권한이 필요 없는 API 만 사용한다.
public static class Native
{
    [DllImport("user32")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32")] static extern bool SetWindowDisplayAffinity(IntPtr h, uint a);
    [DllImport("user32")] static extern bool OpenClipboard(IntPtr h);
    [DllImport("user32")] static extern bool CloseClipboard();
    [DllImport("user32")] static extern bool EmptyClipboard();
    [DllImport("user32")] static extern IntPtr SetClipboardData(uint fmt, IntPtr mem);
    [DllImport("user32", CharSet = CharSet.Unicode)] static extern uint RegisterClipboardFormat(string name);
    [DllImport("kernel32")] static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport("kernel32")] static extern IntPtr GlobalLock(IntPtr h);
    [DllImport("kernel32")] static extern bool GlobalUnlock(IntPtr h);

    [DllImport("libc", EntryPoint = "prctl")] static extern int prctl(int option, ulong a2, ulong a3, ulong a4, ulong a5);
    [DllImport("libc", EntryPoint = "ptrace")] static extern int ptrace_mac(int request, int pid, IntPtr addr, int data);

    /// 같은 사용자의 다른 프로그램이 이 프로세스의 메모리(해독된 비밀번호)를 읽거나 코어 덤프를 만드는 것을 어렵게 한다.
    ///  Linux: PR_SET_DUMPABLE=0 (코어 덤프 금지, /proc/PID/mem · ptrace 접근 차단)   macOS: PT_DENY_ATTACH (디버거/lldb 부착 차단)
    /// 관리자 권한 공격자는 막지 못한다. 실패해도 프로그램은 계속 동작한다.
    public static void HardenProcess()
    {
        try
        {
            if (OperatingSystem.IsLinux()) prctl(4, 0, 0, 0, 0);
            else if (OperatingSystem.IsMacOS()) ptrace_mac(31, 0, IntPtr.Zero, 0);
        }
        catch { }
    }

    public static IntPtr HandleOf(TopLevel w) => w.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

    /// 화면 캡처/녹화/원격 화면 공유에서 이 창이 보이지 않게 한다 (Windows 10 2004+; 이전 버전은 검게 표시)
    public static void ExcludeFromCapture(Window w)
    {
        if (!OperatingSystem.IsWindows()) return;
        var h = HandleOf(w); if (h == IntPtr.Zero) return;
        if (!SetWindowDisplayAffinity(h, 0x11)) SetWindowDisplayAffinity(h, 0x01);
    }

    public static void Restore(Window w)
    {
        if (OperatingSystem.IsWindows()) { var h = HandleOf(w); if (h != IntPtr.Zero && IsIconic(h)) ShowWindow(h, 9); }
        w.WindowState = WindowState.Normal; w.Activate();
    }

    static IntPtr Alloc(byte[] data)
    {
        var h = GlobalAlloc(0x0002 /*MOVEABLE*/, (UIntPtr)data.Length);
        var p = GlobalLock(h); Marshal.Copy(data, 0, p, data.Length); GlobalUnlock(h);
        return h;
    }

    /// 윈도우 클립보드 기록(Win+V)과 클라우드 클립보드에 남지 않도록 표시하고 텍스트를 복사한다
    public static bool SetSecretText(IntPtr owner, string text)
    {
        if (!OperatingSystem.IsWindows() || owner == IntPtr.Zero) return false;
        bool opened = false;
        for (int i = 0; i < 10 && !opened; i++) { opened = OpenClipboard(owner); if (!opened) Thread.Sleep(25); }
        if (!opened) return false;
        try
        {
            EmptyClipboard();
            SetClipboardData(13 /*CF_UNICODETEXT*/, Alloc(Encoding.Unicode.GetBytes(text + "\0")));
            SetClipboardData(RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing"), Alloc(BitConverter.GetBytes(1)));
            SetClipboardData(RegisterClipboardFormat("CanIncludeInClipboardHistory"), Alloc(BitConverter.GetBytes(0)));
            SetClipboardData(RegisterClipboardFormat("CanUploadToCloudClipboard"), Alloc(BitConverter.GetBytes(0)));
            return true;
        }
        finally { CloseClipboard(); }
    }

    public static void ClearClipboard(IntPtr owner)
    {
        if (!OperatingSystem.IsWindows() || owner == IntPtr.Zero) return;
        if (OpenClipboard(owner)) { try { EmptyClipboard(); } finally { CloseClipboard(); } }
    }
}

/// 비밀번호 복사: 클립보드 기록에서 제외 + 일정 시간 뒤(그 사이 내용이 안 바뀌었으면) 자동 삭제
public static class SecretClip
{
    static int _token; static bool _pending;

    public static async Task Copy(TopLevel w, string text, int seconds)
    {
        if (string.IsNullOrEmpty(text)) return;
        bool ok = OperatingSystem.IsWindows() && Native.SetSecretText(Native.HandleOf(w), text);
        if (!ok && w.Clipboard != null) await w.Clipboard.SetTextAsync(text);
        _pending = true; int tok = ++_token;
        await Task.Delay(TimeSpan.FromSeconds(seconds));
        if (tok != _token) return;
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try { if (w.Clipboard != null && await w.Clipboard.GetTextAsync() == text) Clear(w); } catch { }
        });
    }

    public static void Clear(TopLevel w)
    {
        if (!_pending) return;
        _pending = false; _token++;
        try { if (OperatingSystem.IsWindows()) Native.ClearClipboard(Native.HandleOf(w)); else _ = w.Clipboard?.ClearAsync(); } catch { }
    }
}
