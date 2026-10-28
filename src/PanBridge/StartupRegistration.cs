using System.IO;
using Microsoft.Win32;

namespace PanBridge;

public enum StartupState { NotRegistered, Enabled, DisabledByWindows, DifferentPath }

public sealed class StartupRegistration
{
    public const string ValueName = "PanBridge";
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ApprovalKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private readonly string executable;
    private readonly string runKey;
    private readonly string approvalKey;
    public StartupRegistration(string? executable = null, string runKey = RunKey, string approvalKey = ApprovalKey)
    {
        this.executable = Path.GetFullPath(executable ?? Path.Combine(AppPaths.Root, "PanBridge.exe"));
        this.runKey = runKey; this.approvalKey = approvalKey;
    }
    public string Command => $"\"{executable}\" --startup";
    public StartupState ReadState()
    {
        using var run = Registry.CurrentUser.OpenSubKey(runKey);
        if (run?.GetValue(ValueName) is not string command) return StartupState.NotRegistered;
        if (!string.Equals(command, Command, StringComparison.OrdinalIgnoreCase)) return StartupState.DifferentPath;
        // Windows owns this key. Read its disabled state, never reset it when starting/upgrading the app.
        using var approved = Registry.CurrentUser.OpenSubKey(approvalKey);
        if (approved?.GetValue(ValueName) is byte[] { Length: >= 4 } bytes && BitConverter.ToUInt32(bytes, 0) is 3 or 7)
            return StartupState.DisabledByWindows;
        return StartupState.Enabled;
    }
    public void Enable()
    {
        if (Command.Length > 260) throw new CloudAccessException("程序路径太长，无法注册 Windows 启动项。请将程序安装到较短的路径。");
        using var run = Registry.CurrentUser.CreateSubKey(runKey, true);
        run.SetValue(ValueName, Command, RegistryValueKind.String);
    }
    public void Disable()
    {
        using var run = Registry.CurrentUser.OpenSubKey(runKey, true);
        run?.DeleteValue(ValueName, false);
    }
}
