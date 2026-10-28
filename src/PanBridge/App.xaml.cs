using System.IO;
using System.Text.Json;
using System.Windows;

namespace PanBridge;

public partial class App : Application
{
    private Mutex? instance;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--quit"))
        {
            try { using var request = EventWaitHandle.OpenExisting(@"Local\PanBridge.RequestExit"); request.Set(); }
            catch (WaitHandleCannotBeOpenedException) { }
            Shutdown(); return;
        }
        if (e.Args.Contains("--drive-lifecycle-test"))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Shutdown(await DriveLifecycleTest.RunAsync());
            return;
        }
        if (e.Args.Contains("--integration-test"))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Shutdown(await IntegrationTest.RunAsync());
            return;
        }
        if (e.Args.Contains("--self-test"))
        {
            try { SelfTest.Run(); Shutdown(0); }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test.json"), JsonSerializer.Serialize(new { Success = false, Error = ex.GetType().Name, ex.Message }));
                Shutdown(1);
            }
            return;
        }
        if (e.Args.Contains("--startup") && new StartupRegistration().ReadState() != StartupState.Enabled) { Shutdown(); return; }
        instance = new Mutex(true, @"Local\PanBridge.Desktop", out bool created);
        if (!created)
        {
            if (e.Args.Contains("--startup") || e.Args.Contains("--background")) { Shutdown(); return; }
            try { using var activate = EventWaitHandle.OpenExisting(@"Local\PanBridge.ShowWindow"); activate.Set(); }
            catch (WaitHandleCannotBeOpenedException) { MessageBox.Show("交大云盘客户端正在启动，请稍后重试。", "交大云盘"); }
            Shutdown(); return;
        }
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show("操作未完成，请重试。" + Environment.NewLine + SafeError.Describe(args.Exception), "交大云盘");
            args.Handled = true;
        };
        var window = new MainWindow();
        MainWindow = window;
        if (e.Args.Contains("--startup") || e.Args.Contains("--background")) await window.StartInTrayAsync();
        else window.Show();
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}
