using DroidUsage.Models;

namespace DroidUsage.Services.Abstractions;

public interface ICredentialProvider
{
    AuthCredentials? LoadCredentials();
}

public interface ITokenRefresher
{
    Task<AuthCredentials?> RefreshAsync(CancellationToken cancellationToken = default);
}

public interface IPlanTierProvider
{
    string? GetCachedTier();
}

public interface IFactoryAuthCrypto
{
    string? DecryptFileToJson(string filePath, string keyBase64);

    void EncryptJsonToFile(string filePath, string keyBase64, string plaintextJson);
}
