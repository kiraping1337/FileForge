namespace FileForge.Infrastructure.Conversion;

public sealed class FfmpegOptions : ConverterContainerOptions
{
    public const string SectionName = "Ffmpeg";

    public FfmpegOptions()
    {
        // Built from converters/ffmpeg. Video needs more memory, CPU and time than images.
        // Timeout must stay well below Cleanup:StuckJobTimeout (checked at startup).
        Image = "fileforge-ffmpeg:1";
        MemoryLimit = "1g";
        CpuLimit = "2";
        Timeout = TimeSpan.FromMinutes(15);
    }

    /// <summary>FFmpeg would otherwise start a thread per CPU core of the host.</summary>
    public int Threads { get; set; } = 2;

    /// <summary>Largest allowed result. FFmpeg stops writing at this size and the job fails.</summary>
    public long MaxOutputBytes { get; set; } = 1024L * 1024 * 1024;
}
