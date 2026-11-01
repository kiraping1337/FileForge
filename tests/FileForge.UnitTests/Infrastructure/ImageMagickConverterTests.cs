using FileForge.Application.Conversions;
using FileForge.Infrastructure.Conversion;

namespace FileForge.UnitTests.Infrastructure;

public sealed class ImageMagickConverterTests
{
    [Fact]
    public void BuildToolArguments_ForcesFormatsOnInputAndOutput()
    {
        var args = ImageMagickConverter.BuildToolArguments(new ConversionRoute("png", "jpg"));

        Assert.Equal(new[] { "png:/in/input", "jpg:/out/result" }, args);
    }
}
