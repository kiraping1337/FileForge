namespace FileForge.Application.Errors;

/// <summary>
/// An expected error with a code from <see cref="ErrorCodes"/> and a message that is safe to show
/// to the user. The API turns it into an error response; anything else becomes a generic 500.
/// </summary>
public sealed class AppException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
