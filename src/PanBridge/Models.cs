using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PanBridge;

public record SavedCookie(string Name, string Value, string Domain, string Path, bool Secure, bool HttpOnly, bool Session, DateTime Expires, int SameSite);
public record AuthSession(string UserToken, DateTimeOffset SavedAt, List<SavedCookie> Cookies);
public enum DriveAccess { ReadOnly, NoDelete, Full }
public record DriveSettings(string DriveLetter = "J:", bool AutoMount = false, DriveAccess Access = DriveAccess.Full, bool OpenAfterMount = false, bool ShowTrayHint = true);

public static class AppPaths
{
    public static string Root => AppContext.BaseDirectory;
    public static string Data => Path.Combine(Root, "data");
    public static string Browser => Path.Combine(Data, "browser-profile");
    public static string Vault => Path.Combine(Data, "session.dpapi");
    public static string Runtime => Path.Combine(Root, "runtime");
    public static string Tbox => Path.Combine(Runtime, "tbox", "TboxWebdav.Server.AspNetCore.exe");
    public static string Rclone => Path.Combine(Runtime, "rclone", "rclone.exe");
    public static string Installer => Path.Combine(Runtime, "winfsp.msi");
    public static string Status => Path.Combine(Data, "status.json");
    public static void Ensure() => Directory.CreateDirectory(Data);
    public static DriveSettings LoadSettings()
    {
        try { return JsonSerializer.Deserialize<DriveSettings>(File.ReadAllText(Path.Combine(Data, "settings.json"))) ?? new(); }
        catch { return new(); }
    }
    public static void SaveSettings(DriveSettings settings) => File.WriteAllText(Path.Combine(Data, "settings.json"), JsonSerializer.Serialize(settings));
    public static void WriteStatus(string state, int? entries = null, string? drive = null, string? error = null) =>
        File.WriteAllText(Status, JsonSerializer.Serialize(new { State = state, Entries = entries, Drive = drive, Access = LoadSettings().Access.ToString(), Error = error, Time = DateTimeOffset.Now }, new JsonSerializerOptions { WriteIndented = true }));
}

public static partial class SessionRules
{
    [GeneratedRegex(@"\A[0-9a-fA-F]{128}\z")]
    private static partial Regex TokenPattern();
    public static bool ValidToken(string? token) => token is not null && TokenPattern().IsMatch(token);
    public static bool AllowedDomain(string domain) => domain.TrimStart('.').Equals("pan.sjtu.edu.cn", StringComparison.OrdinalIgnoreCase) || domain.TrimStart('.').Equals("jaccount.sjtu.edu.cn", StringComparison.OrdinalIgnoreCase);
    public static SavedCookie? SelectToken(IEnumerable<SavedCookie> cookies) => cookies.FirstOrDefault(c =>
        c.Domain.TrimStart('.').Equals("pan.sjtu.edu.cn", StringComparison.OrdinalIgnoreCase) &&
        (c.Name == "USER_TOKEN" || c.Name == "UserToken") && ValidToken(c.Value) && (c.Session || c.Expires.ToUniversalTime() > DateTime.UtcNow));
}

public static class SafeError
{
    public static string Describe(Exception ex) => ex switch
    {
        CloudAccessException => ex.Message,
        Microsoft.Web.WebView2.Core.WebView2RuntimeNotFoundException => "未检测到 WebView2 Runtime，请安装 Microsoft Edge WebView2 运行时。",
        System.Security.Cryptography.CryptographicException => "本地凭证无法解密，请重新登录。",
        HttpRequestException => "网络连接失败，请检查网络、代理和系统时间后重试。",
        TaskCanceledException => "请求超时，请检查网络后重试。",
        _ => "操作失败（" + ex.GetType().Name + "），请重试。"
    };
}
public class CloudAccessException(string message) : Exception(message);
