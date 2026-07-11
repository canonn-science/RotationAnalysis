using RotationAnalysis.Core.Storage;
using Xunit;

namespace RotationAnalysis.Core.Tests;

public class SecretStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "SecretStoreTests_" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string SecretsPath => Path.Combine(_directory, "secrets.dat");

    [Fact]
    public void TryGetClaudeApiKey_ReturnsFalse_WhenNoKeyStored()
    {
        var store = new SecretStore(SecretsPath);
        Assert.False(store.TryGetClaudeApiKey(out var key));
        Assert.Equal(string.Empty, key);
    }

    [Fact]
    public void SetAndGetClaudeApiKey_RoundTrips()
    {
        var store = new SecretStore(SecretsPath);
        store.SetClaudeApiKey("sk-ant-test-12345");

        Assert.True(store.TryGetClaudeApiKey(out var key));
        Assert.Equal("sk-ant-test-12345", key);
    }

    [Fact]
    public void DeleteClaudeApiKey_RemovesStoredKey()
    {
        var store = new SecretStore(SecretsPath);
        store.SetClaudeApiKey("sk-ant-test-12345");
        store.DeleteClaudeApiKey();

        Assert.False(store.TryGetClaudeApiKey(out _));
        Assert.False(File.Exists(SecretsPath));
    }

    [Fact]
    public void StoredSecretFile_IsNotPlaintext()
    {
        var store = new SecretStore(SecretsPath);
        store.SetClaudeApiKey("sk-ant-test-12345");

        var bytes = File.ReadAllBytes(SecretsPath);
        var raw = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("sk-ant-test-12345", raw);
    }
}
