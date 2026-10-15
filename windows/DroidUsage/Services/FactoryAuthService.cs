using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DroidUsage.Models;

namespace DroidUsage.Services;

public sealed class FactoryAuthService
{
    private const string KeyringTargetName = "Factory CLI/auth-encryption-key";
    private const string WorkOsTokenEndpoint = "https://api.workos.com/user_management/authenticate";
    private const string WorkOsProductionClientId = "client_01HNM792M5G5G1A2THWPXKFMXB";
    private const int KeyLengthBytes = 32;
    private const int IvLengthBytes = 16;
    private const int TagLengthBytes = 16;

    private readonly SettingsService _settingsService;
    private readonly HttpClient _httpClient;
    private readonly string _factoryDir;

    public FactoryAuthService(SettingsService settingsService, HttpClient httpClient)
    {
        _settingsService = settingsService;
        _httpClient = httpClient;
        _factoryDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".factory");
    }

    public string GetFactoryDirectory() => _factoryDir;

    public AuthCredentials? LoadCredentials()
    {
        var manualKey = _settingsService.Current.ManualApiKey?.Trim();
        if (string.IsNullOrEmpty(manualKey))
        {
            manualKey = Environment.GetEnvironmentVariable("FACTORY_API_KEY")?.Trim();
        }

        if (!string.IsNullOrEmpty(manualKey))
        {
            return new AuthCredentials
            {
                AccessToken = manualKey
            };
        }

        var keyringPath = Path.Combine(_factoryDir, "auth.v2.keyring");
        if (File.Exists(keyringPath))
        {
            var keyB64 = CredentialManagerHelper.GetGenericPassword(KeyringTargetName);
            if (!string.IsNullOrWhiteSpace(keyB64))
            {
                var creds = TryDecryptCredentialsFile(keyringPath, keyB64);
                if (creds != null)
                {
                    return creds;
                }
            }
        }

        var fileAuthPath = Path.Combine(_factoryDir, "auth.v2.file");
        var keyFilePath = Path.Combine(_factoryDir, "auth.v2.key");
        if (File.Exists(fileAuthPath) && File.Exists(keyFilePath))
        {
            try
            {
                var keyB64 = File.ReadAllText(keyFilePath, Encoding.UTF8).Trim();
                var creds = TryDecryptCredentialsFile(fileAuthPath, keyB64);
                if (creds != null)
                {
                    return creds;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    public async Task<AuthCredentials?> RefreshWorkOsTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_settingsService.Current.ManualApiKey))
        {
            return null;
        }

        var keyringPath = Path.Combine(_factoryDir, "auth.v2.keyring");
        string? targetAuthPath = null;
        string? keyB64 = null;

        if (File.Exists(keyringPath))
        {
            keyB64 = CredentialManagerHelper.GetGenericPassword(KeyringTargetName);
            targetAuthPath = keyringPath;
        }

        if (string.IsNullOrWhiteSpace(keyB64))
        {
            var fileAuthPath = Path.Combine(_factoryDir, "auth.v2.file");
            var keyFilePath = Path.Combine(_factoryDir, "auth.v2.key");
            if (File.Exists(fileAuthPath) && File.Exists(keyFilePath))
            {
                keyB64 = File.ReadAllText(keyFilePath, Encoding.UTF8).Trim();
                targetAuthPath = fileAuthPath;
            }
        }

        if (string.IsNullOrWhiteSpace(keyB64) || string.IsNullOrWhiteSpace(targetAuthPath))
        {
            return null;
        }

        var rawJson = DecryptFileToRawJson(targetAuthPath, keyB64);
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        var creds = JsonSerializer.Deserialize<AuthCredentials>(rawJson);
        if (creds == null || string.IsNullOrWhiteSpace(creds.RefreshToken))
        {
            return null;
        }

        var formFields = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = creds.RefreshToken,
            ["client_id"] = WorkOsProductionClientId
        };

        if (!string.IsNullOrWhiteSpace(creds.ActiveOrganizationId))
        {
            formFields["organization_id"] = creds.ActiveOrganizationId;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, WorkOsTokenEndpoint)
        {
            Content = new FormUrlEncodedContent(formFields)
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var refreshed = JsonSerializer.Deserialize<WorkOsRefreshResponse>(body);
        if (refreshed == null || string.IsNullOrWhiteSpace(refreshed.AccessToken))
        {
            return null;
        }

        try
        {
            var node = JsonNode.Parse(rawJson)?.AsObject();
            if (node != null)
            {
                node["access_token"] = refreshed.AccessToken;
                node["refresh_token"] = refreshed.RefreshToken;
                EncryptAndSaveFile(targetAuthPath, keyB64, node.ToJsonString());
            }
        }
        catch
        {
        }

        creds.AccessToken = refreshed.AccessToken;
        creds.RefreshToken = refreshed.RefreshToken;
        return creds;
    }

    public string? GetCachedFactoryTier()
    {
        try
        {
            var cachePath = Path.Combine(_factoryDir, "org-managed-settings.cache.json");
            if (!File.Exists(cachePath))
            {
                return null;
            }

            var json = File.ReadAllText(cachePath, Encoding.UTF8);
            var cache = JsonSerializer.Deserialize<OrgManagedSettingsCache>(json);
            return FormatTierDisplayName(cache?.FactoryTier);
        }
        catch
        {
            return null;
        }
    }

    public static string? FormatTierDisplayName(string? rawTier)
    {
        if (string.IsNullOrWhiteSpace(rawTier))
        {
            return null;
        }

        return rawTier.Trim().ToLowerInvariant() switch
        {
            "pro_plus" or "pro+" or "plus" => "Plus",
            "pro" => "Pro",
            "max" => "Max",
            "enterprise" => "Enterprise",
            "free" or "trial" => "Trial",
            _ => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                rawTier.Replace('_', ' ').ToLowerInvariant())
        };
    }

    private static AuthCredentials? TryDecryptCredentialsFile(string filePath, string keyBase64)
    {
        var json = DecryptFileToRawJson(filePath, keyBase64);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AuthCredentials>(json);
        }
        catch
        {
            return null;
        }
    }

    private static string? DecryptFileToRawJson(string filePath, string keyBase64)
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
        catch
        {
            return null;
        }
    }

    private static void EncryptAndSaveFile(string filePath, string keyBase64, string plaintextJson)
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
