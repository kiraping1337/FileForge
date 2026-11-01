using FileForge.Application.Conversions;

namespace FileForge.Infrastructure.Conversion;

/// <summary>
/// Recognizes images, audio and video by their first bytes
/// </summary>
internal sealed class SignatureFormatDetector : IFormatDetector
{
    private const int HeaderLength = 64;

    private static ReadOnlySpan<byte> Png => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static ReadOnlySpan<byte> Jpeg => new byte[] { 0xFF, 0xD8, 0xFF };

    private static ReadOnlySpan<byte> Ebml => new byte[] { 0x1A, 0x45, 0xdF, 0xA3 };

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

        if (header.StartsWith("fLaC"u8))
        {
            return "flac";
        }

        if (header.StartsWith("OggS"u8))
        {
            return "ogg";
        }

        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WAVE"u8))
        {
            return "wav";
        }

        if (header.Length >= 12 && header[4..8].SequenceEqual("ftyp"u8) && header[8..12].SequenceEqual("qt  "u8))
        {
            return "mov";
        }

        if (header.Length >= 12 && header[4..8].SequenceEqual("ftyp"u8))
        {
            return "mp4";
        }

        if (header.StartsWith(Ebml) && header[4..].IndexOf("webm"u8) != -1)
        {
            return "webm";
        }

        if (header.StartsWith(Ebml) && header[4..].IndexOf("matroska"u8) != -1)
        {
            return "mkv";
        }

        if (header.StartsWith("ID3"u8) || (header.Length >= 2 && header[0] == 0xFF && (header[1] & 0xE6) == 0xE2))
        {
            return "mp3";
        }

        if (header.Length >= 2 && header[0] == 0xFF && (header[1] & 0xF6) == 0xF0)
        {
            return "aac";
        }

        return null;
    }
}
