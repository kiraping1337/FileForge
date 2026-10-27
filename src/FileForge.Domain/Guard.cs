using FileForge.Domain.Exceptions;

namespace FileForge.Domain;

/// <summary>
/// Small argument checks shared by the domain entities.
/// </summary>
internal static class Guard
{
    public static void RequireText(string? value, int maxLength, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{paramName} is required.");
        }

        if (value.Length > maxLength)
        {
            throw new DomainException($"{paramName} must be at most {maxLength} characters.");
        }
    }

    public static void RequireUtc(DateTime value, string paramName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new DomainException($"{paramName} must be a UTC time.");
        }
    }

    public static void RequireNonNegative(long value, string paramName)
    {
        if (value < 0)
        {
            throw new DomainException($"{paramName} cannot be negative.");
        }
    }
}
