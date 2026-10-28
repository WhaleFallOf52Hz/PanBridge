using System.IO;
using Microsoft.Win32;

namespace PanBridge;

public static class StartupRegistrationTest
{
    public static void Run()
    {
        // Isolated HKCU test keys: the test never changes the actual Windows startup configuration.
        string root = @"Software\PanBridge\Tests\" + Guid.NewGuid().ToString("N");
        var startup = new StartupRegistration(Path.Combine(AppPaths.Data, "path with spaces", "PanBridge.exe"), root + @"\Run", root + @"\Approved");
        try
        {
            if (startup.ReadState() != StartupState.NotRegistered) throw new Exception("Unexpected startup registration.");
            startup.Enable();
            if (startup.ReadState() != StartupState.Enabled || !startup.Command.StartsWith('"') || !startup.Command.EndsWith("\" --startup")) throw new Exception("Startup path quoting or registration failed.");
            using (var approval = Registry.CurrentUser.CreateSubKey(root + @"\Approved"))
                approval.SetValue(StartupRegistration.ValueName, new byte[] { 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);
            startup.Enable();
            if (startup.ReadState() != StartupState.DisabledByWindows) throw new Exception("App overwrote the Windows disabled state.");
            using (var approval = Registry.CurrentUser.CreateSubKey(root + @"\Approved"))
                approval.SetValue(StartupRegistration.ValueName, new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);
            if (startup.ReadState() != StartupState.Enabled) throw new Exception("Windows re-enable was not detected.");
            var other = new StartupRegistration(Path.Combine(AppPaths.Data, "another", "PanBridge.exe"), root + @"\Run", root + @"\Approved");
            if (other.ReadState() != StartupState.DifferentPath) throw new Exception("Stale executable path not detected.");
            startup.Disable();
            if (startup.ReadState() != StartupState.NotRegistered) throw new Exception("Startup entry was not removed.");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(root, false); }
    }
}
