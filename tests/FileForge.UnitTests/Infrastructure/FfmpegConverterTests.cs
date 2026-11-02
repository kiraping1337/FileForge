using FileForge.Application.Conversions;
using FileForge.Infrastructure.Conversion;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FileForge.UnitTests.Infrastructure;

public sealed class FfmpegConverterTests
{
    private const long MaxOutputBytes = 1_000_000;

    private static List<string> BuildArguments(string source, string target) =>
        FfmpegConverter.BuildToolArguments(new ConversionRoute(source, target), threads: 2, MaxOutputBytes).ToList();

    [Fact]
    public void BuildToolArguments_LimitsOutputSize()
    {
        var args = BuildArguments("mp3", "flac");

        var index = args.IndexOf("-fs");
        Assert.True(index > args.IndexOf("-i"), "-fs must be an output option, after -i");
        Assert.Equal("1000000", args[index + 1]);
    }

    [Theory]
    [InlineData("mp4", "mkv", "mov")]
    [InlineData("mkv", "mp4", "matroska")]
    [InlineData("webm", "mp4", "matroska")]
    [InlineData("aac", "mp3", "aac")]
    public void BuildToolArguments_ForcesInputFormatBeforeInput(string source, string target, string expectedDemuxer)
    {
        var args = BuildArguments(source, target);

        var inputIndex = args.IndexOf("-i");
        Assert.True(inputIndex >= 2, "-i is missing");
        Assert.Equal("-f", args[inputIndex - 2]);
        Assert.Equal(expectedDemuxer, args[inputIndex - 1]);
    }

    [Fact]
    public void BuildToolArguments_AllowsOnlyFileProtocolForInput()
    {
        var args = BuildArguments("mp4", "webm");

        var protocolIndex = args.IndexOf("-protocol_whitelist");
        Assert.True(protocolIndex >= 0, "-protocol_whitelist is missing");
        Assert.Equal("file", args[protocolIndex + 1]);
        Assert.True(protocolIndex < args.IndexOf("-i"), "-protocol_whitelist must come before -i");
    }

    [Theory]
    [InlineData("mp4", "mkv")]
    [InlineData("webm", "mp4")]
    [InlineData("mkv", "mov")]
    [InlineData("mov", "webm")]
    public void BuildToolArguments_VideoTarget_UsesYuv420p(string source, string target)
    {
        var args = BuildArguments(source, target);

        var index = args.IndexOf("-pix_fmt");
        Assert.True(index >= 0, "-pix_fmt is missing");
        Assert.Equal("yuv420p", args[index + 1]);
    }

    [Fact]
    public void BuildToolArguments_AudioTarget_MapsOnlyAudio()
    {
        var args = BuildArguments("wav", "mp3");

        Assert.Single(args, a => a == "-map");
        Assert.Equal("0:a:0", args[args.IndexOf("-map") + 1]);
    }

    [Fact]
    public void BuildToolArguments_WritesToSandboxOutput()
    {
        var args = BuildArguments("wav", "aac");

        Assert.Equal(new[] { "-f", "adts", "-y", "/out/result" }, args.TakeLast(4));
    }

    [Fact]
    public void SupportedRoutes_AudioAndVideoAreNotMixed()
    {
        var sandbox = new DockerSandbox(Options.Create(new SandboxOptions()), NullLogger<DockerSandbox>.Instance);
        var converter = new FfmpegConverter(sandbox, Options.Create(new FfmpegOptions()));

        var routes = converter.SupportedRoutes;

        Assert.Equal(32, routes.Count); // 5x4 audio + 4x3 video
        Assert.DoesNotContain(routes, r => r.SourceFormat == "mp4" && r.TargetFormat == "mp3");
    }
}
