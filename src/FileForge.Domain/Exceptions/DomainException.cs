namespace FileForge.Domain.Exceptions;

/// <summary>
/// Thrown when a domain rule is violated (invalid argument, impossible state, etc.).
/// </summary>
public class DomainException(string message) : Exception(message)
{
}
