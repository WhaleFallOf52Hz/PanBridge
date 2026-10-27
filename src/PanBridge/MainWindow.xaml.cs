using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Forms = System.Windows.Forms;

namespace PanBridge;

public partial class MainWindow : Window
{
    private readonly TboxBackend backend = new();
    private readonly MountManager mount = new();
    private AuthSession? session;
    private bool busy;
    private bool loaded;
    private int entries;
    private readonly Forms.NotifyIcon tray;
    private bool quit;
    private bool trayHintShown;
    private readonly EventWaitHandle showEvent;
    private readonly RegisteredWaitHandle showRegistration;
    private DriveAccess SelectedAccess => Enum.TryParse<DriveAccess>((AccessPicker.SelectedItem as ComboBoxItem)?.Tag as string, out var access) ? access : DriveAccess.Full;
    private string AccessLabel => SelectedAccess switch { DriveAccess.ReadOnly => "只读", DriveAccess.NoDelete => "读写，禁止删除", _ => "完整读写" };
    public MainWindow()
    {
        InitializeComponent(); AppPaths.Ensure();
        RefreshDrives();
        var settings = AppPaths.LoadSettings();
        AccessPicker.SelectedItem = AccessPicker.Items.Cast<ComboBoxItem>().First(item => (string)item.Tag == settings.Access.ToString());
        ModeBadge.Text = AccessLabel;
        if (DrivePicker.Items.Contains(settings.DriveLetter)) DrivePicker.SelectedItem = settings.DriveLetter;
        AutoMountCheck.IsChecked = settings.AutoMount;
        tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Text = "交云桥", Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示主窗口", null, (_, _) => Dispatcher.Invoke(ShowMain));
        menu.Items.Add("打开云盘", null, (_, _) => Dispatcher.Invoke(() => OpenClick(this, new RoutedEventArgs())));
        menu.Items.Add("断开连接", null, (_, _) => Dispatcher.Invoke(async () => await DisconnectAsync()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出程序", null, (_, _) => Dispatcher.Invoke(async () => await ExitAsync()));
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowMain);
        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\PanBridge.ShowWindow");
        showRegistration = ThreadPool.RegisterWaitForSingleObject(showEvent, (_, _) => Dispatcher.BeginInvoke(ShowMain), null, Timeout.Infinite, false);
        DrivePicker.SelectionChanged += (_, _) => SaveSettings();
        Loaded += async (_, _) => { loaded = true; await RestoreAsync(); };
        Closing += OnClosing;
        Closed += (_, _) => { showRegistration.Unregister(null); showEvent.Dispose(); mount.Dispose(); backend.Dispose(); tray.Dispose(); AppPaths.WriteStatus("stopped"); Application.Current.Shutdown(); };
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) Hide(); };
    }
    private void RefreshDrives()
    {
        var previous = DrivePicker.SelectedItem as string;
        var available = MountManager.AvailableDrives();
        if (mount.Drive is not null && mount.IsMounted) { available.Add(mount.Drive); available.Sort(); }
        DrivePicker.ItemsSource = available;
        DrivePicker.SelectedItem = previous is not null && DrivePicker.Items.Contains(previous) ? previous : DrivePicker.Items.Contains("J:") ? "J:" : DrivePicker.Items.Cast<string>().FirstOrDefault();
    }
    private void Dependencies()
    {
        bool installed = MountManager.DriverInstalled;
        InstallButton.Visibility = installed ? Visibility.Collapsed : Visibility.Visible;
        DependencyText.Text = installed ? "关闭窗口收起到托盘，连接保持运行；右键托盘图标可退出。" : "首次挂载需要安装 WinFsp，安装时 Windows 会请求管理员权限。";
    }
    private void SetBusy(bool value)
    {
        busy = value;
        LoginButton.IsEnabled = LogoutButton.IsEnabled = InstallButton.IsEnabled = !value;
        DrivePicker.IsEnabled = !value && !mount.IsMounted;
        AccessPicker.IsEnabled = !value;
        MountButton.IsEnabled = !value && session is not null && !mount.IsMounted;
        DisconnectButton.IsEnabled = OpenButton.IsEnabled = !value && mount.IsMounted;
    }
    private async Task RestoreAsync()
    {
        SetBusy(true); Dependencies();
        bool needLogin = false;
        try
        {
            await backend.SetAccessAsync(SelectedAccess);
            session = SessionVault.Load();
            if (session is null) { needLogin = true; StateText.Text = "等待登录"; DetailText.Text = "在交大官方页面登录后，客户端将自动保存凭证。"; }
            else
            {
                entries = await backend.ValidateAsync(session.UserToken);
                ShowConnected("已恢复登录");
            }
        }
        catch (Exception ex) { StateText.Text = "需要重新登录"; DetailText.Text = SafeError.Describe(ex); session = null; needLogin = ex is CloudAccessException; AppPaths.WriteStatus("restore_failed", error: ex.GetType().Name); }
        finally { SetBusy(false); }
        if (needLogin) await LoginAsync();
        else if (session is not null && AutoMountCheck.IsChecked == true && MountManager.DriverInstalled) await MountAsync();
    }
    private void ShowConnected(string state)
    {
        StateText.Text = state;
        DetailText.Text = $"根目录已读取，共 {entries} 个项目。选择盘符后点击“连接盘符”。";
        LoginButton.Content = "重新登录";
        AppPaths.WriteStatus("authenticated", entries);
    }
    private async Task LoginAsync()
    {
        if (busy) return;
        SetBusy(true);
        try
        {
            if (mount.IsMounted) await mount.DisconnectAsync();
            await backend.SetAccessAsync(SelectedAccess);
            AppPaths.WriteStatus("awaiting_login");
            var login = new LoginWindow(backend.ValidateAsync, session) { Owner = this };
            if (login.ShowDialog() == true && login.Session is not null)
            {
                session = login.Session; entries = login.Entries;
                SessionVault.Save(session);
                ShowConnected("登录成功");
            }
        }
        catch (Exception ex) { StateText.Text = "登录未完成"; DetailText.Text = SafeError.Describe(ex); AppPaths.WriteStatus("login_failed", error: ex.GetType().Name); }
        finally { SetBusy(false); Dependencies(); }
        if (session is not null && AutoMountCheck.IsChecked == true && MountManager.DriverInstalled) await MountAsync();
    }
    private async Task MountAsync()
    {
        if (busy || session is null || mount.IsMounted) return;
        SetBusy(true);
        try
        {
            if (DrivePicker.SelectedItem is not string drive) throw new CloudAccessException("没有可用的盘符。");
            StateText.Text = "正在连接盘符…";
            entries = await backend.ValidateAsync(session.UserToken);
            await mount.MountAsync(backend.Address!, session.UserToken, drive, SelectedAccess);
            ShowMounted(drive);
        }
        catch (Exception ex) { StateText.Text = "盘符未连接"; DetailText.Text = SafeError.Describe(ex); AppPaths.WriteStatus("mount_failed", error: ex.GetType().Name); }
        finally { SetBusy(false); Dependencies(); }
    }
    private async void LoginClick(object sender, RoutedEventArgs e) => await LoginAsync();
    private async void MountClick(object sender, RoutedEventArgs e) => await MountAsync();
    private async void DisconnectClick(object sender, RoutedEventArgs e) => await DisconnectAsync();
    private async Task DisconnectAsync()
    {
        if (busy || !mount.IsMounted) return;
        SetBusy(true);
        try { StateText.Text = "正在断开…"; DetailText.Text = "等待文件上传完成后断开连接。"; await mount.DisconnectAsync(); RefreshDrives(); ShowConnected("已断开盘符"); tray.Text = "交云桥 · 未挂载"; }
        catch (Exception ex) { StateText.Text = "连接已保留"; DetailText.Text = SafeError.Describe(ex); ShowMain(); }
        finally { SetBusy(false); }
    }
    private void OpenClick(object sender, RoutedEventArgs e) { if (mount.IsMounted) Process.Start(new ProcessStartInfo("explorer.exe", mount.Drive + "\\") { UseShellExecute = true }); }
    private async void InstallClick(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        SetBusy(true);
        try
        {
            bool reboot = await MountManager.InstallDriverAsync();
            DetailText.Text = reboot ? "挂载组件已安装，需要重启 Windows 后再连接盘符。" : "挂载组件已安装，可以连接盘符。";
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { DetailText.Text = "安装已取消，登录状态已保留。"; }
        catch (Exception ex) { DetailText.Text = SafeError.Describe(ex); }
        finally { Dependencies(); SetBusy(false); }
    }
    private async void LogoutClick(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        SetBusy(true);
        try
        {
            await mount.DisconnectAsync(); SessionVault.Clear(); session = null;
            var environment = await CoreWebView2Environment.CreateAsync(null, AppPaths.Browser);
            var controller = await environment.CreateCoreWebView2ControllerAsync(new WindowInteropHelper(this).Handle);
            try { controller.IsVisible = false; await controller.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.Cookies | CoreWebView2BrowsingDataKinds.LocalStorage | CoreWebView2BrowsingDataKinds.IndexedDb); }
            finally { controller.Close(); }
            StateText.Text = "已清除登录"; DetailText.Text = "本地加密凭证和应用内浏览器的登录数据已清除。"; LoginButton.Content = "登录交大云盘";
            AppPaths.WriteStatus("logged_out");
        }
        catch (Exception ex) { DetailText.Text = SafeError.Describe(ex); }
        finally { RefreshDrives(); SetBusy(false); }
    }
    private void SaveSettings() { if (loaded && DrivePicker.SelectedItem is string drive) AppPaths.SaveSettings(new(drive, AutoMountCheck.IsChecked == true, SelectedAccess)); }
    private void SettingsChanged(object sender, RoutedEventArgs e) => SaveSettings();
    private void ShowMounted(string drive)
    {
        StateText.Text = $"已连接到 {drive}";
        ModeBadge.Text = AccessLabel;
        DetailText.Text = SelectedAccess == DriveAccess.Full ? "支持上传、修改、重命名和删除。关闭窗口后继续在托盘运行。" : $"当前权限：{AccessLabel}。关闭窗口后继续在托盘运行。";
        tray.Text = $"交云桥 · {drive} · {AccessLabel}";
        SaveSettings(); AppPaths.WriteStatus("mounted", entries, drive);
    }
    private async void AccessChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!loaded || busy) return;
        bool reconnect = mount.IsMounted;
        string? drive = mount.Drive;
        SetBusy(true);
        try
        {
            StateText.Text = "正在切换权限…";
            await mount.DisconnectAsync();
            await backend.SetAccessAsync(SelectedAccess);
            ModeBadge.Text = AccessLabel; SaveSettings();
            if (session is not null)
            {
                entries = await backend.ValidateAsync(session.UserToken);
                if (reconnect && drive is not null) { await mount.MountAsync(backend.Address!, session.UserToken, drive, SelectedAccess); ShowMounted(drive); }
                else ShowConnected("权限已更新");
            }
            else { StateText.Text = "等待登录"; DetailText.Text = "请先登录交大云盘。"; }
        }
        catch (Exception ex) { StateText.Text = "权限切换未完成"; DetailText.Text = SafeError.Describe(ex); }
        finally { RefreshDrives(); SetBusy(false); }
    }
    private void ShowMain() { Show(); WindowState = WindowState.Normal; Activate(); }
    private async Task ExitAsync()
    {
        if (busy) { ShowMain(); return; }
        SetBusy(true);
        try { DetailText.Text = "正在等待文件上传完成并退出…"; await mount.DisconnectAsync(); quit = true; Close(); }
        catch (Exception ex) { DetailText.Text = SafeError.Describe(ex); ShowMain(); SetBusy(false); }
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (quit) return;
        e.Cancel = true; Hide();
        if (!trayHintShown) { trayHintShown = true; tray.ShowBalloonTip(3500, "交云桥在后台运行", "盘符连接保持运行。双击托盘图标打开窗口，右键可退出。", Forms.ToolTipIcon.Info); }
    }
}
