using System.IO;
using System.Text.Json;

namespace PanBridge;

public static class CacheStorageTest
{
    public static void Run()
    {
        var legacy = JsonSerializer.Deserialize<DriveSettings>("{\"DriveLetter\":\"J:\",\"Access\":2}")!;
        if (legacy.CacheDirectory is not null || legacy.CacheMaxBytes != CacheStorage.DefaultMaxBytes || !CacheStorage.SamePath(CacheStorage.FromSettings(legacy).Directory, AppPaths.DefaultCache)) throw new Exception("Legacy cache defaults changed.");
        var half = CacheStorage.Parse(AppPaths.DefaultCache, "0.5");
        if (half.MaxBytes != 536870912) throw new Exception("Fractional GiB conversion failed.");
        foreach (string size in new[] { "", "0", "-1", "NaN", "Infinity", "0.099", "1.234", "1048577", "999999999999999999999999999999999999" })
            Reject(() => CacheStorage.Parse(AppPaths.DefaultCache, size));
        foreach (string path in new[] { "relative-cache", @"\\server\share\cache", Path.GetPathRoot(AppPaths.Data)!, AppPaths.Data, AppPaths.Browser, Path.Combine(AppPaths.Browser, "nested"), AppPaths.Runtime })
            Reject(() => CacheStorage.NormalizePath(path));
        Reject(() => CacheStorage.ValidateTransition(half, half with { Directory = Path.Combine(half.Directory, "nested") }));
        string root = Path.Combine(AppPaths.Data, "self-test-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            CacheStorage.PrepareDirectory(CacheStorage.Parse(root, "0.1"));
            string meta = Path.Combine(root, "vfsMeta", "jbox", "fixture");
            string data = Path.Combine(root, "vfs", "jbox", "fixture");
            Directory.CreateDirectory(Path.GetDirectoryName(meta)!);
            Directory.CreateDirectory(Path.GetDirectoryName(data)!);
            File.WriteAllText(data, "pending test content");
            File.WriteAllText(meta, "{\"Dirty\":true}");
            Reject(() => CacheStorage.EnsureClean(root));
            if (File.ReadAllText(data) != "pending test content") throw new Exception("Pending cached content was changed.");
            File.WriteAllText(meta, "{\"Dirty\":false}");
            CacheStorage.EnsureClean(root);
            File.WriteAllText(meta, "{invalid"); Reject(() => CacheStorage.EnsureClean(root));
            File.WriteAllText(meta, "{}"); Reject(() => CacheStorage.EnsureClean(root));
            File.Delete(meta); Reject(() => CacheStorage.EnsureClean(root));
            File.Delete(data); CacheStorage.EnsureClean(root);
            Directory.Delete(Path.GetDirectoryName(meta)!);
            Directory.Delete(Path.Combine(root, "vfsMeta"));
            File.WriteAllText(Path.Combine(root, "vfsMeta"), "not a directory");
            Reject(() => CacheStorage.EnsureClean(root));
            string conflict = Path.Combine(root, "conflict"); File.WriteAllText(conflict, "file");
            Reject(() => CacheStorage.PrepareDirectory(new(conflict, CacheStorage.DefaultMaxBytes)));
        }
        finally
        {
            // Delete only this newly-created fixture root, never a configured user cache directory.
            if (Path.GetDirectoryName(root) != Path.TrimEndingDirectorySeparator(AppPaths.Data) || !Path.GetFileName(root).StartsWith("self-test-cache-", StringComparison.Ordinal)) throw new Exception("Unexpected test directory.");
            Directory.Delete(root, true);
        }
    }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (CloudAccessException) { return; }
        throw new Exception("Unsafe cache configuration was accepted.");
    }
}
