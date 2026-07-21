using System.Text;
using EunSlip.Infrastructure.Security;
using Google.Apis.Util.Store;
using Newtonsoft.Json;

namespace EunSlip.Infrastructure.Gmail;

public sealed class DpapiTokenDataStore(string directoryPath) : IDataStore
{
    private readonly string _directory = directoryPath;

    public Task StoreAsync<T>(string key, T value)
    {
        Directory.CreateDirectory(_directory);
        string json = JsonConvert.SerializeObject(value);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        byte[] protectedBytes = DpapiKeyProtector.ProtectToken(bytes);
        File.WriteAllBytes(PathFor(key), protectedBytes);
        return Task.CompletedTask;
    }

    public Task DeleteAsync<T>(string key)
    {
        string path = PathFor(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        return Task.CompletedTask;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        string path = PathFor(key);
        if (!File.Exists(path))
        {
            return default;
        }

        byte[] protectedBytes = await File.ReadAllBytesAsync(path);
        byte[] bytes = DpapiKeyProtector.UnprotectToken(protectedBytes);
        string json = Encoding.UTF8.GetString(bytes);
        return JsonConvert.DeserializeObject<T>(json);
    }

    public Task ClearAsync()
    {
        if (Directory.Exists(_directory))
        {
            foreach (string file in Directory.EnumerateFiles(_directory))
            {
                File.Delete(file);
            }
        }
        return Task.CompletedTask;
    }

    private string PathFor(string key) => Path.Combine(_directory, $"{key}.token");
}
