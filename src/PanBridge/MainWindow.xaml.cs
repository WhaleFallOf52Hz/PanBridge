using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Forms = System.Windows.Forms;

namespace PanBridge;

// Desktop shell: window/tray lifetime and native dialogs. State and operations live in WorkspaceViewModel.
public partial class MainWindow : Window
{
    private readonly WorkspaceViewModel workspace;
    private readonly Forms.NotifyIcon tray;
    private readonly System.Drawing.Icon trayIcon;
    private readonly EventWaitHandle showEvent;
    private readonly RegisteredWaitHandle showRegistration;
    private readonly EventWaitHandle exitEvent;
    private readonly RegisteredWaitHandle exitRegistration;
    private Task? initialization;
    private bool quit, trayHintShown;
    public MainWindow()
    {
        InitializeComponent();
        Navigation.SelectionChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Source, Navigation) && Navigation.SelectedItem is TabItem { Content: ScrollViewer page } tab)
                Dispatcher.BeginInvoke(() => { tab.Focus(); page.ScrollToTop(); });
        };
        workspace = new WorkspaceViewModel(OfficialLoginAsync, ClearBrowserAsync, SaveReport, ChooseCacheFolder);
        DataContext = workspace;
        using (var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/PanBridge.ico")).Stream)
        using (var sourceIcon = new System.Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize)) trayIcon = (System.Drawing.Icon)sourceIcon.Clone();
        tray = new Forms.NotifyIcon { Icon = trayIcon, Text = "交云桥", Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示主窗口", null, (_, _) => Dispatcher.Invoke(ShowMain));
        menu.Items.Add("打开云盘", null, (_, _) => Dispatcher.Invoke(workspace.OpenDrive));
        menu.Items.Add("断开连接", null, (_, _) => Dispatcher.Invoke(async () => await workspace.DisconnectAsync()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出程序", null, (_, _) => Dispatcher.Invoke(async () => await ExitAsync()));
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowMain);
        workspace.Changed += () => tray.Text = workspace.IsMounted ? $"交云桥 · {workspace.DriveLabel} · {workspace.AccessLabel}" : "交云桥 · 未挂载";
        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\PanBridge.ShowWindow");
        showRegistration = ThreadPool.RegisterWaitForSingleObject(showEvent, (_, _) => Dispatcher.BeginInvoke(ShowMain), null, Timeout.Infinite, false);
        exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\PanBridge.RequestExit");
        exitRegistration = ThreadPool.RegisterWaitForSingleObject(exitEvent, (_, _) => Dispatcher.BeginInvoke(async () => await ExitAsync()), null, Timeout.Infinite, false);
        Loaded += async (_, _) => await InitializeOnceAsync();
        Activated += (_, _) => workspace.RefreshStartupStatus();
        Closing += OnClosing;
        Closed += (_, _) =>
        {
            showRegistration.Unregister(null); showEvent.Dispose(); exitRegistration.Unregister(null); exitEvent.Dispose(); workspace.Dispose(); tray.Dispose(); trayIcon.Dispose();
            AppPaths.WriteStatus("stopped"); Application.Current.Shutdown();
        };
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) Hide(); };
    }
    private Task InitializeOnceAsync(bool background = false) => initialization ??= workspace.InitializeAsync(background);
    public async Task StartInTrayAsync()
    {
        // Create the hidden HWND for native dialogs/activation, without ever showing a foreground window.
        new WindowInteropHelper(this).EnsureHandle();
        await InitializeOnceAsync(true);
    }
    private Task<AuthSession?> OfficialLoginAsync(TboxBackend backend, AuthSession? previous)
    {
        var dialog = new LoginWindow(backend.ValidateAsync, previous) { Owner = this };
        return Task.FromResult(dialog.ShowDialog() == true ? dialog.Session : null);
    }
    private async Task ClearBrowserAsync()
    {
        var environment = await CoreWebView2Environment.CreateAsync(null, AppPaths.Browser);
        var controller = await environment.CreateCoreWebView2ControllerAsync(new WindowInteropHelper(this).Handle);
        try
        {
            controller.IsVisible = false;
            await controller.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.Cookies | CoreWebView2BrowsingDataKinds.LocalStorage | CoreWebView2BrowsingDataKinds.IndexedDb);
        }
        finally { controller.Close(); }
    }
    private void SaveReport(string report)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = "导出诊断报告", FileName = $"PanBridge-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.txt", Filter = "文本报告 (*.txt)|*.txt", DefaultExt = ".txt" };
        if (dialog.ShowDialog(this) == true) File.WriteAllText(dialog.FileName, report, new System.Text.UTF8Encoding(true));
    }
    private string? ChooseCacheFolder(string current)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择交云桥缓存目录", Multiselect = false,
            InitialDirectory = Directory.Exists(current) ? current : AppPaths.Data
        };
        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }
    private void ShowMain() { Show(); WindowState = WindowState.Normal; Activate(); }
    private async void ExitClick(object sender, RoutedEventArgs e) => await ExitAsync();
    private async Task ExitAsync()
    {
        if (await workspace.PrepareExitAsync()) { quit = true; Close(); }
        else ShowMain();
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (quit) return;
        e.Cancel = true; Hide();
        if (!trayHintShown && workspace.ShowTrayHint)
        {
            trayHintShown = true;
            tray.ShowBalloonTip(3500, "交云桥在后台运行", "盘符连接保持运行。双击托盘图标打开窗口，右键可退出。", Forms.ToolTipIcon.Info);
        }
    }
}
