using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

// Xiaomi MiMo only exposes balance and token-plan data to the signed-in console session,
// so we host the login page in WebView2 and read its cookies for native HTTP calls.
class MiMoConsole : Window {
    const string Origin = "https://platform.xiaomimimo.com";
    const string Home = "https://platform.xiaomimimo.com/#/console/balance";

    WebView2 browser = new WebView2();
    Action<Meter> receive;
    Action<string> failure;
    DispatcherTimer poll;
    bool disposing, ready, busy;

    public MiMoConsole(Window owner, Action<Meter> onData, Action<string> onFailure) {
        Owner = owner;
        ShowInTaskbar = false;
        Icon = owner.Icon;
        receive = onData;
        failure = onFailure;
        Title = "连接小米 MiMo — 登录后会自动同步，可关闭此窗口";
        Width = 1040;
        Height = 760;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Content = browser;
        Closing += (s, e) => {
            if (disposing) return;
            e.Cancel = true;
            Hide();
        };
    }

    public bool Started { get { return ready; } }

    public async Task Start() {
        if (ready) {
            await Fetch();
            return;
        }
        var root = AppDomain.CurrentDomain.BaseDirectory;
        var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(root, "mimo-profile"));
        await browser.EnsureCoreWebView2Async(environment);
        browser.CoreWebView2.NewWindowRequested += (s, e) => { e.Handled = true; };
        browser.Source = new Uri(Home);
        ready = true;
        poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        poll.Tick += async (s, e) => await Fetch();
        poll.Start();
    }

    public async Task Fetch() {
        if (!ready || busy) return;
        busy = true;
        try {
            var cookies = await browser.CoreWebView2.CookieManager.GetCookiesAsync(Origin);
            string header = Join(cookies);
            if (header.IndexOf("api-platform_serviceToken", StringComparison.OrdinalIgnoreCase) < 0) {
                failure("请先在窗口中登录小米账号");
                Resume();
                return;
            }
            var balance = await Get(header, "/api/v1/balance");
            var usage = await Get(header, "/api/v1/tokenPlan/usage");
            var detail = await Get(header, "/api/v1/tokenPlan/detail");
            receive(Data.Mimo(balance, usage, detail));
            if (poll != null) poll.Stop();
            Hide();
        } catch (Exception error) {
            failure(error.Message);
            Resume();
        } finally {
            busy = false;
        }
    }

    // Keep polling while the window is open so an expired session recovers after a fresh login.
    void Resume() {
        if (poll != null && !poll.IsEnabled && IsVisible) poll.Start();
    }

    static string Join(IEnumerable<CoreWebView2Cookie> cookies) {
        var parts = new List<string>();
        foreach (var cookie in cookies) parts.Add(cookie.Name + "=" + cookie.Value);
        return string.Join("; ", parts);
    }

    static async Task<object> Get(string cookie, string path) {
        using (var client = new HttpClient()) {
            client.Timeout = TimeSpan.FromSeconds(20);
            var request = new HttpRequestMessage(HttpMethod.Get, Origin + path);
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            request.Headers.TryAddWithoutValidation("Referer", Home);
            using (var response = await client.SendAsync(request)) {
                if ((int)response.StatusCode == 401) throw new Exception("登录已过期，请在窗口中重新登录");
                if (!response.IsSuccessStatusCode) throw new Exception("HTTP " + (int)response.StatusCode);
                return Data.Json().DeserializeObject(await response.Content.ReadAsStringAsync());
            }
        }
    }

    public void Shutdown() {
        disposing = true;
        if (poll != null) poll.Stop();
        browser.Dispose();
        Close();
    }
}
