namespace ONITON.Tests;

/// 독립 구현(Python cryptography 라이브러리의 Argon2id + AES-GCM)으로 만든 금고 파일 샘플.
/// ONITON 의 VaultCrypto 가 이 파일들을 열 수 있다 = 파일 형식과 암호 처리가 다른 구현과 일치한다.
public static class Fixtures
{
    public const string MasterA = "fixture-master-A";
    public const string MasterB = "fixture-master-B";
    public static readonly byte[] KeyFileB = System.Text.Encoding.ASCII.GetBytes("keyfile-bytes-0123456789");

    // 키 파일 없음 (Argon2id 8 MiB, 1회, 병렬 1)
    public static readonly byte[] VaultA = Convert.FromHexString(
        "4f4e563200002000000100000001000000000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e"
        + "1f6465666768696a6b6c6d6e6fb97b37682cf4a9d5dac83de05ed648071e16c6438bf182668ba05233148f2e1697a3fd"
        + "1c289315749ff7a28cb0acba527d99cb0d072f3999cb6d1d2dd5608da55623c0d9f91de0ba01cd3294edd637b01e457f"
        + "59b9df2c7b6fcf5c1fe93bab8644839a6eff1dbfde13bc6625f11366ff4366cee0fdac4bc12c9208ac08ca2eee30c01b"
        + "31d20fe313e2258be9f67b62c733df2a8b415d235b19d900488931db17fdbb7fb09f3422f10517");

    // 키 파일 필요 (flags=1, pepper = SHA-256(키 파일))
    public static readonly byte[] VaultB = Convert.FromHexString(
        "4f4e56320100200000010000000100000032333435363738393a3b3c3d3e3f404142434445464748494a4b4c4d4e4f50"
        + "51c8c9cacbcccdcecfd0d1d2d32e9745f3502a20c0c5a1ca7d7cb98eb738a2b871f258a4a47ac2ddd732c5ef50d8f347"
        + "e7b232c884939ba1a4746a10c156468361263f0675698912a6cf230fdc96dac14e5f4322ed77784af4e4b081473a72f4"
        + "9675245bb573e843a2bfd6cfce0dc1e9546bba7ded01ac6fe3c81b89e8740ac4ce369b3cfe9271e31fa46d942c139f5b"
        + "2d5a8df9be3229fd69fec411e3c3320bdc0c41bcab53f1b0ce39ffbc03280800f7eda71a7457bc");
}
