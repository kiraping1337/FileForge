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
    [InlineData("524946462400000057415645", "wav")]          // RIFF....WAVE: audio, not WebP
    [InlineData("664C614300000022", "flac")]
    [InlineData("4F67675300020000", "ogg")]
    [InlineData("000000186674797069736F6D", "mp4")]
    [InlineData("000000146674797071742020", "mov")]
    [InlineData("1A45DFA39F4282886D6174726F736B61", "mkv")]
    [InlineData("1A45DFA39F4282847765626D", "webm")]
    [InlineData("1A45DFA39F4282", null)]                     // EBML without type
    [InlineData("49443304000000000000", "mp3")]
    [InlineData("FFFB9064", "mp3")]
    [InlineData("FFF15080", "aac")]
    [InlineData("255044462D312E37", null)]                   // %PDF-1.7
    [InlineData("FF", null)]
    [InlineData("FF00", null)]
    [InlineData("6674797000000000", null)]
    [InlineData("", null)]
    public async Task DetectAsync_RecognizesBySignature(string hex, string? expected)
    {
        using var content = new MemoryStream(Convert.FromHexString(hex));

        var format = await _detector.DetectAsync(content, CancellationToken.None);

        Assert.Equal(expected, format);
    }
}
