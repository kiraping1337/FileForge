using FileForge.Application.Conversions;

namespace FileForge.UnitTests.Application;

public class ConversionRegistryTests
{
    private sealed class FakeConverter(string name, IReadOnlyCollection<ConversionRoute> routes) : IFileConverter
    {
        public string Name => name;

        public IReadOnlyCollection<ConversionRoute> SupportedRoutes => routes;

        public Task ConvertAsync(ConversionRoute route, string inputPath, string outputPath, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The registry tests never convert anything.");
    }

    private static ConversionRegistry CreateRegistry() =>
        new([new FakeConverter("images", [new("png", "webp"), new("png", "jpg"), new("bmp", "png")])]);

    [Fact]
    public void SupportedFormats_GroupsTargetsBySource_Sorted()
    {
        var formats = CreateRegistry().SupportedFormats;

        Assert.Equal(2, formats.Count);
        Assert.Equal("bmp", formats[0].Source);
        Assert.Equal(new[] { "png" }, formats[0].Targets);
        Assert.Equal("png", formats[1].Source);
        Assert.Equal(new[] { "jpg", "webp" }, formats[1].Targets);
    }

    [Fact]
    public void Constructor_SameRouteInTwoConverters_Throws()
    {
        IFileConverter[] converters =
        [
            new FakeConverter("first", [new("png", "jpg")]),
            new FakeConverter("second", [new("PNG", "JPG")]),
        ];

        Assert.Throws<InvalidOperationException>(() => new ConversionRegistry(converters));
    }

    [Theory]
    [InlineData("png", "jpg", true)]
    [InlineData("PNG", "Jpg", true)]
    [InlineData("jpg", "png", false)]
    [InlineData("png", "png", false)]
    [InlineData("", "jpg", false)]
    [InlineData(null, "jpg", false)]
    public void IsSupported_ChecksRegisteredRoutes(string? source, string? target, bool expected)
    {
        Assert.Equal(expected, CreateRegistry().IsSupported(source, target));
    }
}
