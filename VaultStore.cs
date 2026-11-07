using System.Security.Cryptography;
using System.Text.Json;
namespace ONITON;

[System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
public class Entry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Site { get; set; } = "";
    public string Url { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Note { get; set; } = "";
    public override string ToString() => $"{Site}  ({Username})";
}

[System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
public class LogEvent
{
    public DateTime Utc { get; set; }
    public string Kind { get; set; } = "";
    public string Detail { get; set; } = "";
}

[System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
public class VaultData
{
    public List<Entry> Entries { get; set; } = new();
    public List<LogEvent> Log { get; set; } = new();
}

public class VaultStore
{
    public string Path { get; }
    public List<Entry> Entries { get; private set; } = new();
    public List<LogEvent> Log { get; private set; } = new();
    public bool IsUnlocked => _key != null;
    public bool Exists => File.Exists(Path);
    public bool NeedsKeyFile => Exists && VaultCrypto.PeekNeedsKey(Path);
    byte[]? _key, _salt, _pepper; byte _flags; uint _mem = VaultCrypto.DefMem, _iter = VaultCrypto.DefIter, _par = VaultCrypto.DefPar;

    public VaultStore(string path) { Path = path; }

    public void AddLog(string kind, string detail = "")
    {
        Log.Add(new LogEvent { Utc = DateTime.UtcNow, Kind = kind, Detail = detail });
        if (Log.Count > 1000) Log.RemoveRange(0, Log.Count - 1000);
    }

    void NewKey(string master)
    {
        _salt = RandomNumberGenerator.GetBytes(32);
        _mem = VaultCrypto.DefMem; _iter = VaultCrypto.DefIter; _par = VaultCrypto.DefPar;
        _key = VaultCrypto.DeriveKey(master, _pepper, _salt, _mem, _iter, _par);
    }

    public void Create(string master, byte[]? keyFile)
    {
        _pepper = keyFile == null ? null : VaultCrypto.PepperFromKeyFile(keyFile);
        _flags = (byte)(_pepper != null ? 1 : 0);
        NewKey(master);
        Entries = new(); Log = new();
        Save();
    }

    public void Unlock(string master, byte[]? keyFile)
    {
        if (new FileInfo(Path).Length > VaultCrypto.MaxFileBytes) throw new InvalidDataException("too large");
        var file = File.ReadAllBytes(Path);
        var h = VaultCrypto.Parse(file);
        var pepper = h.NeedsKey ? (keyFile == null ? throw new CryptographicException("key file required") : VaultCrypto.PepperFromKeyFile(keyFile)) : null;
        var key = VaultCrypto.DeriveKey(master, pepper, h.Salt, h.Mem, h.Iter, h.Par);
        var plain = VaultCrypto.Decrypt(h, key);   // 실패 시 CryptographicException
        VaultData data;
        using (var doc = JsonDocument.Parse(plain))
        {
            data = doc.RootElement.ValueKind == JsonValueKind.Array
                ? new VaultData { Entries = JsonSerializer.Deserialize<List<Entry>>(plain) ?? new() }
                : JsonSerializer.Deserialize<VaultData>(plain) ?? new();
        }
        CryptographicOperations.ZeroMemory(plain);
        Entries = data.Entries; Log = data.Log;
        _pepper = pepper; _flags = h.Flags; _salt = h.Salt; _key = key; _mem = h.Mem; _iter = h.Iter; _par = h.Par;
        // 옛 형식이거나 KDF 강도가 지금 기준보다 약하면, 로그인에 성공한 이 순간(비밀번호를 알고 있는 때)에 새 salt + 강한 값으로 다시 유도한다.
        // 다음 Save 때 새 파라미터로 저장된다.
        if (h.Version == 1 || h.Mem < VaultCrypto.DefMem || h.Iter < VaultCrypto.DefIter || h.Par < VaultCrypto.DefPar)
        { CryptographicOperations.ZeroMemory(_key); NewKey(master); }   // 옛 형식 → 새 salt(32바이트)로 다시 유도, 다음 Save 때 v2 로 저장됨
    }

    const int KeepBackups = 5;
    string Bak(int i) => Path + ".bak" + i;

    /// 최근 백업 5개(.bak1 = 가장 최근)를 돌려가며 보관. force=false 이면 마지막 백업이 1시간 이내일 때는 만들지 않는다.
    void RotateBackups(bool force)
    {
        if (!File.Exists(Path)) return;
        var b1 = Bak(1);
        if (!force && File.Exists(b1) && DateTime.UtcNow - File.GetLastWriteTimeUtc(b1) < TimeSpan.FromHours(1)) return;
        try
        {
            File.Delete(Bak(KeepBackups));
            for (int i = KeepBackups - 1; i >= 1; i--) if (File.Exists(Bak(i))) File.Move(Bak(i), Bak(i + 1), true);
            File.Copy(Path, b1, true); File.SetLastWriteTimeUtc(b1, DateTime.UtcNow); Paths.Private(b1);
        }
        catch { }
    }

    /// 지금 파일을 즉시 백업 (금고 가져오기 직전 등)
    public void ForceBackup() => RotateBackups(true);

    /// 모든 백업 삭제 (옛 비밀번호로 열리는 백업이 남지 않게). 옛 버전의 .bak 도 함께 지운다.
    void DeleteBackups()
    {
        for (int i = 1; i <= KeepBackups; i++) { try { File.Delete(Bak(i)); } catch { } }
        try { File.Delete(Path + ".bak"); } catch { }
    }

    /// 안전한 저장: ① 임시 파일에 쓰고 디스크까지 반영 → ② 다시 읽어 복호화해 내용이 같은지 검증 → ③ 백업 회전 → ④ 원본 교체.
    /// 검증이 실패하면 원본은 그대로 남는다.
    public void Save()
    {
        if (_key == null || _salt == null) throw new InvalidOperationException();
        var dir = System.IO.Path.GetDirectoryName(Path)!; Directory.CreateDirectory(dir); Paths.Private(dir, true);
        var plain = JsonSerializer.SerializeToUtf8Bytes(new VaultData { Entries = Entries, Log = Log });
        var tmp = Path + ".tmp";
        try
        {
            var enc = VaultCrypto.Encrypt(plain, _key, _salt, _flags, _mem, _iter, _par);
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            { fs.Write(enc, 0, enc.Length); fs.Flush(true); }
            Paths.Private(tmp);
            var back = VaultCrypto.Decrypt(VaultCrypto.Parse(File.ReadAllBytes(tmp)), _key);
            bool same = CryptographicOperations.FixedTimeEquals(back, plain);
            CryptographicOperations.ZeroMemory(back);
            if (!same) throw new IOException("verify failed");
            RotateBackups(false);
            File.Move(tmp, Path, true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
        Paths.Private(Path);
    }

    /// 현재 아이디/비밀번호가 맞는지 확인 (마스터 변경 전에 사용)
    public bool VerifyMaster(string master)
    {
        if (_key == null || _salt == null) return false;
        var k = VaultCrypto.DeriveKey(master, _pepper, _salt, _mem, _iter, _par);
        var ok = CryptographicOperations.FixedTimeEquals(k, _key);
        CryptographicOperations.ZeroMemory(k);
        return ok;
    }

    /// 새 salt + 새 키로 다시 암호화. 옛 마스터로 암호화된 .bak 은 즉시 삭제한다 (유출된 옛 비밀번호로 열리지 않도록)
    public void ChangeMaster(string newMaster)
    {
        if (_key == null) throw new InvalidOperationException();
        CryptographicOperations.ZeroMemory(_key);
        NewKey(newMaster);
        Save();
        DeleteBackups();
    }

    public void Lock()
    {
        if (_key != null) CryptographicOperations.ZeroMemory(_key);
        if (_pepper != null) CryptographicOperations.ZeroMemory(_pepper);
        _key = null; _salt = null; _pepper = null;
        Entries = new(); Log = new();
    }
}
