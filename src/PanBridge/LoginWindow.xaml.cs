using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace PanBridge;

public partial class LoginWindow : Window
{
    private readonly Func<string, Task<int>> validate;
    private readonly AuthSession? previous;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool checking;
    private bool finished;
    private string? attemptedToken;
    public AuthSession? Session { get; private set; }
    public int Entries { get; private set; }
    public LoginWindow(Func<string, Task<int>> validate, AuthSession? previous)
    {
        InitializeComponent(); this.validate = validate; this.previous = previous;
        Loaded += async (_, _) => await InitializeBrowserAsync();
        timer.Tick += async (_, _) => await CheckCookiesAsync();
        Closed += (_, _) => { finished = true; timer.Stop(); Browser.Dispose(); };
    }
    private async Task InitializeBrowserAsync()
    {
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(null, AppPaths.Browser, new CoreWebView2EnvironmentOptions { Language = "zh-CN" });
            await Browser.EnsureCoreWebView2Async(environment);
            Browser.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
            Browser.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
            Browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Browser.CoreWebView2.NewWindowRequested += (_, args) => { args.Handled = true; if (AllowedNavigation(args.Uri)) Browser.CoreWebView2.Navigate(args.Uri); };
            Browser.CoreWebView2.NavigationStarting += (_, args) => { if (!AllowedNavigation(args.Uri)) { args.Cancel = true; LoginStatus.Text = "登录页面跳转到非交大站点，已暂停跳转。"; } };
            Browser.CoreWebView2.NavigationCompleted += async (_, _) => await CheckCookiesAsync();
            if (previous is not null)
            {
                foreach (var saved in previous.Cookies.Where(c => c.Session || c.Expires.ToUniversalTime() > DateTime.UtcNow))
                {
                    if (!SessionRules.AllowedDomain(saved.Domain)) continue;
                    var cookie = Browser.CoreWebView2.CookieManager.CreateCookie(saved.Name, saved.Value, saved.Domain, saved.Path);
                    cookie.IsSecure = saved.Secure; cookie.IsHttpOnly = saved.HttpOnly; cookie.SameSite = (CoreWebView2CookieSameSiteKind)saved.SameSite;
                    if (!saved.Session) cookie.Expires = saved.Expires;
                    Browser.CoreWebView2.CookieManager.AddOrUpdateCookie(cookie);
                }
            }
            LoginStatus.Text = "请在交大官方页面登录，成功后将自动返回客户端。";
            Browser.CoreWebView2.Navigate("https://pan.sjtu.edu.cn/");
            timer.Start();
        }
        catch (Exception ex) { LoginStatus.Text = SafeError.Describe(ex); }
    }
    private static bool AllowedNavigation(string url) => url == "about:blank" || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && (uri.Host == "sjtu.edu.cn" || uri.Host.EndsWith(".sjtu.edu.cn", StringComparison.OrdinalIgnoreCase)));
    private static SavedCookie SaveCookie(CoreWebView2Cookie c) => new(c.Name, c.Value, c.Domain, c.Path, c.IsSecure, c.IsHttpOnly, c.IsSession, c.Expires, (int)c.SameSite);
    private async Task CheckCookiesAsync()
    {
        if (checking || finished || Browser.CoreWebView2 is null) return;
        checking = true;
        try
        {
            var cookies = (await Browser.CoreWebView2.CookieManager.GetCookiesAsync("https://pan.sjtu.edu.cn/")).Select(SaveCookie).ToList();
            var token = SessionRules.SelectToken(cookies);
            if (token is null || token.Value == attemptedToken) return;
            attemptedToken = token.Value;
            LoginStatus.Text = "已检测到登录凭证，正在验证云盘目录…";
            int entries = await validate(token.Value);
            if (finished) return;
            cookies.AddRange((await Browser.CoreWebView2.CookieManager.GetCookiesAsync("https://jaccount.sjtu.edu.cn/jaccount/")).Select(SaveCookie));
            Session = new AuthSession(token.Value, DateTimeOffset.Now, cookies.Where(c => SessionRules.AllowedDomain(c.Domain)).ToList());
            Entries = entries;
            timer.Stop();
            DialogResult = true;
        }
        catch (Exception ex) { if (!finished) LoginStatus.Text = SafeError.Describe(ex) + " 可在页面中退出账号后重新登录。"; }
        finally { checking = false; }
    }
}
