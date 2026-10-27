using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Web.WebView2.Core;

namespace PanBridge;

public record TransferStatus(long Queued, long Active, long Errors)
{
    public string Summary => Errors > 0 ? $"{Errors} 个上传错误，请保持连接并检查网络" : Queued + Active > 0 ? $"{Active} 个上传中 · {Queued} 个等待中" : "上传队列为空";
}
public enum CheckResult { Passed, Warning, Failed, Skipped }
public record DiagnosticCheck(string Name, CheckResult Result, string Detail, string Advice = "")
{
    public string Label => Result switch { CheckResult.Passed => "正常", CheckResult.Warning => "注意", CheckResult.Failed => "异常", _ => "未检查" };
    public string Color => Result switch { CheckResult.Passed => "#18765E", CheckResult.Warning => "#966018", CheckResult.Failed => "#C43F45", _ => "#748094" };
    public string AdviceText => string.IsNullOrWhiteSpace(Advice) ? "" : "建议：" + Advice;
}

public static class ProductInfo
{
    public static string Version => typeof(ProductInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "未知";
    public static string Runtime => $"{RuntimeInformation.FrameworkDescription} · {RuntimeInformation.ProcessArchitecture}";
    public static string WebViewVersion
    {
        get { try { return CoreWebView2Environment.GetAvailableBrowserVersionString(); } catch { return "未安装"; } }
    }
    public static string ComponentVersion(string path)
    {
        try { return File.Exists(path) ? FileVersionInfo.GetVersionInfo(path).ProductVersion?.Split('+')[0] ?? "已包含" : "缺失"; }
        catch { return "无法读取"; }
    }
}

public static class Diagnostics
{
    public static async Task RunAsync(TboxBackend backend, MountManager mount, Func<Task<int>>? validate, Action<DiagnosticCheck> add)
    {
        add(new("挂载组件 · WinFsp", MountManager.DriverInstalled ? CheckResult.Passed : CheckResult.Failed,
            MountManager.DriverInstalled ? "Windows 已注册 WinFsp。" : "未检测到 WinFsp，暂时无法连接盘符。", MountManager.DriverInstalled ? "" : "前往设置安装挂载组件；若刚完成安装，请重启 Windows。"));
        foreach (var item in new[] { ("云盘桥接 · Tbox", AppPaths.Tbox), ("文件挂载 · rclone", AppPaths.Rclone) })
        {
            bool exists = File.Exists(item.Item2);
            add(new(item.Item1, exists ? CheckResult.Passed : CheckResult.Failed, exists ? "组件文件存在。版本 " + ProductInfo.ComponentVersion(item.Item2) : "组件文件缺失。", exists ? "" : "完整解压运行包，不要仅复制主程序。"));
        }
        bool browser = ProductInfo.WebViewVersion != "未安装";
        add(new("官方登录 · WebView2", browser ? CheckResult.Passed : CheckResult.Failed, browser ? ProductInfo.WebViewVersion : "未检测到浏览器运行时。", browser ? "" : "安装 Microsoft Edge WebView2 Runtime 后重新打开客户端。"));
        // This temporary write checks application storage only. It never touches the cloud or credential files.
        string probe = Path.Combine(AppPaths.Data, ".write-check-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(probe, "PanBridge storage check");
            File.Delete(probe);
            var disk = await Task.Run(() => CacheStorage.PrepareDirectory(mount.Cache));
            long free = disk.AvailableFreeSpace;
            add(new("本地数据与缓存", free < 1024L * 1024 * 1024 ? CheckResult.Warning : CheckResult.Passed,
                $"数据与缓存目录可写；缓存磁盘剩余 {free / 1024d / 1024 / 1024:0.0} GiB，上限 {CacheStorage.FormatGiB(mount.Cache.MaxBytes)} GiB。", free < 1024L * 1024 * 1024 ? "释放本地磁盘空间，避免影响文件缓存与上传。" : ""));
        }
        catch { add(new("本地数据与缓存", CheckResult.Failed, "数据或缓存目录不可写，或无法读取磁盘空间。", "检查程序目录和所选缓存磁盘的可用性与写入权限。")); }
        finally { try { if (File.Exists(probe)) File.Delete(probe); } catch { } }
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            var timer = Stopwatch.StartNew();
            using var response = await client.GetAsync("https://pan.sjtu.edu.cn/", HttpCompletionOption.ResponseHeadersRead);
            bool ok = response.IsSuccessStatusCode;
            add(new("云盘网络 · HTTPS", ok ? CheckResult.Passed : CheckResult.Failed, $"HTTP {(int)response.StatusCode} · {timer.ElapsedMilliseconds} ms", ok ? "" : "检查网络连接或稍后重试。"));
        }
        catch (Exception ex) { add(new("云盘网络 · HTTPS", CheckResult.Failed, SafeError.Describe(ex), "检查网络、系统时间和校园网访问情况。")); }
        if (validate is null) add(new("云盘登录与根目录", CheckResult.Skipped, "尚未登录，无法验证目录权限。", "前往连接页面完成官方登录。"));
        else
        {
            try { int count = await validate(); add(new("云盘登录与根目录", CheckResult.Passed, $"凭证验证通过，根目录 {count} 个项目。")); }
            catch (Exception ex) { add(new("云盘登录与根目录", CheckResult.Failed, SafeError.Describe(ex), "网络正常时仍失败，请重新登录。")); }
        }
        try
        {
            bool ok = await backend.ProbeAsync();
            add(new("本地桥接服务", ok ? CheckResult.Passed : backend.IsRunning ? CheckResult.Failed : CheckResult.Skipped,
                ok ? $"127.0.0.1:{backend.Address!.Port} 可访问，未认证请求被拒绝。" : "服务未运行或响应异常。", ok ? "" : "完成登录或重试连接；持续异常时重新启动客户端。"));
        }
        catch (Exception ex) { add(new("本地桥接服务", CheckResult.Failed, SafeError.Describe(ex), "重新启动客户端后重试。")); }
        if (!mount.IsMounted) add(new("盘符与上传队列", CheckResult.Skipped, "盘符未连接。检测不会自动挂盘，也不会上传测试文件。"));
        else
        {
            try
            {
                var status = await mount.ReadTransferStatusAsync();
                add(new("盘符与上传队列", status.Errors > 0 ? CheckResult.Failed : status.Active + status.Queued > 0 ? CheckResult.Warning : CheckResult.Passed,
                    $"{mount.Drive} 设备已连接；{status.Summary}。", status.Errors > 0 ? "保持客户端运行，检查网络后等待上传完成。" : ""));
            }
            catch (Exception ex) { add(new("盘符与上传队列", CheckResult.Failed, SafeError.Describe(ex), "暂时无法确认上传状态，请保持连接并稍后重试。")); }
        }
    }

    // An explicit allowlist: reports never serialize sessions, settings, process environments, paths or exception objects.
    public static string BuildReport(IEnumerable<DiagnosticCheck> checks, DateTimeOffset time)
    {
        var text = new StringBuilder($"PanBridge {ProductInfo.Version} 本地诊断报告\n检测时间：{time:yyyy-MM-dd HH:mm:ss zzz}\n运行时：{ProductInfo.Runtime}\n系统：Windows {Environment.OSVersion.Version}\n\n");
        foreach (var check in checks)
            text.AppendLine($"[{check.Label}] {check.Name}\n{check.Detail}\n{check.AdviceText}\n");
        return text.ToString();
    }
}
