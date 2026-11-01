namespace FileForge.Infrastructure.Conversion;

public sealed class ImageMagickOptions : ConverterContainerOptions
{
    public const string SectionName = "ImageMagick";

    public ImageMagickOptions()
    {
        // Built from converters/imagemagick. MemoryLimit must exceed the memory limit in policy.xml plus /tmp.
        Image = "fileforge-imagemagick:1";
        MemoryLimit = "768m";
        CpuLimit = "1";
        Timeout = TimeSpan.FromSeconds(30);
    }
}
