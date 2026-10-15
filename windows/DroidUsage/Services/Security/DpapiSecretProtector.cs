using System.Security.Cryptography;
using System.Text;
using DroidUsage.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace DroidUsage.Services.Security;

public sealed class DpapiSecretProtector : ISecretProtector
{
    private readonly ILogger<DpapiSecretProtector> _logger;

    public DpapiSecretProtector(ILogger<DpapiSecretProtector> logger)
    {
        _logger = logger;
    }

    public string Protect(string plaintext)
    {
        var bytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plaintext),
            optionalEntropy: null,
            DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(bytes);
    }

    public string? Unprotect(string protectedValue)
    {
        try
        {
            var bytes = ProtectedData.Unprotect(
                Convert.FromBase64String(protectedValue),
                optionalEntropy: null,
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            _logger.LogWarning(ex, "Could not decrypt the stored manual API key; it will be ignored.");
            return null;
        }
    }
}
