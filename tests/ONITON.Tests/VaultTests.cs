using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Konscious.Security.Cryptography;
using Xunit;
namespace ONITON.Tests;

public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "oniton-test-" + Guid.NewGuid().ToString("N"));
    public TempDir() => Directory.CreateDirectory(Path);
    public string File(string name) => System.IO.Path.Combine(Path, name);
    public void Dispose() { try { Directory.Delete(Path, true); } catch { } }
}

public class VaultTests
{
    const string M = "master-pass-1";

    static VaultStore NewVault(TempDir d, byte[]? key = null, int entries = 1)
    {
        var s = new VaultStore(d.File("vault.dat"));
        s.Create(M, key);
        for (int i = 0; i < entries; i++) s.Entries.Add(new Entry { Site = "s" + i, Username = "u" + i, Password = "p" + i });
        s.Save();
        return s;
    }

    [Fact]
    public void CreateSaveUnlock_RoundTrip()
    {
        using var d = new TempDir();
        NewVault(d, null, 3);
        var s2 = new VaultStore(d.File("vault.dat"));
        s2.Unlock(M, null);
        Assert.Equal(3, s2.Entries.Count);
        Assert.Equal("p2", s2.Entries[2].Password);
        Assert.StartsWith("ONV2", Encoding.ASCII.GetString(File.ReadAllBytes(d.File("vault.dat")), 0, 4));
    }

    [Fact]
    public void WrongPassword_Fails_AndLockClearsMemory()
    {
        using var d = new TempDir();
        var s = NewVault(d);
        Assert.ThrowsAny<CryptographicException>(() => new VaultStore(d.File("vault.dat")).Unlock("nope", null));
        s.Lock();
        Assert.False(s.IsUnlocked);
        Assert.Empty(s.Entries);
    }

    [Theory]
    [InlineData(4)]    // 플래그
    [InlineData(5)]    // KDF 값(메모리 낮은 바이트)
    [InlineData(7)]    // KDF 값(메모리 높은 바이트 → 범위 검증)
    [InlineData(20)]   // salt
    [InlineData(52)]   // nonce
    [InlineData(65)]   // tag
    [InlineData(90)]   // 암호문
    public void TamperedFile_IsRejected(int offset)
    {
        using var d = new TempDir();
        NewVault(d);
        var b = File.ReadAllBytes(d.File("vault.dat"));
        b[offset] ^= 0x01;
        File.WriteAllBytes(d.File("vault.dat"), b);
        Assert.ThrowsAny<Exception>(() => new VaultStore(d.File("vault.dat")).Unlock(M, null));
    }

    [Fact]
    public void TruncatedOrGarbageFile_IsRejected()
    {
        using var d = new TempDir();
        File.WriteAllBytes(d.File("vault.dat"), new byte[] { 1, 2, 3, 4, 5 });
        Assert.ThrowsAny<Exception>(() => new VaultStore(d.File("vault.dat")).Unlock(M, null));
        File.WriteAllBytes(d.File("vault.dat"), Encoding.ASCII.GetBytes("ONV2").Concat(new byte[200]).ToArray());   // 파라미터가 0 → 거부
        Assert.ThrowsAny<Exception>(() => new VaultStore(d.File("vault.dat")).Unlock(M, null));
    }

    [Fact]
    public void KeyFile_IsRequired()
    {
        using var d = new TempDir();
        var key = RandomNumberGenerator.GetBytes(64);
        NewVault(d, key);
        var s = new VaultStore(d.File("vault.dat"));
        Assert.True(s.NeedsKeyFile);
        Assert.ThrowsAny<CryptographicException>(() => s.Unlock(M, null));
        Assert.ThrowsAny<CryptographicException>(() => s.Unlock(M, RandomNumberGenerator.GetBytes(64)));
        s.Unlock(M, key);
        Assert.Single(s.Entries);
    }

    [Fact]
    public void Save_LeavesNoTempFile_AndKeepsWorkingAfterManySaves()
    {
        using var d = new TempDir();
        var s = NewVault(d);
        for (int i = 0; i < 5; i++) { s.Entries.Add(new Entry { Site = "x" + i }); s.Save(); }
        Assert.False(File.Exists(d.File("vault.dat.tmp")));
        var s2 = new VaultStore(d.File("vault.dat")); s2.Unlock(M, null);
        Assert.Equal(6, s2.Entries.Count);
    }

    [Fact]
    public void Backups_RotateKeepingFive_NewestFirst()
    {
        using var d = new TempDir();
        var s = NewVault(d, null, 0);
        for (int i = 1; i <= 7; i++) { s.Entries.Add(new Entry { Site = "v" + i }); s.ForceBackup(); s.Save(); }
        var baks = Directory.GetFiles(d.Path, "vault.dat.bak*");
        Assert.Equal(5, baks.Length);
        int Count(string f) { var t = new VaultStore(f); t.Unlock(M, null); return t.Entries.Count; }
        Assert.Equal(6, Count(d.File("vault.dat.bak1")));   // 마지막 저장 직전 상태
        Assert.Equal(2, Count(d.File("vault.dat.bak5")));   // 가장 오래된 백업
    }

    [Fact]
    public void ChangeMaster_DeletesOldBackups_AndOldPasswordStopsWorking()
    {
        using var d = new TempDir();
        var s = NewVault(d);
        s.ForceBackup(); s.Save();
        File.WriteAllBytes(d.File("vault.dat.bak"), new byte[] { 1 });   // 옛 버전의 .bak 흉내
        Assert.True(s.VerifyMaster(M));
        s.ChangeMaster("another-pass-2");
        Assert.Empty(Directory.GetFiles(d.Path, "vault.dat.bak*"));
        Assert.ThrowsAny<CryptographicException>(() => new VaultStore(d.File("vault.dat")).Unlock(M, null));
        var s2 = new VaultStore(d.File("vault.dat")); s2.Unlock("another-pass-2", null);
        Assert.Single(s2.Entries);
        Assert.False(s.VerifyMaster(M));
    }

    [Fact]
    public void LegacyV1Vault_OpensAndUpgradesToV2()
    {
        using var d = new TempDir();
        // 옛 형식: "BXV1" | salt16 | nonce12 | tag16 | ciphertext, AAD = "BXV1", Argon2id 64MiB/3/2
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = VaultCrypto.DeriveKey(M, null, salt, 65536, 3, 2);
        var plain = JsonSerializer.SerializeToUtf8Bytes(new VaultData { Entries = { new Entry { Site = "old", Username = "u", Password = "p" } } });
        var nonce = RandomNumberGenerator.GetBytes(12); var cipher = new byte[plain.Length]; var tag = new byte[16];
        using (var aes = new AesGcm(key, 16)) aes.Encrypt(nonce, plain, cipher, tag, Encoding.ASCII.GetBytes("BXV1"));
        File.WriteAllBytes(d.File("vault.dat"), Encoding.ASCII.GetBytes("BXV1").Concat(salt).Concat(nonce).Concat(tag).Concat(cipher).ToArray());

        var s = new VaultStore(d.File("vault.dat"));
        s.Unlock(M, null);
        Assert.Equal("old", s.Entries[0].Site);
        s.Save();
        Assert.StartsWith("ONV2", Encoding.ASCII.GetString(File.ReadAllBytes(d.File("vault.dat")), 0, 4));
        var s2 = new VaultStore(d.File("vault.dat")); s2.Unlock(M, null);
        Assert.Equal("old", s2.Entries[0].Site);
    }

    [Fact]
    public void CsvImport_HandlesQuotesAndCommas()
    {
        var csv = "name,url,username,password,note\n\"My, Site\",https://a.com,bob,\"pa\"\"ss,1\",\"line1\nline2\"\n,https://b.org,carl,pw2,\n";
        var list = CsvImport.Parse(csv);
        Assert.Equal(2, list.Count);
        Assert.Equal("My, Site", list[0].Site);
        Assert.Equal("pa\"ss,1", list[0].Password);
        Assert.Equal("line1\nline2", list[0].Note);
        Assert.Equal("b.org", list[1].Site);
    }

    [Fact]
    public void AttemptTracker_LocksAfterFiveFails_AndResets()
    {
        using var d = new TempDir();
        var t = AttemptTracker.Load(d.File("a.json"));
        for (int i = 0; i < 4; i++) t.RecordFail();
        Assert.Equal(TimeSpan.Zero, t.Remaining);
        t.RecordFail();
        Assert.True(t.Remaining > TimeSpan.Zero);
        var t2 = AttemptTracker.Load(d.File("a.json"));     // 껐다 켜도 유지
        Assert.True(t2.Remaining > TimeSpan.Zero);
        t2.Reset();
        Assert.Equal(TimeSpan.Zero, t2.Remaining);
    }
}
