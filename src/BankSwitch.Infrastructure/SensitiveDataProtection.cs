using System.Security.Cryptography;
using System.Text;
using BankSwitch.Application;
using Microsoft.Extensions.Configuration;

namespace BankSwitch.Infrastructure;

public sealed class AesGcmSensitiveDataProtector : ISensitiveDataProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public AesGcmSensitiveDataProtector(IConfiguration configuration, ISecretProvider secrets)
    {
        var configuredKey = secrets.GetSecretAsync("CardDataEncryptionKey").GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            throw new InvalidOperationException("CardDataEncryptionKey secret is required. Supply a 32-byte base64 key from a vault or HSM-backed secret provider.");
        }

        _key = DecodeKey(configuredKey);
        if (_key.Length != 32)
        {
            throw new InvalidOperationException("CardDataEncryptionKey must decode to exactly 32 bytes for AES-256-GCM.");
        }
    }

    public string Protect(string plaintext, string purpose)
    {
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];
        var aad = Encoding.UTF8.GetBytes(purpose ?? string.Empty);
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag, aad);
        return $"v1.{Convert.ToBase64String(nonce)}.{Convert.ToBase64String(tag)}.{Convert.ToBase64String(cipher)}";
    }

    public string Unprotect(string protectedValue, string purpose)
    {
        if (string.IsNullOrWhiteSpace(protectedValue)) return string.Empty;
        var parts = protectedValue.Split('.');
        if (parts.Length != 4 || parts[0] != "v1") throw new CryptographicException("Unsupported protected data format.");
        var nonce = Convert.FromBase64String(parts[1]);
        var tag = Convert.FromBase64String(parts[2]);
        var cipher = Convert.FromBase64String(parts[3]);
        var plain = new byte[cipher.Length];
        var aad = Encoding.UTF8.GetBytes(purpose ?? string.Empty);
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain, aad);
        return Encoding.UTF8.GetString(plain);
    }

    private static byte[] DecodeKey(string key)
    {
        try
        {
            return Convert.FromBase64String(key);
        }
        catch (FormatException)
        {
            return Encoding.UTF8.GetBytes(key);
        }
    }
}

public sealed class DevelopmentSensitiveDataProtector : ISensitiveDataProtector
{
    public string Protect(string plaintext, string purpose)
    {
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;
        return "dev." + Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext));
    }

    public string Unprotect(string protectedValue, string purpose)
    {
        if (string.IsNullOrWhiteSpace(protectedValue)) return string.Empty;
        if (!protectedValue.StartsWith("dev.", StringComparison.Ordinal)) return protectedValue;
        return Encoding.UTF8.GetString(Convert.FromBase64String(protectedValue[4..]));
    }
}
