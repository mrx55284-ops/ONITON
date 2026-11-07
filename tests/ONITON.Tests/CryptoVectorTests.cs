using System.Security.Cryptography;
using Konscious.Security.Cryptography;
using Xunit;
namespace ONITON.Tests;

/// 공인된 시험 값(RFC 9106, NIST)으로 쓰고 있는 암호 부품이 표준대로 동작하는지 확인한다.
public class CryptoVectorTests
{
    [Fact]
    public void Argon2id_Rfc9106_TestVector()
    {
        using var a = new Argon2id(Enumerable.Repeat((byte)1, 32).ToArray())
        {
            Salt = Enumerable.Repeat((byte)2, 16).ToArray(),
            KnownSecret = Enumerable.Repeat((byte)3, 8).ToArray(),
            AssociatedData = Enumerable.Repeat((byte)4, 12).ToArray(),
            DegreeOfParallelism = 4, MemorySize = 32, Iterations = 3,
        };
        Assert.Equal("0d640df58d78766c08c037a34a8b53c9d01ef0452d75b65eb52520e96b01e659", Convert.ToHexString(a.GetBytes(32)).ToLowerInvariant());
    }

    [Fact]
    public void AesGcm_Nist_TestCases()
    {
        using var aes = new AesGcm(new byte[16], 16);
        var tag = new byte[16];
        aes.Encrypt(new byte[12], Array.Empty<byte>(), Array.Empty<byte>(), tag);          // Test Case 1
        Assert.Equal("58e2fccefa7e3061367f1d57a4e7455a", Convert.ToHexString(tag).ToLowerInvariant());
        var ct = new byte[16];
        aes.Encrypt(new byte[12], new byte[16], ct, tag);                                    // Test Case 2
        Assert.Equal("0388dace60b6a392f328c2b971b2fe78", Convert.ToHexString(ct).ToLowerInvariant());
        Assert.Equal("ab6e47d42cec13bdf53a67b21257bddf", Convert.ToHexString(tag).ToLowerInvariant());
    }

    [Fact]
    public void IndependentImplementation_VaultWithoutKeyFile_Opens()
    {
        using var d = new TempDir();
        File.WriteAllBytes(d.File("vault.dat"), Fixtures.VaultA);
        var s = new VaultStore(d.File("vault.dat"));
        Assert.False(s.NeedsKeyFile);
        s.Unlock(Fixtures.MasterA, null);
        Assert.Single(s.Entries);
        Assert.Equal("앨리스", s.Entries[0].Username);
        Assert.Equal("S3cret!pw", s.Entries[0].Password);
        Assert.Equal("예시", s.Entries[0].Site);
    }

    [Fact]
    public void IndependentImplementation_VaultWithKeyFile_Opens()
    {
        using var d = new TempDir();
        File.WriteAllBytes(d.File("vault.dat"), Fixtures.VaultB);
        var s = new VaultStore(d.File("vault.dat"));
        Assert.True(s.NeedsKeyFile);
        Assert.ThrowsAny<CryptographicException>(() => s.Unlock(Fixtures.MasterB, null));                       // 키 파일 없이는 불가
        Assert.ThrowsAny<CryptographicException>(() => s.Unlock(Fixtures.MasterB, new byte[] { 1, 2, 3 }));     // 다른 키 파일 불가
        s.Unlock(Fixtures.MasterB, Fixtures.KeyFileB);
        Assert.Equal("앨리스", s.Entries[0].Username);
    }

    [Fact]
    public void IndependentImplementation_WrongPassword_Fails()
    {
        using var d = new TempDir();
        File.WriteAllBytes(d.File("vault.dat"), Fixtures.VaultA);
        Assert.ThrowsAny<CryptographicException>(() => new VaultStore(d.File("vault.dat")).Unlock("wrong", null));
    }

    [Fact]
    public void WeakKdfVault_IsUpgradedToCurrentStrength_OnNextSave()
    {
        using var d = new TempDir();
        File.WriteAllBytes(d.File("vault.dat"), Fixtures.VaultA);        // 8 MiB / 1회 로 만들어진 금고
        var s = new VaultStore(d.File("vault.dat"));
        s.Unlock(Fixtures.MasterA, null);
        s.Save();
        var h = VaultCrypto.Parse(File.ReadAllBytes(d.File("vault.dat")));
        Assert.Equal(VaultCrypto.DefMem, h.Mem);
        Assert.Equal(VaultCrypto.DefIter, h.Iter);
        var s2 = new VaultStore(d.File("vault.dat")); s2.Unlock(Fixtures.MasterA, null);
        Assert.Equal("앨리스", s2.Entries[0].Username);
    }

    [Fact]
    public void OversizedFile_IsRejected()
    {
        var big = new byte[(int)(VaultCrypto.MaxFileBytes + 1)];
        Assert.Throws<InvalidDataException>(() => VaultCrypto.Parse(big));
    }
}
