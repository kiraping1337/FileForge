namespace FileForge.Infrastructure.Sandbox;

/// <summary>
/// The whole internal API of the sandbox runner: "convert this stored file along this route".
/// No image names, no command-line arguments: the runner decides those itself, so even a fully
/// compromised worker can only ask for conversions FileForge supports anyway.
/// </summary>
public sealed record RunnerConvertRequest(
    string SourceFormat,
    string TargetFormat,
    string InputStorageKey,
    string OutputStorageKey);

/// <summary>Body of a 422 answer: the conversion was attempted and failed for a known reason.</summary>
public sealed record RunnerError(string Code);
