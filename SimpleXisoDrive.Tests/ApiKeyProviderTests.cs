using System.Security.Cryptography;
using System.Text;
using SimpleXisoDrive.Core.Services;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the <c>ApiKeyProvider</c> decryption: determinism, expected digest and eager preload.
/// </summary>
public class ApiKeyProviderTests
{
    /// <summary>
    /// Verifies the key decrypts to a 65-character value and stays stable across accesses.
    /// </summary>
    [Fact]
    public void ApiKey_IsDecryptedDeterministically()
    {
        var key = ApiKeyProvider.ApiKey;

        Assert.False(string.IsNullOrWhiteSpace(key));
        Assert.Equal(65, key.Length);
        Assert.Equal(key, ApiKeyProvider.ApiKey);
    }

    /// <summary>
    /// Verifies the decrypted key hashes to the expected SHA-256 digest.
    /// </summary>
    [Fact]
    public void ApiKey_MatchesExpectedDigest()
    {
        // SHA-256 of the plaintext key; the key itself is never stored in the test.
        const string expectedDigest = "BF8A92A0948A5B4C89B56EA8A03681892158608FB867C0E75D9C5F252471264C";

        var actualDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ApiKeyProvider.ApiKey)));

        Assert.Equal(expectedDigest, actualDigest);
    }

    /// <summary>
    /// Verifies <c>Preload</c> eagerly decrypts and caches the key.
    /// </summary>
    [Fact]
    public void Preload_DecryptsTheKey()
    {
        ApiKeyProvider.Preload();

        Assert.NotEmpty(ApiKeyProvider.ApiKey);
    }
}