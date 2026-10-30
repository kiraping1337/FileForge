using FileForge.Application.Errors;
using FileForge.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace FileForge.UnitTests.Infrastructure;

// xUnit creates a new instance for every test, so each test gets its own empty folder;
// Dispose runs after each test and removes it.
public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "FileForge.Tests", Guid.NewGuid().ToString("N"));
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
    {
        _storage = new LocalFileStorage(Options.Create(new StorageOptions { RootPath = _root }));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task SaveAsync_ThenOpenRead_ReturnsSameBytes()
    {
        byte[] data = [1, 2, 3, 4, 5];

        var stored = await _storage.SaveAsync(new MemoryStream(data), maxBytes: 10, CancellationToken.None);

        Assert.Equal(5L, stored.SizeBytes);
        await using var saved = _storage.OpenRead(stored.StorageKey);
        using var copy = new MemoryStream();
        await saved.CopyToAsync(copy);
        Assert.Equal(data, copy.ToArray());
    }

    [Fact]
    public async Task SaveAsync_TooLarge_ThrowsAndLeavesNoFile()
    {
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _storage.SaveAsync(new MemoryStream(new byte[11]), maxBytes: 10, CancellationToken.None));

        Assert.Equal(ErrorCodes.FileTooLarge, ex.Code);
        Assert.Empty(Directory.GetFiles(_root));
    }

    [Fact]
    public async Task ListFiles_IgnoresFilesItDidNotCreate()
    {
        var stored = await _storage.SaveAsync(new MemoryStream([1, 2, 3]), maxBytes: 10, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(_root, "notes.txt"), "not ours");

        var files = _storage.ListFiles().ToList();

        var single = Assert.Single(files);
        Assert.Equal(stored.StorageKey, single.StorageKey);
    }

    [Theory]
    [InlineData("../../appsettings.json")]
    [InlineData("ABCDEF0123456789ABCDEF0123456789")] // valid length, but upper case
    [InlineData("")]
    public void OpenRead_InvalidKey_Throws(string key)
    {
        Assert.Throws<ArgumentException>(() => _storage.OpenRead(key));
    }
}
