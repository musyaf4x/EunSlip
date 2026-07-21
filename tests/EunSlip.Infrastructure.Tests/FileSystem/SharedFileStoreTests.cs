using EunSlip.Core.Payroll;
using EunSlip.Infrastructure.FileSystem;

namespace EunSlip.Infrastructure.Tests.FileSystem;

public sealed class SharedFileStoreTests : IDisposable
{
    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";
    private const string TinyJpegBase64 =
        "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAAMCAgICAgMCAgIDAwMDBAYEBAQEBAgGBgUGCQgKCgkICQkKDA8MCgsOCwkJDRENDg8QEBEQCgwSExIQEw8QEBD/wAARCAABAAEDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD3+iiigD//2Q==";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "eunslip-tests", Guid.NewGuid().ToString("N"));
    private readonly SharedFileStore _store;

    public SharedFileStoreTests()
    {
        AppPaths paths = new(_root);
        paths.EnsureCreated();
        _store = new SharedFileStore(paths);
    }

    private string WriteSource(string fileName, byte[] content)
    {
        string path = Path.Combine(_root, fileName);
        File.WriteAllBytes(path, content);
        return path;
    }

    private string ValidPng() => WriteSource("source.png", Convert.FromBase64String(TinyPngBase64));
    private string ValidJpeg() => WriteSource("source.jpg", Convert.FromBase64String(TinyJpegBase64));

    [Fact]
    public void Import_Png_CopiesIntoStampDirectory()
    {
        string source = ValidPng();

        string imported = _store.ImportStamp(source);

        Assert.True(File.Exists(imported));
        Assert.EndsWith("stamp.png", imported);
        Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(imported));
        Assert.Equal(imported, _store.GetActiveStampPath());
    }

    [Fact]
    public void Import_Jpeg_IsAccepted()
    {
        string imported = _store.ImportStamp(ValidJpeg());

        Assert.EndsWith("stamp.jpg", imported);
    }

    [Fact]
    public void Import_UnsupportedExtension_Throws()
    {
        string gif = WriteSource("source.gif", Convert.FromBase64String(TinyPngBase64));

        Assert.Throws<StampValidationException>(() => _store.ImportStamp(gif));
    }

    [Fact]
    public void Import_UnreadableImage_Throws()
    {
        string garbage = WriteSource("garbage.png", "not an image"u8.ToArray());

        Assert.Throws<StampValidationException>(() => _store.ImportStamp(garbage));
    }

    [Fact]
    public void Import_OversizedImage_Throws()
    {
        string path = Path.Combine(_root, "big.png");
        using (FileStream stream = File.Create(path))
        {
            stream.SetLength(6 * 1024 * 1024);
        }

        Assert.Throws<StampValidationException>(() => _store.ImportStamp(path));
    }

    [Fact]
    public void Import_ReplacesExistingStamp()
    {
        _store.ImportStamp(ValidPng());

        _store.ImportStamp(ValidJpeg());

        Assert.EndsWith("stamp.jpg", _store.GetActiveStampPath());
        Assert.Single(Directory.GetFiles(Path.Combine(_root, "stamp")));
    }

    [Fact]
    public void Remove_DeletesActiveStamp()
    {
        _store.ImportStamp(ValidPng());

        _store.RemoveStamp();

        Assert.Null(_store.GetActiveStampPath());
    }

    [Fact]
    public void GetActiveStampPath_IsNull_WhenNoStampExists()
    {
        Assert.Null(_store.GetActiveStampPath());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
