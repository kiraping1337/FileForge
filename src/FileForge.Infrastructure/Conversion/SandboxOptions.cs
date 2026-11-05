namespace FileForge.Infrastructure.Conversion;

/// <summary>Settings shared by all converter containers.</summary>
public sealed class SandboxOptions
{
    public const string SectionName = "Sandbox";

    public string DockerExecutable { get; set; } = "docker";

    /// <summary>
    /// Temporary folders for container output, one per conversion. LocalApplicationData instead of
    /// the temp folder: on Windows the temp path may contain short 8.3 names, which Docker cannot mount.
    /// </summary>
    public string WorkPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileForge", "work");

    /// <summary>
    /// Empty (default): converter containers bind-mount folders of this machine, used when the code
    /// runs on the host. Set to a Docker volume name when the code runs in a container: paths inside
    /// our container mean nothing to the host's Docker. WorkPath must then be where the volume is mounted.
    /// </summary>
    public string? WorkVolume { get; set; }

    public int PidsLimit { get; set; } = 64;

    /// <summary>Size of the in-memory /tmp. It counts towards the container memory limit.</summary>
    public string TmpSize { get; set; } = "256m";
}

/// <summary>Settings of one converter's container: which image, how much CPU, memory and time.</summary>
public abstract class ConverterContainerOptions
{
    public string Image { get; set; } = string.Empty;

    public string MemoryLimit { get; set; } = "768m";

    public string CpuLimit { get; set; } = "1";

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
