namespace FileForge.Infrastructure.Conversion;

public sealed class FfmpegOptions : ConverterContainerOptions
{
    public const string SectionName = "Ffmpeg";

    public FfmpegOptions()
    {
        // Built from converters/ffmpeg. Video needs more memory, CPU and time than images.
        // Timeout must stay well below Cleanup:StuckJobTimeout.
        Image = "fileforge-ffmpeg:1";
        MemoryLimit = "1g";
        CpuLimit = "2";
        Timeout = TimeSpan.FromMinutes(5);
    }

    /// <summary>FFmpeg would otherwise start a thread per CPU core of the host.</summary>
    public int Threads { get; set; } = 2;
}
