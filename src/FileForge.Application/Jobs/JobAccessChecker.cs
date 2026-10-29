using FileForge.Application.Abstractions;
using FileForge.Application.Errors;
using Microsoft.EntityFrameworkCore;

namespace FileForge.Application.Jobs;

public sealed class JobAccessChecker(IAppDbContext db)
{
    public async Task EnsureAccessAsync(DateTime now, Guid id, string? token, CancellationToken cancellationToken)
    {
        byte[]? hash = AccessTokens.TryHash(token);
        if (hash == null)
        {
            throw new AppException(ErrorCodes.AccessDenied, "Access to this job is denied.");
        }
        var exists = await db.JobAccessTokens.AnyAsync(
            t => t.JobId == id && t.TokenHash == hash && t.ExpiresAtUtc > now,
            cancellationToken);

        if (!exists)
        {
            throw new AppException(ErrorCodes.AccessDenied, "Access to this job is denied.");
        }
    }
}
