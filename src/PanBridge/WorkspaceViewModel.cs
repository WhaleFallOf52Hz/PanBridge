using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;

namespace PanBridge;

public record AccessOption(DriveAccess Value, string Label);
public sealed class UiCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => execute();
    public event EventHandler? CanExecuteChanged;
    public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

// Presentation state and connection operations; the shell supplies native dialogs only.
public sealed class WorkspaceViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly TboxBackend backend = new();
    private readonly MountManager mount = new();
    private readonly Func<TboxBackend, AuthSession?, Task<AuthSession?>> login;
    private readonly Func<Task> clearBrowser;
    private readonly Action<string> saveReport;
    private readonly Func<string, string?>? chooseCacheFolder;
    private readonly List<UiCommand> commands = [];
    private AuthSession? session;
    private DriveSettings settings;
    private string? selectedDrive;
    private DriveAccess selectedAccess;
    private string cacheDirectoryText, cacheLimitGiBText;
    private bool busy, cloudVerified, hasError;
    private int entries;
    private string message = "正在恢复本地登录…", operation = "正在准备", transfer = "尚未检测";
    private DateTimeOffset? checkedAt, diagnosticsAt;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Changed;
    public ObservableCollection<string> AvailableDrives { get; } = [];
    public ObservableCollection<DiagnosticCheck> Checks { get; } = [];
    public IReadOnlyList<AccessOption> AccessModes { get; } = [new(DriveAccess.ReadOnly, "只读"), new(DriveAccess.NoDelete, "读写，禁止删除"), new(DriveAccess.Full, "完整读写，允许删除")];
    public UiCommand PrimaryCommand { get; }
    public UiCommand LoginCommand { get; }
    public UiCommand MountCommand { get; }
    public UiCommand DisconnectCommand { get; }
    public UiCommand OpenCommand { get; }
    public UiCommand ApplyCommand { get; }
    public UiCommand TestCommand { get; }
    public UiCommand DiagnoseCommand { get; }
    public UiCommand ExportCommand { get; }
    public UiCommand InstallCommand { get; }
    public UiCommand LogoutCommand { get; }
    public UiCommand DataCommand { get; }
    public UiCommand LicensesCommand { get; }
    public UiCommand RefreshDrivesCommand { get; }
    public UiCommand BrowseCacheCommand { get; }
    public UiCommand ApplyCacheCommand { get; }
    public UiCommand DefaultCacheCommand { get; }
    public UiCommand OpenCacheCommand { get; }
    public WorkspaceViewModel(Func<TboxBackend, AuthSession?, Task<AuthSession?>> login, Func<Task> clearBrowser, Action<string> saveReport, Func<string, string?>? chooseCacheFolder = null)
    {
        this.login = login; this.clearBrowser = clearBrowser; this.saveReport = saveReport;
        this.chooseCacheFolder = chooseCacheFolder;
        AppPaths.Ensure(); settings = AppPaths.LoadSettings();
        if (!Enum.IsDefined(settings.Access)) settings = settings with { Access = DriveAccess.Full };
        cacheDirectoryText = string.IsNullOrWhiteSpace(settings.CacheDirectory) ? AppPaths.DefaultCache : settings.CacheDirectory;
        cacheLimitGiBText = CacheStorage.FormatGiB(settings.CacheMaxBytes);
        mount.ConfigureCache(new(cacheDirectoryText, settings.CacheMaxBytes));
        selectedAccess = settings.Access; RefreshDrives();
        UiCommand Command(Action action, Func<bool>? can = null) { var command = new UiCommand(action, can); commands.Add(command); return command; }
        PrimaryCommand = Command(async () => { if (IsMounted) OpenDrive(); else if (session is null) await LoginAsync(); else await MountAsync(); }, () => !busy);
        LoginCommand = Command(async () => await LoginAsync(), () => !busy);
        MountCommand = Command(async () => await MountAsync(), () => !busy && session is not null && !IsMounted && SelectedDrive is not null);
        DisconnectCommand = Command(async () => await DisconnectAsync(), () => !busy && IsMounted);
        OpenCommand = Command(OpenDrive, () => !busy && IsMounted);
        ApplyCommand = Command(async () => await ApplyAsync(), () => !busy && HasPendingChanges && SelectedDrive is not null);
        TestCommand = Command(async () => await TestAsync(), () => !busy && session is not null);
        DiagnoseCommand = Command(async () => await DiagnoseAsync(), () => !busy);
        ExportCommand = Command(() => { try { saveReport(Diagnostics.BuildReport(Checks, diagnosticsAt ?? DateTimeOffset.Now)); } catch (Exception ex) { ShowError(ex); } }, () => !busy && diagnosticsAt is not null);
        InstallCommand = Command(async () => await RunAsync("正在安装挂载组件", async () =>
        {
            try { bool reboot = await MountManager.InstallDriverAsync(); Message = reboot ? "组件已安装，请重启 Windows 后连接盘符。" : "组件已安装，可以连接盘符。"; }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { Message = "安装已取消，登录状态已保留。"; }
        }), () => !busy);
        LogoutCommand = Command(async () => await RunAsync("正在清除登录", async () =>
        {
            await mount.DisconnectAsync(); SessionVault.Clear(); session = null; cloudVerified = false; entries = 0; checkedAt = null; transfer = "盘符未连接";
            WriteState("logged_out"); await clearBrowser(); Message = "已清除本地加密凭证和应用内浏览器登录数据。";
        }), () => !busy && (session is not null || File.Exists(AppPaths.Vault) || Directory.Exists(AppPaths.Browser)));
        DataCommand = Command(() => OpenPath(AppPaths.Data), () => !busy);
        LicensesCommand = Command(() => OpenPath(Path.Combine(AppPaths.Root, "THIRD-PARTY-NOTICES.txt")), () => !busy);
        RefreshDrivesCommand = Command(() => { RefreshDrives(); Notify(); }, () => !busy);
        BrowseCacheCommand = Command(() =>
        {
            try { string? directory = this.chooseCacheFolder?.Invoke(CacheDirectoryText); if (directory is not null) CacheDirectoryText = directory; }
            catch (Exception ex) { ShowError(ex); }
        }, () => !busy && this.chooseCacheFolder is not null);
        ApplyCacheCommand = Command(async () => await ApplyCacheAsync(), () => !busy && CacheHasChanges && CacheValidation.Length == 0);
        DefaultCacheCommand = Command(() => { cacheDirectoryText = AppPaths.DefaultCache; cacheLimitGiBText = "1"; Notify(); }, () => !busy);
        OpenCacheCommand = Command(async () => await RunAsync("正在打开缓存目录", async () =>
        {
            var current = CacheStorage.FromSettings(settings);
            await Task.Run(() => CacheStorage.PrepareDirectory(current)); OpenPath(current.Directory);
        }), () => !busy);
    }
    public bool IsBusy => busy;
    public bool IsIdle => !busy;
    public bool IsMounted => mount.IsMounted;
    public bool HasSession => session is not null;
    public bool HasError => hasError;
    public string Message { get => message; private set { message = value; Notify(); } }
    public string StatusTitle => busy ? operation : IsMounted ? $"云盘已连接到 {mount.Drive}" : cloudVerified ? "云盘已就绪" : session is not null ? "登录状态待检测" : "连接你的交大云盘";
    public string StatusDescription => IsMounted ? "在资源管理器中访问文件。关闭窗口后，连接继续在托盘运行。" : cloudVerified ? "登录验证已通过，连接盘符后即可在资源管理器中使用。" : "通过交大官方页面登录，客户端会自动保存本地加密凭证。";
    public string PrimaryLabel => IsMounted ? "打开云盘" : session is null ? "登录交大云盘" : "连接盘符";
    public string LoginLabel => session is null ? "登录交大云盘" : "重新登录";
    public string Badge => busy ? "处理中" : IsMounted ? "已连接" : cloudVerified ? "待挂载" : "未连接";
    public string BadgeColor => IsMounted ? "#18765E" : "#5C6F8B";
    public string DriveLabel => mount.Drive ?? "未连接";
    public string EntryLabel => cloudVerified ? $"{entries} 个项目" : "尚未读取";
    public string AccessLabel => Label(settings.Access);
    public string CloudLabel => cloudVerified ? "云盘可访问" : checkedAt is not null ? "检测未通过" : session is not null ? "等待检测" : "尚未登录";
    public string CloudDescription => checkedAt is not null ? $"最近检测 {checkedAt:HH:mm:ss} · {EntryLabel}" : "pan.sjtu.edu.cn · 上海交通大学云盘";
    public string AuthLabel => session is null ? "尚未保存登录" : "已保存加密凭证";
    public string AuthDescription => session is null ? "点击登录，在官方页面完成验证。" : $"保存于 {session.SavedAt:yyyy-MM-dd HH:mm}，仅当前 Windows 用户可解密。";
    public string ExpiryLabel
    {
        get
        {
            var token = session?.Cookies.FirstOrDefault(c => c.Name is "USER_TOKEN" or "UserToken");
            return token is null ? "登录后可查看凭证有效期" : token.Session ? "会话凭证，有效期由学校服务控制" : $"浏览器凭证到期：{token.Expires.ToLocalTime():yyyy-MM-dd HH:mm}；服务端可能提前失效";
        }
    }
    public string ServiceLabel => backend.IsRunning ? "本地服务运行中" : "本地服务未运行";
    public string ServiceDescription => backend.IsRunning && backend.Address is not null ? $"仅监听 127.0.0.1:{backend.Address.Port} · {AccessLabel}" : "登录或连接时自动启动桥接服务。";
    public string TransferLabel => transfer;
    public string DriverLabel => MountManager.DriverInstalled ? "WinFsp 已安装" : "WinFsp 未安装";
    public string DriverDescription => MountManager.DriverInstalled ? "挂载组件已就绪，可将云盘连接为 Windows 盘符。" : "首次连接需要安装组件，Windows 会请求管理员权限。";
    public string DiagnosticSummary => busy && operation == "正在运行诊断" ? $"已完成 {Checks.Count} 项检查…" : diagnosticsAt is null ? "尚未运行诊断" : Summarize(Checks);
    public string DiagnosticTime => diagnosticsAt is null ? "检测不会修改云盘文件，也不会自动断开连接。" : $"最近检测 {diagnosticsAt:yyyy-MM-dd HH:mm:ss} · 结果可导出为文本报告";
    public bool HasChecks => Checks.Count > 0;
    public bool HasPendingChanges => SelectedDrive is not null && (SelectedDrive != settings.DriveLetter || SelectedAccess != settings.Access);
    public string ApplyHint => HasPendingChanges ? IsMounted ? "有未应用的更改。应用时会等待上传结束，再重新连接。" : "有未应用的更改。保存后将在下次连接时使用。" : "挂载设置已保存。";
    public string DraftAccessDescription => SelectedAccess switch { DriveAccess.ReadOnly => "仅浏览和下载，不允许修改云盘。", DriveAccess.NoDelete => "可上传和修改，禁止删除文件及文件夹。", _ => "可上传、修改、重命名及删除云盘文件。" };
    public string? SelectedDrive { get => selectedDrive; set { selectedDrive = value; Notify(); } }
    public DriveAccess SelectedAccess { get => selectedAccess; set { selectedAccess = value; Notify(); } }
    public bool AutoMount { get => settings.AutoMount; set => SavePreference(settings with { AutoMount = value }); }
    public bool OpenAfterMount { get => settings.OpenAfterMount; set => SavePreference(settings with { OpenAfterMount = value }); }
    public bool ShowTrayHint { get => settings.ShowTrayHint; set => SavePreference(settings with { ShowTrayHint = value }); }
    public string VersionLabel => "版本 " + ProductInfo.Version + " · Windows x64";
    public string RuntimeLabel => ProductInfo.Runtime;
    public string WebViewLabel => ProductInfo.WebViewVersion;
    public string TboxVersion => ProductInfo.ComponentVersion(AppPaths.Tbox);
    public string RcloneVersion => ProductInfo.ComponentVersion(AppPaths.Rclone);
    public string DataPath => AppPaths.Data;
    public string CacheDirectoryText { get => cacheDirectoryText; set { if (cacheDirectoryText != value) { cacheDirectoryText = value; Notify(); } } }
    public string CacheLimitGiBText { get => cacheLimitGiBText; set { if (cacheLimitGiBText != value) { cacheLimitGiBText = value; Notify(); } } }
    public string CacheValidation
    {
        get { try { CacheStorage.Parse(CacheDirectoryText, CacheLimitGiBText); return ""; } catch (CloudAccessException ex) { return ex.Message; } }
    }
    public bool CacheHasChanges
    {
        get
        {
            try
            {
                var draft = CacheStorage.Parse(CacheDirectoryText, CacheLimitGiBText);
                var current = CacheStorage.FromSettings(settings);
                return !CacheStorage.SamePath(draft.Directory, current.Directory) || draft.MaxBytes != current.MaxBytes;
            }
            catch { return true; }
        }
    }
    public bool HasCacheError => CacheValidation.Length > 0;
    public string CacheSummary => $"当前缓存上限 {CacheStorage.FormatGiB(settings.CacheMaxBytes)} GiB";
    public string CacheApplyHint => CacheHasChanges ? "有未应用的缓存更改。已连接时会等待上传结束，再自动重连。" : "缓存设置已保存。";
    public async Task InitializeAsync() => await RunAsync("正在恢复登录", async () =>
    {
        WriteState("starting");
        session = SessionVault.Load();
        if (session is null) { Message = "欢迎使用交云桥。先登录交大云盘，再连接本地盘符。"; WriteState("logged_out"); return; }
        await backend.SetAccessAsync(settings.Access); await ValidateAsync();
        Message = "已恢复登录，可以连接云盘。"; WriteState();
        if (settings.AutoMount && MountManager.DriverInstalled) await MountCoreAsync();
    });
    private async Task<int> ValidateAsync()
    {
        try { entries = await backend.ValidateAsync(session!.UserToken); cloudVerified = true; return entries; }
        catch { cloudVerified = false; throw; }
        finally { checkedAt = DateTimeOffset.Now; Notify(); }
    }
    public async Task LoginAsync() => await RunAsync("正在等待官方登录", async () =>
    {
        await mount.DisconnectAsync(); await backend.SetAccessAsync(settings.Access); WriteState("awaiting_login");
        var result = await login(backend, session);
        if (result is null) { Message = "登录窗口已关闭，原有登录已保留。"; WriteState(); return; }
        SessionVault.Save(result); session = result; await ValidateAsync(); Message = "登录成功，凭证已加密保存。"; WriteState();
        if (settings.AutoMount && MountManager.DriverInstalled) await MountCoreAsync();
    });
    public async Task MountAsync() => await RunAsync("正在连接云盘", MountCoreAsync);
    private async Task MountCoreAsync()
    {
        if (session is null || IsMounted) return;
        await SaveMountSettingsAsync(); await ValidateAsync();
        mount.ConfigureCache(CacheStorage.FromSettings(settings));
        await mount.MountAsync(backend.Address!, session.UserToken, settings.DriveLetter, settings.Access);
        transfer = "可在连接检测中查看上传队列"; Message = $"已连接 {mount.Drive}，当前为{AccessLabel}。"; WriteState();
        if (settings.OpenAfterMount) OpenDrive();
    }
    public async Task DisconnectAsync() => await RunAsync("正在等待上传并断开", async () =>
    {
        await mount.DisconnectAsync(); transfer = "盘符未连接"; RefreshDrives(); Message = "已断开盘符，登录已保留。"; WriteState();
    });
    public async Task<bool> PrepareExitAsync()
    {
        if (busy) return false;
        bool complete = false;
        await RunAsync("正在等待上传并退出", async () => { await mount.DisconnectAsync(); complete = true; });
        return complete;
    }
    private async Task ApplyAsync() => await RunAsync("正在应用挂载设置", async () =>
    {
        bool reconnect = IsMounted; await mount.DisconnectAsync(); await SaveMountSettingsAsync();
        if (session is not null)
        {
            await ValidateAsync();
            if (reconnect) await mount.MountAsync(backend.Address!, session.UserToken, settings.DriveLetter, settings.Access);
        }
        Message = reconnect ? "设置已应用，盘符已重新连接。" : "挂载设置已保存。"; RefreshDrives(); WriteState();
    });
    private async Task SaveMountSettingsAsync()
    {
        if (SelectedDrive is null) throw new CloudAccessException("没有可用的盘符，请刷新列表或断开其他磁盘。");
        // Callers flush/stop the current mount before a permission change.
        if (session is not null) await backend.SetAccessAsync(SelectedAccess);
        var next = settings with { DriveLetter = SelectedDrive, Access = SelectedAccess };
        AppPaths.SaveSettings(next); settings = next; Notify();
    }
    public async Task ApplyCacheAsync() => await RunAsync("正在应用缓存设置", async () =>
    {
        var next = CacheStorage.Parse(CacheDirectoryText, CacheLimitGiBText);
        var previousSettings = settings;
        var previous = CacheStorage.FromSettings(settings);
        CacheStorage.ValidateTransition(previous, next);
        bool changedDirectory = !CacheStorage.SamePath(previous.Directory, next.Directory);
        // Validate first: an invalid destination must leave a live connection untouched.
        await Task.Run(() => { CacheStorage.PrepareDirectory(next); if (changedDirectory) CacheStorage.EnsureClean(next.Directory); });
        bool reconnect = IsMounted, saved = false;
        string? drive = mount.Drive;
        await mount.WaitForUploadsAsync();
        if (changedDirectory) await Task.Run(() => CacheStorage.EnsureClean(previous.Directory));
        try
        {
            await mount.DisconnectAsync();
            if (changedDirectory) await Task.Run(() => { CacheStorage.EnsureClean(previous.Directory); CacheStorage.EnsureClean(next.Directory); });
            mount.ConfigureCache(next);
            var updated = settings with { CacheDirectory = CacheStorage.SamePath(next.Directory, AppPaths.DefaultCache) ? null : next.Directory, CacheMaxBytes = next.MaxBytes };
            AppPaths.SaveSettings(updated); saved = true; settings = updated;
            if (reconnect && session is not null)
                await mount.MountAsync(backend.Address!, session.UserToken, drive!, settings.Access);
            cacheDirectoryText = next.Directory; cacheLimitGiBText = CacheStorage.FormatGiB(next.MaxBytes);
            transfer = reconnect ? "上传队列为空" : "盘符未连接";
            Message = reconnect ? "缓存设置已应用，盘符已重新连接。旧缓存保留在原目录。" : "缓存设置已保存，将在下次连接时使用。旧缓存保留在原目录。";
            WriteState();
        }
        catch (Exception changeError)
        {
            // Restore both persisted preferences and the active mount configuration on failure.
            try
            {
                if (saved || !IsMounted)
                {
                    await mount.DisconnectAsync(); mount.ConfigureCache(previous);
                    if (saved) AppPaths.SaveSettings(previousSettings);
                    settings = previousSettings;
                    if (reconnect && session is not null)
                        await mount.MountAsync(backend.Address!, session.UserToken, drive!, previousSettings.Access);
                }
            }
            catch { throw new CloudAccessException("缓存设置未能应用，旧缓存已保留。请检查磁盘和网络后按原设置重新连接。"); }
            throw new CloudAccessException("缓存设置未能应用，已恢复原设置。" + SafeError.Describe(changeError));
        }
    });
    private async Task TestAsync() => await RunAsync("正在检测连接", async () =>
    {
        var timer = Stopwatch.StartNew(); await backend.SetAccessAsync(settings.Access); await ValidateAsync();
        if (IsMounted) transfer = (await mount.ReadTransferStatusAsync()).Summary;
        Message = $"连接检测通过 · {timer.ElapsedMilliseconds} ms · 根目录 {entries} 个项目。"; WriteState();
    });
    private async Task DiagnoseAsync() => await RunAsync("正在运行诊断", async () =>
    {
        Checks.Clear(); diagnosticsAt = null; Notify();
        await Diagnostics.RunAsync(backend, mount, session is null ? null : ValidateAsync, check => { Checks.Add(check); Notify(); });
        diagnosticsAt = DateTimeOffset.Now; Message = Summarize(Checks); hasError = Checks.Any(c => c.Result == CheckResult.Failed); WriteState();
    });
    public static string Summarize(IEnumerable<DiagnosticCheck> checks)
    {
        var all = checks.ToList();
        int failed = all.Count(c => c.Result == CheckResult.Failed), warning = all.Count(c => c.Result == CheckResult.Warning), skipped = all.Count(c => c.Result == CheckResult.Skipped);
        return failed > 0 ? $"{failed} 项异常 · {warning} 项注意 · {skipped} 项未检查" : warning > 0 ? $"{warning} 项需要注意 · 其余检查已完成" : skipped > 0 ? $"已完成检查 · {skipped} 项因未登录或未挂载跳过" : $"{all.Count} 项检查全部通过";
    }
    private async Task RunAsync(string title, Func<Task> action)
    {
        if (busy) return;
        busy = true; operation = title; hasError = false; message = title + "…"; Notify();
        try { await action(); }
        catch (Exception ex) { ShowError(ex); WriteState(IsMounted ? "mounted" : "operation_failed", ex.GetType().Name); }
        finally { busy = false; Notify(); }
    }
    private void SavePreference(DriveSettings next)
    {
        if (busy) return;
        try { AppPaths.SaveSettings(next); settings = next; hasError = false; message = "偏好设置已保存。"; Notify(); }
        catch (Exception ex) { ShowError(ex); }
    }
    private void RefreshDrives()
    {
        var previous = selectedDrive; var available = MountManager.AvailableDrives();
        if (mount.IsMounted && mount.Drive is not null && !available.Contains(mount.Drive)) available.Add(mount.Drive);
        available.Sort(); AvailableDrives.Clear(); foreach (string drive in available) AvailableDrives.Add(drive);
        selectedDrive = previous is not null && available.Contains(previous) ? previous : available.Contains(settings.DriveLetter) ? settings.DriveLetter : available.FirstOrDefault();
    }
    public void OpenDrive() { if (IsMounted) OpenPath(mount.Drive + "\\"); }
    private void OpenPath(string path) { try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch (Exception ex) { ShowError(ex); } }
    public void ShowError(Exception ex) { hasError = true; message = SafeError.Describe(ex); Notify(); }
    private void WriteState(string? state = null, string? error = null)
    {
        try { AppPaths.WriteStatus(state ?? (IsMounted ? "mounted" : cloudVerified ? "authenticated" : "logged_out"), cloudVerified ? entries : null, mount.Drive, error); }
        catch (IOException) { /* A status file is observational, never a reason to interrupt a live mount. */ }
        catch (UnauthorizedAccessException) { }
    }
    private static string Label(DriveAccess access) => access switch { DriveAccess.ReadOnly => "只读", DriveAccess.NoDelete => "读写，禁止删除", _ => "完整读写" };
    private void Notify()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(""));
        foreach (var command in commands) command.Refresh(); Changed?.Invoke();
    }
    public void Dispose() { mount.Dispose(); backend.Dispose(); }
}
