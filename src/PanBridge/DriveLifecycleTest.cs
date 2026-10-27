using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Automation;

namespace PanBridge;

// A read-only regression for the Explorer ghost-drive problem. It never writes cloud files.
public static class DriveLifecycleTest
{
    private const string ComputerFolder = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint QueryDosDevice(string name, StringBuilder target, int size);

    public static async Task<int> RunAsync()
    {
        AppPaths.Ensure();
        string stage = "load_session";
        string? drive = null;
        bool? deviceGone = null;
        int? shellItems = null;
        using var backend = new TboxBackend();
        using var mount = new MountManager(Path.Combine(AppPaths.Data, "lifecycle-cache-" + Guid.NewGuid().ToString("N")[..8]), "PanBridge-Lifecycle");
        try
        {
            var session = SessionVault.Load() ?? throw new InvalidOperationException("No saved session.");
            await backend.SetAccessAsync(DriveAccess.ReadOnly);
            await backend.ValidateAsync(session.UserToken);
            var available = MountManager.AvailableDrives();
            drive = available.Contains("T:") ? "T:" : available.First();
            stage = "mount_read_only";
            await mount.MountAsync(backend.Address!, session.UserToken, drive, DriveAccess.ReadOnly);
            stage = "show_drive_in_explorer";
            var windows = ComputerWindows();
            if (windows.Count == 0) throw new InvalidOperationException("Open This PC before running the lifecycle regression.");
            foreach (dynamic window in windows) window.Refresh();
            await Task.Delay(700);
            int before = CountDriveItems(windows, drive);
            if (before == 0) throw new InvalidOperationException("Explorer did not show the mounted test drive.");
            stage = "disconnect";
            await mount.DisconnectAsync();
            await Task.Delay(700);
            deviceGone = QueryDosDevice(drive, new StringBuilder(8192), 8192) == 0 && Marshal.GetLastWin32Error() == 2;
            shellItems = CountDriveItems(windows, drive);
            if (!deviceGone.Value || shellItems != 0) throw new InvalidOperationException("Disconnected drive remains in Explorer.");
            Write(new { Success = true, Drive = drive, DeviceRemoved = deviceGone, ExplorerItemsAfterDisconnect = shellItems, Checks = new[] { "read_only_mount", "visible_in_existing_explorer", "device_removed", "explorer_icon_removed" } });
            return 0;
        }
        catch (Exception ex)
        {
            try { await mount.DisconnectAsync(); } catch { }
            Write(new { Success = false, Stage = stage, Drive = drive, Error = ex.GetType().Name, DeviceRemoved = deviceGone, ExplorerItemsAfterDisconnect = shellItems });
            return 1;
        }
    }
    private static List<object> ComputerWindows()
    {
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!;
        var windows = new List<object>();
        foreach (dynamic window in shell.Windows())
        {
            try { if ((string)window.Document.Folder.Self.Path == ComputerFolder) windows.Add(window); }
            catch { }
        }
        return windows;
    }
    private static int CountDriveItems(IEnumerable<object> windows, string drive)
    {
        int count = 0;
        foreach (dynamic window in windows)
        {
            var element = AutomationElement.FromHandle((IntPtr)(int)window.HWND);
            var nodes = element.FindAll(TreeScope.Descendants, Condition.TrueCondition);
            foreach (AutomationElement node in nodes)
            {
                string name = node.Current.Name;
                if (name.Contains("(" + drive + ")", StringComparison.OrdinalIgnoreCase) || name.Equals(drive + "\\", StringComparison.OrdinalIgnoreCase)) count++;
            }
        }
        return count;
    }
    private static void Write(object result) => File.WriteAllText(Path.Combine(AppPaths.Root, "drive-lifecycle-test.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
}
