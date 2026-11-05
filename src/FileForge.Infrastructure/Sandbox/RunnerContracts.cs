namespace FileForge.Infrastructure.Sandbox;

/// <summary>
/// The whole internal API of the sandbox runner: "convert this stored file along this route".
/// No image names or command-line arguments: the runner decides those, so a compromised worker
/// can only ask for conversions that FileForge supports anyway.
/// </summary>
public sealed record RunnerConvertRequest(
    string SourceFormat,
    string TargetFormat,
    string InputStorageKey,
    string OutputStorageKey);

/// <summary>Body of a 422 answer: the conversion was attempted and failed for a known reason.</summary>
public sealed record RunnerError(string Code);
