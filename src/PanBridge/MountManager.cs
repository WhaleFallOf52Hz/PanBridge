using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using Microsoft.Win32;

namespace PanBridge;

public sealed class MountManager : IDisposable
{
    private Process? process;
    private Uri? controlAddress;
    private string? controlPassword;
    private DriveAccess access;
    private readonly string cacheDirectory;
    public MountManager(string? cacheDirectory = null) => this.cacheDirectory = cacheDirectory ?? Path.Combine(AppPaths.Data, "cache");
    public string? Drive { get; private set; }
    public bool IsMounted => process is { HasExited: false } && Drive is not null && Directory.Exists(Drive + "\\");
    public static bool DriverInstalled => Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WinFsp") is not null || Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\WinFsp") is not null;
    public static List<string> AvailableDrives() => Enumerable.Range('D', 'Z' - 'D' + 1).Select(c => $"{(char)c}:").Where(d => !Directory.Exists(d + "\\") && !DriveInfo.GetDrives().Any(v => v.Name.StartsWith(d, StringComparison.OrdinalIgnoreCase))).ToList();
    public static async Task<bool> InstallDriverAsync()
    {
        if (!File.Exists(AppPaths.Installer)) throw new CloudAccessException("缺少 WinFsp 安装包，请完整解压应用后重试。");
        var start = new ProcessStartInfo("msiexec.exe") { UseShellExecute = true, Verb = "runas" };
        start.Arguments = $"/i \"{AppPaths.Installer}\" /passive /norestart";
        using var install = Process.Start(start) ?? throw new IOException("Installer did not start.");
        await install.WaitForExitAsync();
        if (install.ExitCode is not (0 or 3010)) throw new CloudAccessException($"挂载组件安装未完成（代码 {install.ExitCode}）。");
        return install.ExitCode == 3010;
    }
    public async Task MountAsync(Uri address, string token, string drive, DriveAccess requestedAccess)
    {
        if (process is { HasExited: false }) throw new CloudAccessException("请先断开当前盘符。");
        if (!DriverInstalled) throw new CloudAccessException("首次使用需要安装挂载组件，请点击“安装挂载组件”。");
        if (!AvailableDrives().Contains(drive)) throw new CloudAccessException("这个盘符已被占用，请选择其他盘符。");
        if (!File.Exists(AppPaths.Rclone)) throw new CloudAccessException("缺少 rclone 组件，请完整解压应用后重试。");
        var obscureStart = new ProcessStartInfo(AppPaths.Rclone) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        obscureStart.ArgumentList.Add("obscure"); obscureStart.ArgumentList.Add("-");
        using var obscure = Process.Start(obscureStart) ?? throw new IOException("rclone did not start.");
        await obscure.StandardInput.WriteLineAsync(token); obscure.StandardInput.Close();
        string password = (await obscure.StandardOutput.ReadToEndAsync()).Trim();
        await obscure.WaitForExitAsync();
        if (obscure.ExitCode != 0 || password.Length == 0) throw new IOException("Could not prepare mount credentials.");
        var start = new ProcessStartInfo(AppPaths.Rclone) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = AppPaths.Root };
        foreach (var arg in new[] { "mount", "jbox:", drive, "--config", "NUL", "--network-mode", "--vfs-cache-mode", "full", "--vfs-write-back", "1s", "--vfs-cache-max-size", "1G", "--cache-dir", cacheDirectory, "--dir-cache-time", "30s", "--volname", "PanBridge", "--log-level", "ERROR", "--rc" }) start.ArgumentList.Add(arg);
        if (requestedAccess == DriveAccess.ReadOnly) start.ArgumentList.Add("--read-only");
        access = requestedAccess;
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        int controlPort = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        controlAddress = new Uri($"http://127.0.0.1:{controlPort}/");
        controlPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        start.Environment["RCLONE_RC_ADDR"] = $"127.0.0.1:{controlPort}";
        start.Environment["RCLONE_RC_USER"] = "jboxdrive";
        start.Environment["RCLONE_RC_PASS"] = controlPassword;
        start.Environment["RCLONE_CONFIG_JBOX_TYPE"] = "webdav";
        start.Environment["RCLONE_CONFIG_JBOX_URL"] = address.ToString();
        start.Environment["RCLONE_CONFIG_JBOX_VENDOR"] = "other";
        start.Environment["RCLONE_CONFIG_JBOX_USER"] = "local";
        start.Environment["RCLONE_CONFIG_JBOX_PASS"] = password;
        process = Process.Start(start) ?? throw new IOException("Could not start mount.");
        process.OutputDataReceived += (_, _) => { }; process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        Drive = drive;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (process.HasExited) { Disconnect(); throw new CloudAccessException("挂载失败，请检查 WinFsp 是否正确安装，并尝试其他盘符。"); }
            if (Directory.Exists(drive + "\\")) return;
            await Task.Delay(200);
        }
        Disconnect(); throw new CloudAccessException("等待挂载超时，请重试。");
    }
    private async Task<JsonDocument> ControlAsync(string endpoint)
    {
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(10) };
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(controlAddress!, endpoint));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("jboxdrive:" + controlPassword)));
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());
    }
    public async Task WaitForUploadsAsync()
    {
        if (process is null || process.HasExited) return;
        if (access != DriveAccess.ReadOnly && IsMounted)
        {
            int stable = 0;
            for (int attempt = 0; attempt < 60; attempt++)
            {
                try
                {
                    using var stats = await ControlAsync("vfs/stats");
                    var disk = stats.RootElement.GetProperty("diskCache");
                    if (disk.GetProperty("erroredFiles").GetInt64() > 0) throw new CloudAccessException("仍有文件上传失败，连接已保留。请检查网络并等待上传完成后再退出。");
                    bool idle = disk.GetProperty("uploadsInProgress").GetInt64() == 0 && disk.GetProperty("uploadsQueued").GetInt64() == 0;
                    stable = idle ? stable + 1 : 0;
                    if (stable >= 2) break;
                }
                catch (CloudAccessException) { throw; }
                catch { throw new CloudAccessException("暂时无法确认上传状态，连接已保留。请稍后再断开。"); }
                if (attempt == 59) throw new CloudAccessException("文件仍在上传，连接已保留。上传完成后再断开或退出。");
                await Task.Delay(1000);
            }
        }
    }
    public async Task DisconnectAsync()
    {
        if (process is null || process.HasExited) { Disconnect(); return; }
        await WaitForUploadsAsync();
        try { using var result = await ControlAsync("core/quit"); } catch (HttpRequestException) { }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { throw new CloudAccessException("挂载服务尚未退出，请稍后重试。"); }
        Disconnect();
    }
    public void Disconnect()
    {
        if (process is not null)
        {
            try { if (!process.HasExited) { process.Kill(true); process.WaitForExit(5000); } } catch { }
            process.Dispose(); process = null;
        }
        Drive = null;
        controlAddress = null; controlPassword = null;
    }
    public void Dispose() => Disconnect();
}
