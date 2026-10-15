namespace DroidUsage.Services.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }

    DateTime Now { get; }
}

public interface ISecretProtector
{
    string Protect(string plaintext);

    string? Unprotect(string protectedValue);
}

public interface ISecretStore
{
    string? GetGenericPassword(string targetName);
}
