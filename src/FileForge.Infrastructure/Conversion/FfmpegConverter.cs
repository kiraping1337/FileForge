using System.Globalization;
using FileForge.Application.Conversions;
using Microsoft.Extensions.Options;

namespace FileForge.Infrastructure.Conversion;

internal sealed class FfmpegConverter(DockerSandbox sandbox, IOptions<FfmpegOptions> options) : IFileConverter
{
    private static readonly string[] AudioFormats = ["mp3", "wav", "flac", "ogg", "aac"];
    private static readonly string[] VideoFormats = ["mp4", "mkv", "webm", "mov"];

    // FFmpeg's own names for reading (demuxer) and writing (muxer) each of our formats.
    // MP4 and MOV share one demuxer, MKV and WebM too: they are close relatives.
    private static readonly Dictionary<string, string> Demuxers = new()
    {
        ["mp3"] = "mp3", ["wav"] = "wav", ["flac"] = "flac", ["ogg"] = "ogg", ["aac"] = "aac",
        ["mp4"] = "mov", ["mov"] = "mov", ["mkv"] = "matroska", ["webm"] = "matroska",
    };

    private static readonly Dictionary<string, string> Muxers = new()
    {
        ["mp3"] = "mp3", ["wav"] = "wav", ["flac"] = "flac", ["ogg"] = "ogg", ["aac"] = "adts",
        ["mp4"] = "mp4", ["mov"] = "mov", ["mkv"] = "matroska", ["webm"] = "webm",
    };

    // The container format and the codec inside it are different things: each target gets
    // a codec its players actually support. yuv420p because most players cannot show anything else.
    private static readonly string[] H264WithAac =
        ["-c:v", "libx264", "-preset", "veryfast", "-crf", "23", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "160k"];

    private static readonly Dictionary<string, string[]> Codecs = new()
    {
        ["mp3"] = ["-c:a", "libmp3lame", "-q:a", "2"],
        ["wav"] = ["-c:a", "pcm_s16le"],
        ["flac"] = ["-c:a", "flac"],
        ["ogg"] = ["-c:a", "libvorbis", "-q:a", "5"],
        ["aac"] = ["-c:a", "aac", "-b:a", "192k"],
        ["mp4"] = [.. H264WithAac, "-movflags", "+faststart"], // metadata first: playback starts before download ends
        ["mov"] = [.. H264WithAac, "-movflags", "+faststart"],
        ["mkv"] = H264WithAac,
        ["webm"] =
        [
            "-c:v", "libvpx-vp9", "-deadline", "realtime", "-cpu-used", "8", "-row-mt", "1",
            "-crf", "32", "-b:v", "0", "-pix_fmt", "yuv420p", "-c:a", "libopus", "-b:a", "128k",
        ],
    };

    public string Name => "FFmpeg";

    // Audio to audio and video to video, every format to every other: 5x4 + 4x3 routes.
    public IReadOnlyCollection<ConversionRoute> SupportedRoutes { get; } =
        AllPairs(AudioFormats).Concat(AllPairs(VideoFormats)).ToArray();

    public Task ConvertAsync(
        ConversionRoute route,
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        return sandbox.RunAsync(settings, BuildToolArguments(route, settings.Threads), inputPath, outputPath, cancellationToken);
    }

    internal static IReadOnlyList<string> BuildToolArguments(ConversionRoute route, int threads)
    {
        var threadCount = threads.ToString(CultureInfo.InvariantCulture);

        // Only the streams we need: subtitles, data streams and cover art are dropped.
        // "0:a:0?" = audio is optional, a silent video is fine.
        string[] streams = AudioFormats.Contains(route.TargetFormat)
            ? ["-map", "0:a:0"]
            : ["-map", "0:v:0", "-map", "0:a:0?"];

        return
        [
            "-hide_banner", "-nostdin", "-loglevel", "error",
            // Input options, they must come before -i. The forced demuxer and the file-only protocol
            // stop known attacks where a crafted file makes FFmpeg read other files or URLs.
            "-protocol_whitelist", "file",
            "-threads", threadCount,
            "-f", Demuxers[route.SourceFormat],
            "-i", DockerSandbox.InputPath,
            .. streams,
            .. Codecs[route.TargetFormat],
            "-threads", threadCount,
            "-f", Muxers[route.TargetFormat],
            "-y", DockerSandbox.OutputPath,
        ];
    }

    private static IEnumerable<ConversionRoute> AllPairs(string[] formats) =>
        formats.SelectMany(source => formats
            .Where(target => target != source)
            .Select(target => new ConversionRoute(source, target)));
}
