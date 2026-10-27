using System.Globalization;
using System.IO;
using System.Text.Json;

namespace PanBridge;

public record CacheConfiguration(string Directory, long MaxBytes);

public static class CacheStorage
{
    public const long DefaultMaxBytes = 1024L * 1024 * 1024;
    private const decimal GiB = DefaultMaxBytes;
    public static CacheConfiguration FromSettings(DriveSettings settings) => new(
        NormalizePath(string.IsNullOrWhiteSpace(settings.CacheDirectory) ? AppPaths.DefaultCache : settings.CacheDirectory), settings.CacheMaxBytes);
    public static string FormatGiB(long bytes) => (bytes / GiB).ToString("0.##", CultureInfo.CurrentCulture);
    public static bool SamePath(string left, string right) => string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)), StringComparison.OrdinalIgnoreCase);
    private static bool Contains(string parent, string child) => SamePath(parent, child) || child.StartsWith(Path.TrimEndingDirectorySeparator(parent) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    public static string NormalizePath(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || !Path.IsPathFullyQualified(input.Trim()) || input.Trim().StartsWith(@"\\"))
            throw new CloudAccessException("请选择本机磁盘上的完整目录路径，例如 D:\\PanBridgeCache。");
        string path;
        try { path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(input.Trim())); }
        catch { throw new CloudAccessException("缓存目录路径无效。"); }
        string root = Path.GetPathRoot(path)!;
        if (root.Length != 3 || root[1] != ':' || SamePath(path, root) || path[root.Length..].IndexOfAny(['<', '>', ':', '"', '|', '?', '*']) >= 0)
            throw new CloudAccessException("请选择本机磁盘中的文件夹，不能使用磁盘根目录或特殊设备路径。");
        if (Contains(path, AppPaths.Data) || Contains(AppPaths.Browser, path) || Contains(AppPaths.Runtime, path))
            throw new CloudAccessException("缓存需使用独立文件夹，不能与登录数据目录或程序组件目录混用。");
        return path;
    }
    public static CacheConfiguration Parse(string directory, string sizeGiB)
    {
        const NumberStyles style = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;
        if (!(decimal.TryParse(sizeGiB, style, CultureInfo.CurrentCulture, out decimal size) || decimal.TryParse(sizeGiB, style, CultureInfo.InvariantCulture, out size)) || size < 0.1m || size > 1048576m || decimal.Round(size, 2) != size)
            throw new CloudAccessException("最大缓存空间请输入 0.1–1048576 GiB 之间的数字，最多两位小数。");
        return new(NormalizePath(directory), decimal.ToInt64(decimal.Round(size * GiB, 0, MidpointRounding.AwayFromZero)));
    }
    public static void ValidateTransition(CacheConfiguration current, CacheConfiguration next)
    {
        if (!SamePath(current.Directory, next.Directory) && (Contains(current.Directory, next.Directory) || Contains(next.Directory, current.Directory)))
            throw new CloudAccessException("新旧缓存目录不能互相包含，请选择另一个独立文件夹。");
    }
    public static DriveInfo PrepareDirectory(CacheConfiguration configuration)
    {
        string path = NormalizePath(configuration.Directory);
        if (configuration.MaxBytes < 107374182 || configuration.MaxBytes > 1048576m * GiB)
            throw new CloudAccessException("缓存空间上限无效，请重新设置。");
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(path)!);
            if (!drive.IsReady || drive.DriveType is not (DriveType.Fixed or DriveType.Removable))
                throw new CloudAccessException("缓存目录必须位于可用的本机磁盘，不能放在云盘盘符或网络共享中。");
            if (drive.DriveFormat is not ("NTFS" or "ReFS"))
                throw new CloudAccessException("请选择 NTFS 或 ReFS 磁盘，以支持文件缓存所需的稀疏文件。");
            // Reject junctions/symlinks so a nominal local path cannot point into a mounted cloud drive.
            for (var part = new DirectoryInfo(path); part is not null; part = part.Parent)
                if (part.Exists && part.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    throw new CloudAccessException("缓存目录不能经过符号链接或目录联接，请直接选择实际的本机目录。");
            System.IO.Directory.CreateDirectory(path);
            string probe = Path.Combine(path, ".panbridge-write-" + Guid.NewGuid().ToString("N"));
            using (var file = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) file.WriteByte(0);
            return drive;
        }
        catch (CloudAccessException) { throw; }
        catch { throw new CloudAccessException("缓存目录无法创建或写入，请检查路径、磁盘和文件夹权限。"); }
    }
    public static void EnsureClean(string directory)
    {
        // Pinned rclone 1.75.2 persists unuploaded changes with Dirty=true in vfsMeta.
        // Only inspect metadata; never copy, move or delete cached file contents.
        string meta = Path.Combine(directory, "vfsMeta");
        string data = Path.Combine(directory, "vfs");
        try
        {
            foreach (string file in EnumerateFiles(meta))
            {
                if (new FileInfo(file).Length > 8 * 1024 * 1024) throw new InvalidDataException();
                using var stream = File.OpenRead(file);
                using var json = JsonDocument.Parse(stream);
                if (!json.RootElement.TryGetProperty("Dirty", out var dirty) || dirty.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new InvalidDataException();
                if (dirty.GetBoolean()) throw new CloudAccessException("缓存中仍有未上传文件，暂不切换目录。请先保存并关闭云盘文件，使用当前缓存连接云盘，等待上传完成后重试。");
            }
            foreach (string file in EnumerateFiles(data))
                if (!File.Exists(Path.Combine(meta, Path.GetRelativePath(data, file)))) throw new InvalidDataException();
        }
        catch (CloudAccessException) { throw; }
        catch { throw new CloudAccessException("无法确认缓存文件的上传状态，暂不切换目录。请保留缓存，并使用原目录连接云盘后重试。"); }
    }
    private static IEnumerable<string> EnumerateFiles(string root)
    {
        bool missing = false;
        FileAttributes attributes = default;
        try { attributes = File.GetAttributes(root); }
        catch (FileNotFoundException) { missing = true; }
        catch (DirectoryNotFoundException) { missing = true; }
        if (missing) yield break;
        if (!attributes.HasFlag(FileAttributes.Directory)) throw new InvalidDataException();
        var pending = new Stack<string>(); pending.Push(root);
        while (pending.TryPop(out string? current))
        {
            if (File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException();
            foreach (string entry in System.IO.Directory.EnumerateFileSystemEntries(current))
            {
                var entryAttributes = File.GetAttributes(entry);
                if (entryAttributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException();
                if (entryAttributes.HasFlag(FileAttributes.Directory)) pending.Push(entry);
                else yield return entry;
            }
        }
    }
}
