using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PanBridge;

// Opt-in live check: creates and removes only its own uniquely named cloud directory.
public static class IntegrationTest
{
    public static async Task<int> RunAsync()
    {
        AppPaths.Ensure();
        var checks = new List<string>();
        string stage = "load_session";
        string folder = "PanBridge-check-" + Guid.NewGuid().ToString("N")[..12];
        string? localFolder = null;
        using var backend = new TboxBackend();
        using var mount = new MountManager(Path.Combine(AppPaths.Data, "integration-cache-" + Guid.NewGuid().ToString("N")[..8]));
        bool cleanup = false;
        try
        {
            var session = SessionVault.Load() ?? throw new InvalidOperationException("No saved login.");
            stage = "start_full_access";
            await backend.SetAccessAsync(DriveAccess.Full);
            int entries = await backend.ValidateAsync(session.UserToken);
            checks.Add("saved_cookie_login");
            var drives = MountManager.AvailableDrives();
            string drive = drives.Contains("T:") ? "T:" : drives.First();
            stage = "mount";
            await mount.MountAsync(backend.Address!, session.UserToken, drive, DriveAccess.Full);
            checks.Add("mount");
            localFolder = Path.Combine(drive + "\\", folder);
            stage = "create_directory";
            Directory.CreateDirectory(localFolder); cleanup = true;
            checks.Add("create_directory");
            string first = Path.Combine(localFolder, "probe.txt");
            stage = "upload";
            await File.WriteAllTextAsync(first, "PanBridge upload check", new UTF8Encoding(false));
            await mount.WaitForUploadsAsync();
            using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("local:" + session.UserToken)));
            async Task<string> ReadCloud(string name) => await client.GetStringAsync(new Uri(backend.Address!, folder + "/" + name));
            if (await ReadCloud("probe.txt") != "PanBridge upload check") throw new IOException("Cloud upload did not match.");
            checks.Add("upload_verified_from_cloud");
            stage = "modify";
            await File.WriteAllTextAsync(first, "PanBridge modified check", new UTF8Encoding(false));
            await mount.WaitForUploadsAsync();
            if (await ReadCloud("probe.txt") != "PanBridge modified check") throw new IOException("Cloud modification did not match.");
            checks.Add("modify_verified_from_cloud");
            stage = "same_size_modify";
            await File.WriteAllTextAsync(first, "PanBridge replaced check", new UTF8Encoding(false));
            await mount.WaitForUploadsAsync();
            if (await ReadCloud("probe.txt") != "PanBridge replaced check") throw new IOException("Same-size modification returned stale content.");
            checks.Add("same_size_modify_verified_from_cloud");
            stage = "rename";
            string renamed = Path.Combine(localFolder, "renamed.txt");
            File.Move(first, renamed);
            await mount.WaitForUploadsAsync();
            if (await ReadCloud("renamed.txt") != "PanBridge replaced check") throw new IOException("Cloud rename did not match.");
            checks.Add("rename_verified_from_cloud");
            stage = "delete";
            File.Delete(renamed);
            Directory.Delete(localFolder);
            cleanup = false;
            using var deleted = await client.GetAsync(new Uri(backend.Address!, folder + "/renamed.txt"));
            if (deleted.StatusCode is not (HttpStatusCode.NotFound or HttpStatusCode.Gone)) throw new IOException("Deleted file still exists remotely.");
            checks.Add("delete_verified_from_cloud");
            stage = "disconnect";
            await mount.DisconnectAsync();
            checks.Add("graceful_disconnect");
            Write(new { Success = true, Checks = checks, InitialEntries = entries });
            return 0;
        }
        catch (Exception ex)
        {
            if (cleanup && localFolder is not null)
            {
                try
                {
                    await mount.WaitForUploadsAsync();
                    foreach (string name in new[] { "probe.txt", "renamed.txt" }) { string file = Path.Combine(localFolder, name); if (File.Exists(file)) File.Delete(file); }
                    if (Directory.Exists(localFolder)) Directory.Delete(localFolder); // Never recursive; never delete user data.
                    cleanup = false;
                }
                catch { }
            }
            try { await mount.DisconnectAsync(); } catch { }
            Write(new { Success = false, Stage = stage, Error = ex.GetType().Name, Checks = checks, TestDirectoryRetained = cleanup ? folder : null });
            return 1;
        }
    }
    private static void Write(object result) => File.WriteAllText(Path.Combine(AppPaths.Root, "integration-test.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
}
