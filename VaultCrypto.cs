using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
namespace ONITON;

/// 금고 파일 형식 v2 (헤더 전체가 AES-GCM 의 인증 대상 AAD 로 묶여 있어, 헤더를 바꾸면 복호화가 실패한다)
///   "ONV2"(4) | flags(1) | mem KiB(4) | iter(4) | parallel(4) | salt(32) | nonce(12) | tag(16) | ciphertext
///   flags bit0 = 키 파일 필요.   v1("BXV1": salt16|nonce12|tag16|ct) 금고는 읽을 수 있고, 다음 로그인 때 v2 로 자동 변환된다.
public sealed class VaultHeader
{
    public int Version; public byte Flags; public uint Mem, Iter, Par;
    public byte[] Salt = Array.Empty<byte>(), Nonce = Array.Empty<byte>(), Tag = Array.Empty<byte>(), Cipher = Array.Empty<byte>(), Aad = Array.Empty<byte>();
    public bool NeedsKey => (Flags & 1) != 0;
}

public static class VaultCrypto
{
    static readonly byte[] V1 = Encoding.ASCII.GetBytes("BXV1");
    static readonly byte[] V2 = Encoding.ASCII.GetBytes("ONV2");
    public const uint DefMem = 65536, DefIter = 3, DefPar = 2;   // Argon2id: 64 MiB, 3회, 병렬 2
    const int HeadV2 = 49, FixedV2 = 77;

    public static VaultHeader Parse(byte[] f)
    {
        if (f.Length >= 48 && f.AsSpan(0, 4).SequenceEqual(V1))
            return new VaultHeader { Version = 1, Mem = DefMem, Iter = DefIter, Par = DefPar,
                Salt = f[4..20], Nonce = f[20..32], Tag = f[32..48], Cipher = f[48..], Aad = V1 };
        if (f.Length >= FixedV2 && f.AsSpan(0, 4).SequenceEqual(V2))
        {
            var h = new VaultHeader { Version = 2, Flags = f[4],
                Mem = BinaryPrimitives.ReadUInt32LittleEndian(f.AsSpan(5)), Iter = BinaryPrimitives.ReadUInt32LittleEndian(f.AsSpan(9)),
                Par = BinaryPrimitives.ReadUInt32LittleEndian(f.AsSpan(13)), Salt = f[17..49], Nonce = f[49..61], Tag = f[61..77], Cipher = f[77..], Aad = f[..HeadV2] };
            // 파일 안의 값은 믿지 않는다: 악의적으로 큰 값으로 메모리를 고갈시키는 것을 막는다
            if (h.Mem < 8192 || h.Mem > 1_048_576 || h.Iter < 1 || h.Iter > 20 || h.Par < 1 || h.Par > 16) throw new InvalidDataException("bad kdf params");
            return h;
        }
        throw new InvalidDataException("not a vault");
    }

    /// 암호를 풀지 않고 헤더만 읽어 키 파일이 필요한지 확인
    public static bool PeekNeedsKey(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var b = new byte[5]; if (fs.Read(b, 0, 5) < 5) return false;
            return b.AsSpan(0, 4).SequenceEqual(V2) && (b[4] & 1) != 0;
        }
        catch { return false; }
    }

    public static byte[] PepperFromKeyFile(byte[] keyFile) => SHA256.HashData(keyFile);

    public static byte[] DeriveKey(string password, byte[]? pepper, byte[] salt, uint mem, uint iter, uint par)
    {
        var pw = Encoding.UTF8.GetBytes(password);
        try
        {
            using var a = new Argon2id(pw) { Salt = salt, DegreeOfParallelism = (int)par, MemorySize = (int)mem, Iterations = (int)iter };
            if (pepper != null) a.KnownSecret = pepper;
            return a.GetBytes(32);
        }
        finally { CryptographicOperations.ZeroMemory(pw); }
    }

    public static byte[] Encrypt(byte[] plain, byte[] key, byte[] salt, byte flags, uint mem, uint iter, uint par)
    {
        var f = new byte[FixedV2 + plain.Length];
        V2.CopyTo(f, 0); f[4] = flags;
        BinaryPrimitives.WriteUInt32LittleEndian(f.AsSpan(5), mem); BinaryPrimitives.WriteUInt32LittleEndian(f.AsSpan(9), iter);
        BinaryPrimitives.WriteUInt32LittleEndian(f.AsSpan(13), par); salt.CopyTo(f, 17);
        RandomNumberGenerator.Fill(f.AsSpan(49, 12));                                  // 저장할 때마다 새 nonce
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(f.AsSpan(49, 12), plain, f.AsSpan(FixedV2, plain.Length), f.AsSpan(61, 16), f.AsSpan(0, HeadV2));
        return f;
    }

    public static byte[] Decrypt(VaultHeader h, byte[] key)
    {
        var plain = new byte[h.Cipher.Length];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(h.Nonce, h.Cipher, h.Tag, plain, h.Aad);   // 비밀번호/키 파일이 틀리거나 파일이 변조되면 예외
        return plain;
    }
}
