using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace VideoAnalysis.Core.Storage;

/// <summary>Persists a single secret (the optional Claude API key used as a Jet Cone Length HUD-OCR
/// fallback) encrypted at rest with Windows DPAPI, current-user scope. Kept in its own file rather
/// than <c>settings.json</c> so a credential never ends up in a plaintext config file someone might
/// paste into a bug report.</summary>
[SupportedOSPlatform("windows")]
public sealed class SecretStore
{
    private const string ClaudeApiKeyName = "claude-api-key";

    public string SecretsPath { get; }

    public SecretStore(string? secretsPath = null)
    {
        SecretsPath = secretsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RotationAnalysisLab",
            "secrets.dat");
    }

    public bool TryGetClaudeApiKey(out string apiKey)
    {
        apiKey = string.Empty;
        if (!File.Exists(SecretsPath))
        {
            return false;
        }

        try
        {
            var encrypted = File.ReadAllBytes(SecretsPath);
            var decrypted = ProtectedData.Unprotect(encrypted, optionalEntropy: null, DataProtectionScope.CurrentUser);
            apiKey = Encoding.UTF8.GetString(decrypted);
            return apiKey.Length > 0;
        }
        catch (CryptographicException)
        {
            // Written by a different user profile, or corrupted - treat as "no key set" rather
            // than crashing the app.
            return false;
        }
        catch (Exception)
        {
            // IO/permission/corruption errors - treat as "no key set" rather than crashing the app.
            return false;
        }
    }

    public void SetClaudeApiKey(string apiKey)
    {
        var directory = Path.GetDirectoryName(SecretsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var plaintext = Encoding.UTF8.GetBytes(apiKey);
        var encrypted = ProtectedData.Protect(plaintext, optionalEntropy: null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(SecretsPath, encrypted);
    }

    public void DeleteClaudeApiKey()
    {
        if (File.Exists(SecretsPath))
        {
            File.Delete(SecretsPath);
        }
    }
}
