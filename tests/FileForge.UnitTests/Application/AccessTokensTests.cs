using FileForge.Application.Jobs;

namespace FileForge.UnitTests.Application;

public class AccessTokensTests
{
    [Fact]
    public void TryHash_GeneratedToken_ReturnsSameHash()
    {
        var (token, hash) = AccessTokens.Generate();
        Assert.Equal(hash, AccessTokens.TryHash(token));
    }

    [Fact]
    public void Generate_TwoCalls_ReturnDifferentTokens()
    {
        var (token1, _) = AccessTokens.Generate();
        var (token2, _) = AccessTokens.Generate();
        Assert.NotEqual(token1, token2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!")]
    public void TryHash_MalformedToken_ReturnsNull(string? token)
    {
        Assert.Null(AccessTokens.TryHash(token));
    }
}
