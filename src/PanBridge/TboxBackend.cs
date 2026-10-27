using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Xml;

namespace PanBridge;

public sealed class TboxBackend : IDisposable
{
    private Process? process;
    private DriveAccess access = DriveAccess.ReadOnly;
    public Uri? Address { get; private set; }
    private readonly HttpClient client = new(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(45) };
    public async Task SetAccessAsync(DriveAccess requested)
    {
        if (requested != access) { StopProcess(); access = requested; }
        await EnsureStartedAsync();
    }
    public async Task EnsureStartedAsync()
    {
        if (process is { HasExited: false }) return;
        if (!File.Exists(AppPaths.Tbox)) throw new CloudAccessException("缺少 Tbox 组件，请完整解压客户端后运行。");
        using (var https = new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
        using (var response = await https.GetAsync("https://pan.sjtu.edu.cn/", HttpCompletionOption.ResponseHeadersRead)) { }
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        Address = new Uri($"http://127.0.0.1:{port}/");
        var start = new ProcessStartInfo(AppPaths.Tbox)
        {
            WorkingDirectory = Path.GetDirectoryName(AppPaths.Tbox)!, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var arg in new[] { "--host", "127.0.0.1", "--port", port.ToString(), "--auth", "UserToken", "--access", access.ToString() }) start.ArgumentList.Add(arg);
        start.Environment["Logging__LogLevel__Default"] = "Warning";
        start.Environment["Logging__LogLevel__Microsoft.AspNetCore"] = "Warning";
        process = Process.Start(start) ?? throw new IOException("Could not start Tbox.");
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        for (int attempt = 0; attempt < 60; attempt++)
        {
            if (process.HasExited) throw new CloudAccessException("Tbox 未能启动，请关闭其他实例后重试。");
            try
            {
                using var response = await client.GetAsync(Address);
                if (response.StatusCode == HttpStatusCode.Unauthorized) return;
            }
            catch (HttpRequestException) { }
            await Task.Delay(150);
        }
        throw new TimeoutException("Tbox startup timed out.");
    }
    public async Task<int> ValidateAsync(string token)
    {
        if (!SessionRules.ValidToken(token)) throw new CloudAccessException("云盘登录凭证格式无效，请重新登录。");
        await EnsureStartedAsync();
        using var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), Address);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("local:" + token)));
        request.Headers.Add("Depth", "1");
        request.Content = new StringContent("<?xml version=\"1.0\"?><d:propfind xmlns:d=\"DAV:\"><d:prop><d:displayname/><d:resourcetype/></d:prop></d:propfind>", Encoding.UTF8, "application/xml");
        using var response = await client.SendAsync(request);
        if ((int)response.StatusCode != 207)
        {
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized) throw new CloudAccessException("云盘凭证无效或已过期，请重新登录交大账号。");
            throw new CloudAccessException($"云盘目录读取失败（HTTP {(int)response.StatusCode}）。");
        }
        return ParseListing(await response.Content.ReadAsByteArrayAsync());
    }
    public static int ParseListing(byte[] body)
    {
        using var stream = new MemoryStream(body);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 32 * 1024 * 1024 });
        var document = new XmlDocument { XmlResolver = null };
        document.Load(reader);
        var ns = new XmlNamespaceManager(document.NameTable); ns.AddNamespace("d", "DAV:");
        var entries = document.SelectNodes("/d:multistatus/d:response", ns);
        if (entries is null || entries.Count == 0 || entries[0]!.SelectSingleNode("d:propstat[contains(d:status,'200')]/d:prop/d:resourcetype/d:collection", ns) is null)
            throw new CloudAccessException("云盘未返回可用的根目录。");
        return entries.Count - 1;
    }
    private void StopProcess()
    {
        if (process is not null) { try { if (!process.HasExited) { process.Kill(true); process.WaitForExit(3000); } } catch { } process.Dispose(); }
        process = null; Address = null;
    }
    public void Dispose() { StopProcess(); client.Dispose(); }
}
