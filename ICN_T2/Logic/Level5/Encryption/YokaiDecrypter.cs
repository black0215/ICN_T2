using System;
using System.IO;
using System.Security.Cryptography;
using System.Linq;

namespace ICN_T2.Logic.Level5.Encryption;

/// <summary>
/// [최적화 완료] Level-5 AES 복호화기
/// - 메모리 복사 없이 스트림 오프셋을 사용하여 해독 (Zero-Copy Decryption)
/// - 확장자별 키 자동 선택
/// </summary>
public static class YokaiDecrypter
{
    // 1. 사운드 파일용 키 (.m4a, .ogg 등)
    private static readonly byte[] SoundKey = new byte[] {
        0x80, 0xF0, 0x08, 0x39, 0x4E, 0xB0, 0x2F, 0x4F,
        0xC7, 0xF5, 0xA5, 0xC2, 0x35, 0xC4, 0x29, 0x18
    };

    // 2. 데이터 파일용 키 (.ez, .zip, .xpck 등)
    private static readonly byte[] EzKey = new byte[] {
        0x2A, 0xB5, 0x11, 0xF4, 0x77, 0x97, 0x7D, 0x25,
        0xCF, 0x6F, 0x7A, 0x8A, 0xE0, 0x49, 0xA1, 0x25
    };

    /// <summary>
    /// 파일 확장자에 따라 키를 선택하고 AES-128-CBC 복호화를 수행합니다.
    /// </summary>
    /// <param name="fileName">파일 이름 (확장자 확인용)</param>
    /// <param name="fileData">암호화된 원본 데이터</param>
    /// <returns>복호화된 데이터 (실패 시 null)</returns>
    public static byte[]? DecryptFile(string fileName, byte[] fileData)
    {
        // 최소 길이 체크 (IV 16바이트 + 데이터 1바이트 이상)
        if (fileData == null || fileData.Length <= 16)
            return null;

        // 1. 키 선택
        byte[] key = GetKeyForFile(fileName);

        // 2. IV 추출 (앞 16바이트)
        // IV는 CreateDecryptor에 배열로 넘겨야 하므로 할당 불가피 (작아서 OK)
        byte[] iv = new byte[16];
        Array.Copy(fileData, 0, iv, 0, 16);

        try
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = 128;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor())
                // [핵심 최적화] 배열을 복사하지 않고, Offset(16)부터 읽는 스트림 생성
                using (var msDecrypt = new MemoryStream(fileData, 16, fileData.Length - 16))
                using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (var msResult = new MemoryStream())
                {
                    csDecrypt.CopyTo(msResult);
                    return msResult.ToArray();
                }
            }
        }
        catch
        {
            // 복호화 실패 (키가 안 맞거나 암호화된 파일이 아님)
            return null;
        }
    }

    private static byte[] GetKeyForFile(string fileName)
    {
        string ext = Path.GetExtension(fileName)?.Trim('.').ToLowerInvariant() ?? "";

        // 사운드 포맷 체크
        if (ext == "m4" || ext == "og" || ext == "m4a" || ext == "ogg")
        {
            return SoundKey;
        }

        // 그 외엔 기본 데이터 키 사용
        return EzKey;
    }
}