namespace ONITON;

public static class Paths
{
    /// 소유자만 읽고 쓰게 (Mac/Linux). Windows 는 사용자 프로필 폴더 권한을 그대로 사용.
    public static void Private(string path, bool dir = false)
    {
        if (OperatingSystem.IsWindows()) return;
        try
        {
            File.SetUnixFileMode(path, dir ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                                           : UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch { }
    }

    /// 앱이 있는 위치(맥 .app 은 .app 이 들어 있는 폴더). 휴대용 데이터는 항상 이 옆의 data 폴더에 둔다.
    static string AppBase()
    {
        var d = AppContext.BaseDirectory;
        var k = d.IndexOf(".app" + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        if (k >= 0) d = System.IO.Path.GetDirectoryName(d.Substring(0, k + 4)) ?? d;
        return d;
    }

    /// USB 같은 외부 저장장치에서 실행 중인지
    static bool OnExternalVolume(string d)
    {
        try
        {
            if (OperatingSystem.IsMacOS()) return d.StartsWith("/Volumes/", StringComparison.Ordinal);
            if (OperatingSystem.IsLinux()) return d.StartsWith("/media/") || d.StartsWith("/run/media/") || d.StartsWith("/mnt/");
            var root = System.IO.Path.GetPathRoot(d);
            return !string.IsNullOrEmpty(root) && new DriveInfo(root).DriveType == DriveType.Removable;
        }
        catch { return false; }
    }

    /// 휴대용 = 금고가 앱 옆 data 폴더에 저장되어, 폴더째 옮기면 금고도 같이 간다. 아래 중 하나면 자동으로 켜진다:
    ///  ① 앱 옆에 이미 data/vault.dat 가 있음 (앱+data 를 통째로 복사해 온 경우)  ② USB 같은 외부 저장장치에서 실행  ③ 앱 옆에 ONITON.portable 파일
    public static bool Portable
    {
        get
        {
            var b = AppBase();
            return File.Exists(System.IO.Path.Combine(b, "data", "vault.dat")) || OnExternalVolume(b) || File.Exists(System.IO.Path.Combine(b, "ONITON.portable"));
        }
    }

    public static string DataDir => Portable
        ? System.IO.Path.Combine(AppBase(), "data")
        : LegacyDir;

    /// 이 컴퓨터(사용자 프로필)의 기본 저장 위치
    public static string LegacyDir => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ONITON");
    public static bool LegacyVaultExists => File.Exists(System.IO.Path.Combine(LegacyDir, "vault.dat"));
}

/// 같은 데이터 폴더로 ONITON 을 두 번 실행하면 나중 저장이 먼저 저장한 내용을 덮어쓸 수 있으므로 두 번째 실행을 막는다.
public static class InstanceLock
{
    static FileStream? _fs;
    public static bool TryAcquire()
    {
        try
        {
            Directory.CreateDirectory(Paths.DataDir);
            _fs = new FileStream(System.IO.Path.Combine(Paths.DataDir, "ONITON.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException) { return false; }   // 이미 다른 ONITON 이 잡고 있음
        catch { return true; }                    // 읽기 전용 USB 등 만들 수 없는 경우는 막지 않는다
    }
}
