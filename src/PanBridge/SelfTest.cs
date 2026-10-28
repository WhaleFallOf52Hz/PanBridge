using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PanBridge;

public static class SelfTest
{
    public static void Run()
    {
        string fake = new('a', 128);
        var cookie = new SavedCookie("USER_TOKEN", fake, "pan.sjtu.edu.cn", "/", true, true, false, DateTime.UtcNow.AddHours(1), 0);
        if (SessionRules.SelectToken([cookie])?.Value != fake) throw new Exception("Current cookie name not recognized.");
        if (SessionRules.SelectToken([cookie with { Name = "UserToken" }]) is null) throw new Exception("Legacy cookie not recognized.");
        if (SessionRules.SelectToken([cookie with { Expires = DateTime.UtcNow.AddDays(-1) }]) is not null) throw new Exception("Expired cookie accepted.");
        if (SessionRules.SelectToken([cookie with { Domain = "example.com" }]) is not null) throw new Exception("Untrusted cookie accepted.");
        string path = Path.Combine(AppContext.BaseDirectory, "self-test-session.dpapi");
        try
        {
            SessionVault.Save(new AuthSession(fake, DateTimeOffset.UtcNow, [cookie]), path);
            if (Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains(fake)) throw new Exception("Session was stored as plaintext.");
            if (SessionVault.Load(path)?.UserToken != fake) throw new Exception("DPAPI roundtrip failed.");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
        const string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:multistatus xmlns:D=\"DAV:\"><D:response><D:propstat><D:prop><D:resourcetype><D:collection/></D:resourcetype></D:prop><D:status>HTTP/1.1 200 OK</D:status></D:propstat></D:response><D:response><D:href>/example</D:href></D:response></D:multistatus>";
        if (TboxBackend.ParseListing(Encoding.UTF8.GetBytes(xml)) != 1) throw new Exception("Directory response parsing failed.");
        bool rejected = false;
        try { TboxBackend.ParseListing(Encoding.UTF8.GetBytes("<html/>")); } catch (CloudAccessException) { rejected = true; }
        if (!rejected) throw new Exception("Invalid listing accepted.");
        if (JsonSerializer.Deserialize<DriveSettings>("{\"DriveLetter\":\"J:\",\"AutoMount\":false}")?.Access != DriveAccess.Full) throw new Exception("Upgrade default access is incorrect.");
        var upgraded = JsonSerializer.Deserialize<DriveSettings>("{\"DriveLetter\":\"J:\",\"AutoMount\":true,\"Access\":2}")!;
        if (!upgraded.AutoMount || upgraded.Access != DriveAccess.Full || upgraded.OpenAfterMount || !upgraded.ShowTrayHint) throw new Exception("Existing preferences were not preserved on upgrade.");
        if (WorkspaceViewModel.Summarize([new("network", CheckResult.Failed, "offline"), new("mount", CheckResult.Skipped, "not connected")]).Contains("全部通过")) throw new Exception("Failed diagnostics reported success.");
        if (WorkspaceViewModel.Summarize([new("login", CheckResult.Skipped, "not logged in")]).Contains("全部通过")) throw new Exception("Skipped diagnostics reported success.");
        if (new TransferStatus(2, 1, 1).Summary.Contains("队列为空")) throw new Exception("Pending failed uploads were reported idle.");
        CacheStorageTest.Run();
        StartupRegistrationTest.Run();
        using (var viewModel = new WorkspaceViewModel((_, _) => Task.FromResult<AuthSession?>(null), () => Task.CompletedTask, _ => { }))
        {
            if (viewModel.MountCommand.CanExecute(null) || viewModel.TestCommand.CanExecute(null) || viewModel.OpenCommand.CanExecute(null) || viewModel.ExportCommand.CanExecute(null)) throw new Exception("Unauthenticated UI commands are enabled.");
            if (!viewModel.PrimaryCommand.CanExecute(null) || !viewModel.DiagnoseCommand.CanExecute(null)) throw new Exception("First-run recovery actions are unavailable.");
            viewModel.SelectedAccess = viewModel.SelectedAccess == DriveAccess.Full ? DriveAccess.ReadOnly : DriveAccess.Full;
            if (!viewModel.HasPendingChanges || !viewModel.ApplyCommand.CanExecute(null)) throw new Exception("Permission edits were not staged for explicit apply.");
            string alternateCacheLimit = viewModel.CacheLimitGiBText == "0.5" ? "2" : "0.5";
            viewModel.CacheLimitGiBText = "0";
            if (!viewModel.HasCacheError || viewModel.ApplyCacheCommand.CanExecute(null)) throw new Exception("Invalid cache limit can be applied.");
            viewModel.CacheLimitGiBText = alternateCacheLimit;
            if (viewModel.HasCacheError || !viewModel.CacheHasChanges || !viewModel.ApplyCacheCommand.CanExecute(null)) throw new Exception("Cache limit edits cannot be applied.");
        }
        using (var png = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/PanBridge.png")).Stream)
        {
            var frame = BitmapDecoder.Create(png, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            if (frame.PixelWidth != 1024 || frame.PixelHeight != 1024) throw new Exception("Application icon has unexpected dimensions.");
        }
        using (var ico = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/PanBridge.ico")).Stream)
        using (var icon = new System.Drawing.Icon(ico, new System.Drawing.Size(16, 16)))
            if (icon.Width != 16 || icon.Height != 16) throw new Exception("Small tray icon could not be decoded.");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test.json"), JsonSerializer.Serialize(new { Success = true, Checks = new[] { "cookie_names", "cookie_expiry", "cookie_domain", "dpapi_roundtrip", "no_plaintext_token", "webdav_xml", "invalid_xml_rejected", "settings_upgrade", "diagnostic_severity", "upload_status", "command_availability", "explicit_permission_apply", "cache_legacy_defaults", "cache_limit_validation", "cache_path_validation", "cache_pending_upload_guard", "cache_metadata_failure_guard", "cache_write_probe", "cache_command_validation", "startup_registration", "windows_disable_respected", "startup_path_quoting", "app_icon_resources", "small_tray_icon" } }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
