using System.Security.Cryptography;
using System.Text;
using Serilog;

namespace SimpleXisoDrive.Services;

/// <summary>
/// Supplies the API key used by the bug report and statistics services. The key is
/// never stored in plain text: it is protected by two independent layers in the
/// assembly (an AES-256-CBC outer layer over a SHA-256 XOR inner layer) and is
/// decrypted once at startup.
/// </summary>
internal static class ApiKeyProvider
{
    // Outer layer: AES-256-CBC ciphertext, split into two halves so no contiguous
    // blob of the ciphertext appears in the binary or source.
    private const string CipherTextPart1 = "fkLVD5B94neWFwVC8GUnqBWIytSbn1tO6crhsV3nNqI/h5eq/zMSPA";
    private const string CipherTextPart2 = "I1NWSoCI1S/dOQDBaXplcfvTH8Yi83yqweCUV0qFrfmZOwkHTgZa8=";
    private const string InitializationVector = "T7vi840FJ/aetDrpAId22Q==";
    private const string AesPassphrase = "SXD.K3y.L2.AES.2026.7f4a9c";

    // Inner layer: repeating XOR pad derived from this passphrase.
    private const string XorPassphrase = "SXD.K3y.L1.XOR.2026.b81e5d";

    private static readonly Lock Sync = new();
    private static string? _apiKey;

    /// <summary>
    /// Gets the decrypted API key, or an empty string when decryption fails (remote
    /// reporting is then disabled). The value is decrypted on first access and cached.
    /// </summary>
    public static string ApiKey
    {
        get
        {
            lock (Sync)
            {
                if (_apiKey is not null)
                {
                    return _apiKey;
                }

                try
                {
                    _apiKey = Decrypt();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to decrypt the API key; remote reporting is disabled");
                    _apiKey = string.Empty;
                }

                return _apiKey;
            }
        }
    }

    /// <summary>
    /// Decrypts the key eagerly so it is ready before the first report is built.
    /// Called during application startup; never throws.
    /// </summary>
    public static void Preload() => _ = ApiKey;

    private static string Decrypt()
    {
        byte[] cipherText = Convert.FromBase64String(CipherTextPart1 + CipherTextPart2);
        byte[] initializationVector = Convert.FromBase64String(InitializationVector);
        byte[] aesKey = SHA256.HashData(Encoding.UTF8.GetBytes(AesPassphrase));

        // Layer 2: AES-256-CBC.
        byte[] intermediate;
        using (var aes = Aes.Create())
        {
            aes.Key = aesKey;
            aes.IV = initializationVector;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            intermediate = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
        }

        // Layer 1: XOR with the SHA-256-derived repeating pad.
        byte[] pad = SHA256.HashData(Encoding.UTF8.GetBytes(XorPassphrase));
        for (var i = 0; i < intermediate.Length; i++)
        {
            intermediate[i] ^= pad[i % pad.Length];
        }

        return Encoding.UTF8.GetString(intermediate);
    }
}
