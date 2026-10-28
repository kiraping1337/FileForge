using System.Buffers.Text;
using System.Security.Cryptography;

namespace FileForge.Application.Jobs;

/// <summary>
/// Secrets that grant access to one job. The client receives the token once; only its SHA-256 hash is stored.
/// </summary>
public static class AccessTokens
{
    private const int TokenBytes = 32;

    public static (string Token, byte[] Hash) Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenBytes);
        return (Base64Url.EncodeToString(bytes), SHA256.HashData(bytes));
    }

    /// <summary>
    /// Returns null for anything that is not a well-formed token, so callers can simply answer "access denied".
    /// </summary>
    public static byte[]? TryHash(string? token)
    {
        if (string.IsNullOrEmpty(token)
            || !Base64Url.IsValid(token, out var decodedLength)
            || decodedLength != TokenBytes)
        {
            return null;
        }

        return SHA256.HashData(Base64Url.DecodeFromChars(token));
    }
}
