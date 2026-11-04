namespace FileForge.IntegrationTests.Infrastructure;

/// <summary>
/// Tiny files that pass the signature check. The content after the header does not matter:
/// the test converter only copies it.
/// </summary>
public static class TestFiles
{
    public static readonly byte[] Png =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];

    public static readonly byte[] Text = "just some text, not an image"u8.ToArray();
}
