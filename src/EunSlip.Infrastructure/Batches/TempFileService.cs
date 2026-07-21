using EunSlip.Core.Batches;
using EunSlip.Infrastructure.FileSystem;

namespace EunSlip.Infrastructure.Batches;

public sealed class TempFileService(AppPaths paths) : ITempFileService
{
    private readonly AppPaths _paths = paths;

    public string CreateBatchTempDirectory(Guid batchId)
    {
        string dir = Path.Combine(_paths.TempDirectory, batchId.ToString("N"));
        _ = Directory.CreateDirectory(dir);
        return dir;
    }

    public void DeleteFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public void CleanupLeftovers()
    {
        if (!Directory.Exists(_paths.TempDirectory))
        {
            return;
        }

        foreach (string dir in Directory.EnumerateDirectories(_paths.TempDirectory))
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
