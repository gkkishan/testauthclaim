using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Shared.Models;

namespace Shared.Encryption;

public static class AesTokenEncryptor
{
    public static string Encrypt(OneAccessToken token, string aesKey, string aesIV)
    {
        var json = JsonSerializer.Serialize(token);
        var plainBytes = Encoding.UTF8.GetBytes(json);

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = DeriveKey(aesKey);
        aes.IV = DeriveIV(aesIV);

        using var encryptor = aes.CreateEncryptor();
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        return Convert.ToBase64String(cipherBytes);
    }

    public static OneAccessToken? Decrypt(string encryptedToken, string aesKey, string aesIV)
    {
        try
        {
            var cipherBytes = Convert.FromBase64String(encryptedToken);

            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = DeriveKey(aesKey);
            aes.IV = DeriveIV(aesIV);

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            var json = Encoding.UTF8.GetString(plainBytes);
            return JsonSerializer.Deserialize<OneAccessToken>(json);
        }
        catch
        {
            return null;
        }
    }

    // AES-256 requires 32-byte key; derive via SHA256 if input is shorter
    private static byte[] DeriveKey(string keyString)
    {
        var raw = Encoding.UTF8.GetBytes(keyString);
        if (raw.Length == 32)
            return raw;
        return SHA256.HashData(raw);
    }

    // AES CBC requires 16-byte IV; derive via MD5 if input is shorter
    private static byte[] DeriveIV(string ivString)
    {
        var raw = Encoding.UTF8.GetBytes(ivString);
        if (raw.Length == 16)
            return raw;
        return MD5.HashData(raw);
    }
}
