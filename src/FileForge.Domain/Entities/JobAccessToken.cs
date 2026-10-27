using FileForge.Domain.Exceptions;

namespace FileForge.Domain.Entities;

/// <summary>
/// Grants access to one job without user accounts. Only the SHA-256 hash of the
/// token is stored: a database leak does not reveal tokens that can be used.
/// </summary>
public class JobAccessToken
{
    /// <summary>SHA-256 produces 32 bytes.</summary>
    public const int HashLengthBytes = 32;

    private JobAccessToken()
    {
    }

    public Guid Id { get; private set; }

    public Guid JobId { get; private set; }

    public byte[] TokenHash { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public static JobAccessToken Create(Guid jobId, byte[] tokenHash, DateTime nowUtc, DateTime expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        Guard.RequireUtc(nowUtc, nameof(nowUtc));
        Guard.RequireUtc(expiresAtUtc, nameof(expiresAtUtc));

        if (tokenHash.Length != HashLengthBytes)
        {
            throw new DomainException($"Token hash must be {HashLengthBytes} bytes.");
        }

        if (expiresAtUtc <= nowUtc)
        {
            throw new DomainException("Token expiry must be in the future.");
        }

        return new JobAccessToken
        {
            Id = Guid.CreateVersion7(),
            JobId = jobId,
            TokenHash = tokenHash.ToArray(), // copy so the caller cannot change it later
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = expiresAtUtc,
        };
    }

    public bool IsActiveAt(DateTime nowUtc) => nowUtc < ExpiresAtUtc;
}
