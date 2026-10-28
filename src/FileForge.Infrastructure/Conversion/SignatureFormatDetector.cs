using FileForge.Application.Conversions;

namespace FileForge.Infrastructure.Conversion;

/// <summary>
/// Recognizes images by their first bytes ("magic numbers"). A quick first filter only:
/// a file with a valid header can still be broken inside. Full validation by ImageMagick comes in stage 3.
/// </summary>
internal sealed class SignatureFormatDetector : IFormatDetector
{
    private const int HeaderLength = 12;

    private static ReadOnlySpan<byte> Png => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static ReadOnlySpan<byte> Jpeg => new byte[] { 0xFF, 0xD8, 0xFF };

    public async Task<string?> DetectAsync(Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        var header = new byte[HeaderLength];
        var read = await content.ReadAtLeastAsync(header, HeaderLength, throwOnEndOfStream: false, cancellationToken);

        return Detect(header.AsSpan(0, read));
    }

    private static string? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(Png))
        {
            return "png";
        }

        if (header.StartsWith(Jpeg))
        {
            return "jpg";
        }

        // RIFF is a container shared with WAV and AVI; the type sits at bytes 8..11.
        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            return "webp";
        }

        if (header.StartsWith("BM"u8))
        {
            return "bmp";
        }

        return null;
    }
}
