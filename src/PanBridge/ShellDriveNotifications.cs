using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace PanBridge;

public static class ShellDriveNotifications
{
    private const int DriveRemoved = 0x80;
    private const int DriveAdded = 0x100;
    private const uint PathUnicodeAndFlush = 0x1005;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint QueryDosDevice(string name, StringBuilder target, int size);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(int change, uint flags, string path, IntPtr item2);

    private static string Normalize(string drive)
    {
        if (drive.Length != 2 || drive[1] != ':' || !char.IsAsciiLetter(drive[0])) throw new ArgumentException("Expected a drive letter.", nameof(drive));
        return drive.ToUpperInvariant();
    }

    public static bool DeviceExists(string drive)
    {
        drive = Normalize(drive);
        if (QueryDosDevice(drive, new StringBuilder(8192), 8192) != 0) return true;
        int error = Marshal.GetLastWin32Error();
        if (error == 2) return false;
        throw new Win32Exception(error);
    }

    public static void Added(string drive) => SHChangeNotify(DriveAdded, PathUnicodeAndFlush, Normalize(drive) + "\\", IntPtr.Zero);

    public static void RemovedIfDetached(string drive)
    {
        drive = Normalize(drive);
        // A display notification must never remove an icon for a live or reassigned drive.
        if (!DeviceExists(drive)) SHChangeNotify(DriveRemoved, PathUnicodeAndFlush, drive + "\\", IntPtr.Zero);
    }

    public static async Task WaitForRemovalAsync(string drive)
    {
        drive = Normalize(drive);
        for (int attempt = 0; attempt < 50; attempt++)
        {
            if (!DeviceExists(drive)) { RemovedIfDetached(drive); return; }
            await Task.Delay(100);
        }
        throw new CloudAccessException("挂载进程已停止，Windows 仍在移除盘符，请稍后重试断开。");
    }
}
