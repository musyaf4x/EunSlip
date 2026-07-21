using EunSlip.Core.Payroll;
using PdfSharp.Drawing;

namespace EunSlip.Infrastructure.FileSystem;

public sealed class SharedFileStore(AppPaths paths) : ISharedFileStore
{
    private const long MaxStampBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };

    private readonly AppPaths _paths = paths;

    public string? GetActiveStampPath()
    {
        return Directory
            .EnumerateFiles(_paths.StampDirectory, "stamp.*")
            .Where(f => AllowedExtensions.Contains(Path.GetExtension(f)))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public string ImportStamp(string sourcePath)
    {
        Validate(sourcePath);
        string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        string destination = Path.Combine(_paths.StampDirectory, $"stamp{extension}");
        string temporary = Path.Combine(_paths.StampDirectory, "stamp.tmp");

        File.Copy(sourcePath, temporary, overwrite: true);
        try
        {
            ValidateImage(temporary);
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }

        RemoveStamp();
        File.Move(temporary, destination);
        return destination;
    }

    public void RemoveStamp()
    {
        foreach (string file in Directory
            .EnumerateFiles(_paths.StampDirectory, "stamp.*")
            .Where(f => AllowedExtensions.Contains(Path.GetExtension(f))))
        {
            File.Delete(file);
        }
    }

    private static void Validate(string sourcePath)
    {
        if (!File.Exists(sourcePath))
        {
            throw new StampValidationException("Stamp file does not exist.");
        }

        if (!AllowedExtensions.Contains(Path.GetExtension(sourcePath)))
        {
            throw new StampValidationException("Stamp must be a PNG or JPEG image.");
        }

        ValidateImage(sourcePath);
    }

    private static void ValidateImage(string sourcePath)
    {
        if (new FileInfo(sourcePath).Length > MaxStampBytes)
        {
            throw new StampValidationException("Stamp image is too large (maximum 5 MB).");
        }

        try
        {
            using XImage image = XImage.FromFile(sourcePath);
            if (image.PixelWidth == 0 || image.PixelHeight == 0)
            {
                throw new StampValidationException("Stamp image is empty.");
            }
        }
        catch (Exception ex) when (ex is not StampValidationException)
        {
            throw new StampValidationException("Stamp image is unreadable.");
        }
    }
}
