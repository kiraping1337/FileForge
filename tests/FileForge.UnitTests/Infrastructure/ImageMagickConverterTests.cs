using FileForge.Application.Conversions;
using FileForge.Infrastructure.Conversion;

namespace FileForge.UnitTests.Infrastructure;

public sealed class ImageMagickConverterTests
{
    private static IReadOnlyList<string> BuildArguments() =>
        ImageMagickConverter.BuildDockerArguments(
            new SandboxOptions(),
            new ConversionRoute("png", "jpg"),
            "C:\\storage\\input",
            "C:\\work\\abc",
            "fileforge-im-test");


    [Fact]
    public void BuildDockerArguments_HasReadOnlyFlag()
    {
        var args = BuildArguments();
        Assert.Contains("--read-only", args);
    }

    [Fact]
    public void BuildDockerArguments_HasExpectedLastTwoArguments()
    {
        var args = BuildArguments();
        Assert.Equal("png:/in/input", args[^2]);
        Assert.Equal("jpg:/out/result", args[^1]);
    }

    [Fact]
    public void BuildDockerArguments_MemorySwapEqualsMemory()
    {
        var args = BuildArguments();

        Assert.Contains("--memory", args);
        Assert.Contains("--memory-swap", args);

        var memoryIndex = args.ToList().IndexOf("--memory");
        var memorySwapIndex = args.ToList().IndexOf("--memory-swap");

        Assert.Equal(args[memoryIndex + 1], args[memorySwapIndex + 1]);
    }

    [Fact]
    public void BuildDockerArguments_InputMountIsReadOnly()
    {
        var args = BuildArguments();
        var element = args.Single(a => a.StartsWith("type=bind,source=C:\\storage\\input"));
        Assert.EndsWith("readonly", element);
    }

    [Theory]
    [InlineData("--network", "none")]
    [InlineData("--user", "10001")]
    [InlineData("--cap-drop", "ALL")]
    public void BuildDockerArguments_FlagHasExpectedValue(string flag, string expectedValue)
    {
        var args = BuildArguments();
        Assert.Contains(flag, args);

        var i = args.ToList().IndexOf(flag);
        Assert.Equal(expectedValue, args[i + 1]);
    }
}
