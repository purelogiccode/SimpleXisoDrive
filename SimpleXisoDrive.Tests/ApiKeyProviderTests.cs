using System.Security.Cryptography;
using System.Text;
using SimpleXisoDrive.Services;

namespace SimpleXisoDrive.Tests;

public class ApiKeyProviderTests
{
    [Fact]
    public void ApiKey_IsDecryptedDeterministically()
    {
        var key = ApiKeyProvider.ApiKey;

        Assert.False(string.IsNullOrWhiteSpace(key));
        Assert.Equal(65, key.Length);
        Assert.Equal(key, ApiKeyProvider.ApiKey);
    }

    [Fact]
    public void ApiKey_MatchesExpectedDigest()
    {
        // SHA-256 of the plaintext key; the key itself is never stored in the test.
        const string expectedDigest = "BF8A92A0948A5B4C89B56EA8A03681892158608FB867C0E75D9C5F252471264C";

        var actualDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ApiKeyProvider.ApiKey)));

        Assert.Equal(expectedDigest, actualDigest);
    }

    [Fact]
    public void Preload_DecryptsTheKey()
    {
        ApiKeyProvider.Preload();

        Assert.NotEmpty(ApiKeyProvider.ApiKey);
    }
}
