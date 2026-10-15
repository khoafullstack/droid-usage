using System.Security.Cryptography;
using System.Text;
using DroidUsage.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace DroidUsage.Services.Auth;

public sealed class FactoryAuthCrypto : IFactoryAuthCrypto
{
    private const int KeyLengthBytes = 32;
    private const int IvLengthBytes = 16;
    private const int TagLengthBytes = 16;

    private readonly ILogger<FactoryAuthCrypto> _logger;

    public FactoryAuthCrypto(ILogger<FactoryAuthCrypto> logger)
    {
        _logger = logger;
    }

    public string? DecryptFileToJson(string filePath, string keyBase64)
    {
        try
        {
            var key = Convert.FromBase64String(keyBase64.Trim());
            if (key.Length != KeyLengthBytes)
            {
                return null;
            }

            var encryptedContent = File.ReadAllText(filePath, Encoding.UTF8).Trim();
            var parts = encryptedContent.Split(':');
            if (parts.Length != 3)
            {
                return null;
            }

            var iv = Convert.FromBase64String(parts[0]);
            var tag = Convert.FromBase64String(parts[1]);
            var ciphertext = Convert.FromBase64String(parts[2]);

            if (iv.Length != IvLengthBytes || tag.Length != TagLengthBytes)
            {
                return null;
            }

            var plaintext = new byte[ciphertext.Length];
            using var aesGcm = new AesGcm(key, TagLengthBytes);
            aesGcm.Decrypt(iv, ciphertext, tag, plaintext);

            return Encoding.UTF8.GetString(plaintext);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt Factory auth file {File}.", Path.GetFileName(filePath));
            return null;
        }
    }

    public void EncryptJsonToFile(string filePath, string keyBase64, string plaintextJson)
    {
        var key = Convert.FromBase64String(keyBase64.Trim());
        if (key.Length != KeyLengthBytes)
        {
            return;
        }

        var iv = RandomNumberGenerator.GetBytes(IvLengthBytes);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintextJson);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagLengthBytes];

        using var aesGcm = new AesGcm(key, TagLengthBytes);
        aesGcm.Encrypt(iv, plaintextBytes, ciphertext, tag);

        var payload = $"{Convert.ToBase64String(iv)}:{Convert.ToBase64String(tag)}:{Convert.ToBase64String(ciphertext)}";
        var tempPath = filePath + ".tmp";
        File.WriteAllText(tempPath, payload, Encoding.UTF8);
        File.Move(tempPath, filePath, overwrite: true);
    }
}
