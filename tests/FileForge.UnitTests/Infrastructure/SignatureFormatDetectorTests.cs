using FileForge.Infrastructure.Conversion;

namespace FileForge.UnitTests.Infrastructure;

public class SignatureFormatDetectorTests
{
    private readonly SignatureFormatDetector _detector = new();

    [Theory]
    [InlineData("89504E470D0A1A0A0000000D", "png")]
    [InlineData("FFD8FFE000104A4649460001", "jpg")]
    [InlineData("52494646240000005745425056503820", "webp")] // RIFF....WEBPVP8
    [InlineData("424D36000000000000003600", "bmp")]
    [InlineData("524946462400000057415645", null)]           // RIFF....WAVE: audio, not WebP
    [InlineData("255044462D312E37", null)]                   // %PDF-1.7
    [InlineData("", null)]
    public async Task DetectAsync_RecognizesBySignature(string hex, string? expected)
    {
        using var content = new MemoryStream(Convert.FromHexString(hex));

        var format = await _detector.DetectAsync(content, CancellationToken.None);

        Assert.Equal(expected, format);
    }
}
