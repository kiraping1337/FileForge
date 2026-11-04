using FileForge.Infrastructure.Conversion;

namespace FileForge.UnitTests.Infrastructure;

public sealed class DockerSandboxTests
{
    private static IReadOnlyList<string> BuildArguments() =>
        DockerSandbox.BuildRunArguments(
            new SandboxOptions(),
            new ImageMagickOptions(),
            DockerSandbox.BindMounts("C:\\storage\\input", "C:\\work\\abc\\out"),
            "fileforge-test",
            ["tool-arg-1", "tool-arg-2"]);

    [Fact]
    public void BuildRunArguments_HasReadOnlyFlag()
    {
        var args = BuildArguments();

        Assert.Contains("--read-only", args);
    }

    [Fact]
    public void BuildRunArguments_ToolArgumentsComeLast()
    {
        var args = BuildArguments();

        Assert.Equal("tool-arg-1", args[^2]);
        Assert.Equal("tool-arg-2", args[^1]);
    }

    [Fact]
    public void BuildRunArguments_MemorySwapEqualsMemory()
    {
        var args = BuildArguments();

        Assert.Contains("--memory", args);
        Assert.Contains("--memory-swap", args);

        var memoryIndex = args.ToList().IndexOf("--memory");
        var memorySwapIndex = args.ToList().IndexOf("--memory-swap");

        Assert.Equal(args[memoryIndex + 1], args[memorySwapIndex + 1]);
    }

    [Fact]
    public void BuildRunArguments_InputMountIsReadOnly()
    {
        var args = BuildArguments();

        var element = args.Single(a => a.StartsWith("type=bind,source=C:\\storage\\input"));
        Assert.EndsWith("readonly", element);
    }

    [Theory]
    [InlineData("--network", "none")]
    [InlineData("--user", "10001")]
    [InlineData("--cap-drop", "ALL")]
    public void BuildRunArguments_FlagHasExpectedValue(string flag, string expectedValue)
    {
        var args = BuildArguments();
        Assert.Contains(flag, args);

        var i = args.ToList().IndexOf(flag);
        Assert.Equal(expectedValue, args[i + 1]);
    }

    [Fact]
    public void VolumeMounts_InputIsReadOnly_OutputIsNot()
    {
        var mounts = DockerSandbox.VolumeMounts("fileforge-work", "abc123");

        var input = mounts.Single(m => m.Contains("target=/in,"));
        var output = mounts.Single(m => m.Contains("target=/out,"));
        Assert.EndsWith("readonly", input);
        Assert.DoesNotContain("readonly", output);
    }

    [Fact]
    public void VolumeMounts_ExposeOnlyThisConversionsFolders()
    {
        var mounts = DockerSandbox.VolumeMounts("fileforge-work", "abc123");

        // Every mount is a sub-folder of this one conversion, never the whole volume.
        Assert.All(
            mounts.Where(m => m.StartsWith("type=volume")),
            m => Assert.Contains("volume-subpath=abc123/", m));
    }
}
