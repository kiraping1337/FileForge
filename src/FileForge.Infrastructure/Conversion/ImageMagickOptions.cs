namespace FileForge.Infrastructure.Conversion;

public sealed class ImageMagickOptions
{
    public const string SectionName = "ImageMagick";

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Used only when the sandbox is disabled: magick runs directly on this machine.
    /// "magick" is found through PATH.
    /// </summary>
    public string ExecutablePath { get; set; } = "magick";

    /// <summary>
    /// Temporary folders for container output, one per conversion. LocalApplicationData instead of
    /// the temp folder: on Windows the temp path may contain short 8.3 names, which Docker cannot mount.
    /// </summary>
    public string WorkPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileForge", "work");

    public SandboxOptions Sandbox { get; set; } = new();
}

/// <summary>Limits of the one-off container that runs each conversion.</summary>
public sealed class SandboxOptions
{
    /// <summary>false = run magick directly on the host. Only for development without Docker.</summary>
    public bool Enabled { get; set; } = true;

    public string DockerExecutable { get; set; } = "docker";

    /// <summary>Built from converters/imagemagick.</summary>
    public string Image { get; set; } = "fileforge-imagemagick:1";

    /// <summary>Must be larger than the memory limit in policy.xml plus the /tmp size: tmpfs counts as memory.</summary>
    public string MemoryLimit { get; set; } = "768m";

    public string CpuLimit { get; set; } = "1";

    public int PidsLimit { get; set; } = 64;

    public string TmpSize { get; set; } = "256m";
}
