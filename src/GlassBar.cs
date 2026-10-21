using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

// The floating bar window: tray icon, docking, hover cards, settings and refresh timer.
class GlassBar : Window {
    const double UiScale = .75;
    static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
    static readonly string SettingsPath = Path.Combine(Root, "settings.dat");

    readonly Viewbox compact = new Viewbox { Stretch = Stretch.Uniform };
    readonly BarFace face = new BarFace();
    readonly DispatcherTimer refresh = new DispatcherTimer();
    readonly DispatcherTimer spring = new DispatcherTimer();
    readonly Popup hover = new Popup { AllowsTransparency = true, StaysOpen = true };
    Config config = new Config();
    DeepSeekConsole console;
    WinForms.NotifyIcon tray;
    System.Drawing.Icon trayIcon;
    bool busy, dragging, moved, preview;
    Point start, origin;
    double velocity;
    string state = "未刷新";

    public GlassBar(bool demo = false) {
        preview = demo;
        Title = "Token Bar · Glass";
        Width = 132 * UiScale;
        Height = 306 * UiScale;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = false;

        if (!demo) LoadConfig();
        face.Limits = Limits.From(config);
        face.Width = 132;
        face.Height = 306;
        compact.Child = face;
        Content = compact;
        face.Light = config.Theme == "light";
        face.Edge = config.Edge ?? "right";
        Orient(face.Edge);

        refresh.Interval = TimeSpan.FromMinutes(5);
        refresh.Tick += async (s, e) => await Refresh();
        spring.Interval = TimeSpan.FromMilliseconds(16);
        spring.Tick += (s, e) => Spring();

        MouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseLeave += (s, e) => { face.Hover = -1; hover.IsOpen = false; };
        MouseLeftButtonUp += OnMouseUp;
        MouseRightButtonUp += (s, e) => Menu();
        LostMouseCapture += (s, e) => {
            if (!dragging) return;
            dragging = false;
            Magnet(true);
            spring.Start();
        };
        Loaded += OnLoaded;
        Closed += (s, e) => {
            DisposeTray();
            hover.IsOpen = false;
            refresh.Stop();
            spring.Stop();
            if (console != null) console.Shutdown();
            if (!preview) Save();
        };
    }

    void LoadConfig() {
        try {
            if (!File.Exists(SettingsPath)) return;
            var bytes = System.Security.Cryptography.ProtectedData.Unprotect(
                File.ReadAllBytes(SettingsPath), null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
            config = Data.Json().Deserialize<Config>(System.Text.Encoding.UTF8.GetString(bytes));
        } catch {
            state = "无法读取设置，请重新连接";
        }
    }

    async void OnLoaded(object sender, RoutedEventArgs e) {
        if (!preview) CreateTray();
        var area = Area();
        Left = config.X < 0 ? area.Right - Width + 10 * UiScale : config.X;
        Top = config.X < 0 ? area.Top + 120 : config.Y;
        Clamp();
        Magnet(true);
        if (preview) return;
        refresh.Start();
        if (config.ConsoleConnected) await ConnectConsole(false);
        await Refresh();
    }

    void OnMouseDown(object sender, MouseButtonEventArgs e) {
        hover.IsOpen = false;
        dragging = true;
        moved = false;
        start = Pointer();
        origin = new Point(Left, Top);
        CaptureMouse();
    }

    void OnMouseMove(object sender, MouseEventArgs e) {
        var p = e.GetPosition(face);
        if (dragging) {
            var delta = Pointer() - start;
            if (delta.Length > 4) moved = true;
            if (!moved) return;
            Left = origin.X + delta.X;
            Top = origin.Y + delta.Y;
            Magnet(false);
            return;
        }
        int hit = face.Hit(p);
        if (hit == face.Hover) return;
        face.Hover = hit;
        ShowHover(hit);
    }

    void OnMouseUp(object sender, MouseButtonEventArgs e) {
        if (!dragging) return;
        dragging = false;
        ReleaseMouseCapture();
        if (moved) {
            Magnet(true);
            velocity = -face.Pull * .8;
            spring.Start();
            Save();
            return;
        }
        int hit = face.Hit(e.GetPosition(face));
        if (hit == 3) ToggleTheme();
        else if (hit == 4) Menu();
        else ShowHover(hit);
    }

    void CreateTray() {
        using (var stream = typeof(GlassBar).Assembly.GetManifestResourceStream("tokenbar.ico"))
        using (var original = new System.Drawing.Icon(stream)) {
            trayIcon = (System.Drawing.Icon)original.Clone();
        }
        Icon = Imaging.CreateBitmapSourceFromHIcon(trayIcon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        tray = new WinForms.NotifyIcon { Icon = trayIcon, Text = "Token Bar", Visible = true };
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("显示浮条", null, (s, e) => { Show(); WindowState = WindowState.Normal; Activate(); });
        menu.Items.Add("隐藏浮条", null, (s, e) => { hover.IsOpen = false; Hide(); });
        menu.Items.Add("连接设置", null, (s, e) => { Show(); Settings(); });
        menu.Items.Add("刷新", null, async (s, e) => await Refresh());
        menu.Items.Add("切换深色 / 浅色", null, (s, e) => ToggleTheme());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("退出", null, (s, e) => Close());
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (s, e) => { Show(); WindowState = WindowState.Normal; Activate(); };
    }

    void DisposeTray() {
        if (tray != null) {
            tray.Visible = false;
            var menu = tray.ContextMenuStrip;
            tray.Dispose();
            if (menu != null) menu.Dispose();
            tray = null;
        }
        if (trayIcon != null) {
            trayIcon.Dispose();
            trayIcon = null;
        }
    }

    Point Pointer() {
        var p = WinForms.Control.MousePosition;
        var source = PresentationSource.FromVisual(this);
        return source != null
            ? source.CompositionTarget.TransformFromDevice.Transform(new Point(p.X, p.Y))
            : new Point(p.X, p.Y);
    }

    Rect Area() {
        var p = WinForms.Control.MousePosition;
        var work = WinForms.Screen.FromPoint(p).WorkingArea;
        var source = PresentationSource.FromVisual(this);
        var transform = source == null ? Matrix.Identity : source.CompositionTarget.TransformFromDevice;
        return new Rect(transform.Transform(new Point(work.Left, work.Top)), transform.Transform(new Point(work.Right, work.Bottom)));
    }

    void Clamp() {
        var area = Area();
        Left = Math.Max(area.Left - 10 * UiScale, Math.Min(Left, area.Right - Width + 10 * UiScale));
        Top = Math.Max(area.Top - 7 * UiScale, Math.Min(Top, area.Bottom - Height));
    }

    public static string Nearest(Rect area, double x, double y, double width, out double distance, double scale = 1) {
        double l = Math.Abs(x + 10 * scale - area.Left),
               r = Math.Abs(x + width - 10 * scale - area.Right),
               t = Math.Abs(y + 7 * scale - area.Top);
        distance = Math.Min(l, Math.Min(r, t));
        return distance == t ? "top" : distance == l ? "left" : "right";
    }

    void Orient(string edge) {
        bool horizontal = edge == "top";
        if (face.Horizontal != horizontal) {
            double center = Left + Width / 2;
            face.Horizontal = horizontal;
            face.Width = horizontal ? 344 : 132;
            face.Height = horizontal ? 118 : 306;
            Width = face.Width * UiScale;
            Height = face.Height * UiScale;
            if (!double.IsNaN(center)) Left = center - Width / 2;
        }
        face.InvalidateMeasure();
        face.InvalidateVisual();
    }

    void Magnet(bool snap) {
        var area = Area();
        double distance;
        string edge = Nearest(area, Left, Top, Width, out distance, UiScale);
        if (distance < 60) {
            face.Edge = edge;
            face.Pull = (60 - distance) / 60 * 8;
            if (snap) {
                Orient(edge);
                if (edge == "left") Left = area.Left - 10 * UiScale;
                else if (edge == "right") Left = area.Right - Width + 10 * UiScale;
                else Top = area.Top - 7 * UiScale;
                config.Edge = edge;
            }
        } else {
            face.Pull = 0;
        }
        if (snap) Clamp();
        face.InvalidateVisual();
    }

    void Spring() {
        velocity += -face.Pull * .18;
        velocity *= .72;
        face.Pull += velocity;
        face.RenderTransformOrigin = new Point(face.Edge == "left" ? 0 : face.Edge == "right" ? 1 : .5, face.Edge == "top" ? 0 : .5);
        face.RenderTransform = new ScaleTransform(
            face.Edge == "top" ? 1 + face.Pull * .002 : 1 + face.Pull * .006,
            face.Edge == "top" ? 1 + face.Pull * .006 : 1 - face.Pull * .002);
        face.InvalidateVisual();
        if (Math.Abs(face.Pull) + Math.Abs(velocity) < .035) {
            face.Pull = 0;
            face.RenderTransform = Transform.Identity;
            spring.Stop();
        }
    }

    bool Save() {
        if (preview) return true;
        config.X = (int)Left;
        config.Y = (int)Top;
        config.Theme = face.Light ? "light" : "dark";
        try {
            var json = Data.Json().Serialize(config);
            var bytes = System.Security.Cryptography.ProtectedData.Protect(
                System.Text.Encoding.UTF8.GetBytes(json), null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
            File.WriteAllBytes(SettingsPath, bytes);
            return true;
        } catch {
            state = "设置保存失败";
            return false;
        }
    }

    void ToggleTheme() {
        face.Light = !face.Light;
        face.InvalidateVisual();
        hover.IsOpen = false;
        Save();
    }

    void Menu() {
        hover.IsOpen = false;
        var menu = new ContextMenu();
        Action<string, Action> add = (label, action) => {
            var item = new MenuItem { Header = label };
            item.Click += (s, e) => action();
            menu.Items.Add(item);
        };
        add("连接设置", () => Settings());
        add("连接 DeepSeek 控制台", async () => await ConnectConsole(true));
        add("刷新", async () => await Refresh());
        add(face.Light ? "切换 Dark" : "切换 Light", () => ToggleTheme());
        add(Topmost ? "取消置顶" : "保持置顶", () => Topmost = !Topmost);
        menu.Items.Add(new Separator());
        add("退出", () => Close());
        menu.PlacementTarget = face;
        menu.IsOpen = true;
    }

    async Task ConnectConsole(bool show) {
        try {
            config.ConsoleConnected = true;
            face.Usage = null;
            face.Balance = new Meter("DeepSeek", "待登录", "请在控制台窗口登录 DeepSeek");
            if (console == null) {
                console = new DeepSeekConsole(this,
                    snapshot => {
                        face.Balance = snapshot.Balance;
                        face.Usage = snapshot.Usage;
                        state = "控制台已同步 " + DateTime.Now.ToString("HH:mm");
                        face.InvalidateVisual();
                    },
                    reason => {
                        face.Usage = null;
                        if (reason.StartsWith("usage:") && (face.Balance.Value.StartsWith("¥") || face.Balance.Value.StartsWith("$")))
                            face.Balance.Detail += "\n用量查询失败：" + reason;
                        else
                            face.Balance = new Meter("DeepSeek", reason.StartsWith("auth:") ? "待登录" : "读取失败", reason);
                        face.InvalidateVisual();
                    });
                console.ShowActivated = show;
                console.Show();
                await console.Start();
                if (!show) console.Hide();
            } else {
                if (show) {
                    console.Show();
                    console.Activate();
                }
                await console.Refresh();
            }
            Save();
        } catch {
            if (console != null) {
                console.Shutdown();
                console = null;
            }
            face.Balance = new Meter("DeepSeek", "未连接", "控制台组件未能启动。请确认已安装 Microsoft Edge WebView2 Runtime。");
            if (show) MessageBox.Show(this, face.Balance.Detail, "控制台连接");
        }
        face.InvalidateVisual();
    }

    void ShowHover(int hit) {
        if (dragging || hit < 0) {
            hover.IsOpen = false;
            return;
        }
        string text =
            hit == 1 ? "ZHIPU · PLAN\n\n外圈 / 5 小时   " + Data.Pct(face.Plan.FiveHour) + " 剩余\n内圈 / 周额度   "
                + Data.Pct(face.Plan.Weekly) + " 剩余\n\n" + face.Plan.Detail
            : hit == 2 ? "DEEPSEEK · 今日累计\n\n余额   " + face.Balance.Value + "\n当日花费   " + face.Spend
                + "\nToken 总消耗   " + (face.Usage != null && face.Usage.Tokens.HasValue ? face.Usage.Tokens.Value.ToString("N0") : "暂无数据")
                + "\n\n" + (face.Usage == null ? "余额接口不含当日花费与 token 用量。\n请在设置中连接用量数据。" : face.Usage.Source + "\n" + face.Usage.UpdatedAt)
                + "\n\n" + face.Balance.Detail
            : hit == 3 ? "切换深色 / 浅色"
            : hit == 4 ? "连接、刷新与退出"
            : "拖动浮条 · 靠近上 / 左 / 右边缘吸附\n" + state;

        var box = new Border {
            Background = Paint.B(face.Light ? "#F3F7FBFF" : "#F0202D40"),
            BorderBrush = Paint.B(face.Light ? "#FFE0E7F0" : "#FF455570"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(18),
            Child = new TextBlock {
                Text = text,
                Foreground = face.Ink,
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"),
                MaxWidth = 300,
                TextWrapping = TextWrapping.Wrap
            }
        };
        hover.Child = box;
        hover.PlacementTarget = face;
        hover.Placement = face.Horizontal ? PlacementMode.Bottom : face.Edge == "right" ? PlacementMode.Left : PlacementMode.Right;
        hover.HorizontalOffset = 8;
        hover.VerticalOffset = face.Horizontal ? 8 : hit == 2 ? 140 * UiScale : 35 * UiScale;
        hover.IsOpen = true;
    }

    async Task Refresh() {
        if (busy) return;
        busy = true;
        face.Busy = true;
        if (face.Usage != null && face.Usage.Date != DateTime.Now.ToString("yyyy-MM-dd")) face.Usage = null;
        face.InvalidateVisual();

        var jobs = new List<Task>();
        jobs.Add(Update(true, config.Glm, config.International));
        if (!config.ConsoleConnected) jobs.Add(Update(false, config.DeepSeek, false));
        else if (console != null) jobs.Add(console.Refresh());
        try {
            await Task.WhenAll(jobs);
        } catch {
            state = "刷新失败，请重连控制台";
        }

        if (!config.ConsoleConnected) {
            try { face.Usage = TodayUsage.Load(config.UsageFile); } catch { face.Usage = null; }
        }
        state = "更新于 " + DateTime.Now.ToString("HH:mm") + " · 每 5 分钟刷新";
        busy = false;
        face.Busy = false;
        face.InvalidateVisual();
        if (hover.IsOpen) ShowHover(face.Hover);
    }

    async Task Update(bool plan, string key, bool international) {
        Meter result;
        if (string.IsNullOrEmpty(key)) {
            result = new Meter(plan ? "GLM" : "DeepSeek", "未连接", "请在设置中填写 API Key");
        } else {
            try {
                string url = plan
                    ? (international ? "https://api.z.ai/api/monitor/usage/quota/limit" : "https://open.bigmodel.cn/api/monitor/usage/quota/limit")
                    : "https://api.deepseek.com/user/balance";
                var json = await Data.Fetch(url, key, !plan);
                result = plan ? Data.Glm(json) : Data.Deep(json);
            } catch {
                result = new Meter(plan ? "GLM" : "DeepSeek", "读取失败", "请检查网络、Key 或套餐地区后刷新");
            }
        }
        if (plan) face.Plan = result;
        else face.Balance = result;
    }

    void Settings() {
        var win = new Window {
            Title = "Token Bar · 连接",
            ShowInTaskbar = false,
            Icon = Icon,
            Width = 450,
            Height = 620,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            Topmost = true,
            Background = Paint.B(face.Light ? "#F4F7FC" : "#202C3D")
        };
        var panel = new StackPanel { Margin = new Thickness(24) };
        // Scrolls instead of clipping when the system font or DPI leaves less room than expected.
        win.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Action<string> label = text => panel.Children.Add(new TextBlock {
            Text = text, Foreground = face.Ink, Margin = new Thickness(0, 8, 0, 5)
        });

        label("GLM 个人套餐 API Key");
        var glm = new PasswordBox { Password = config.Glm, Padding = new Thickness(6) };
        panel.Children.Add(glm);

        label("DeepSeek API Key");
        var deep = new PasswordBox { Password = config.DeepSeek, Padding = new Thickness(6) };
        panel.Children.Add(deep);

        var region = new CheckBox {
            Content = "GLM 使用国际站 Z.ai",
            IsChecked = config.International,
            Foreground = face.Ink,
            Margin = new Thickness(0, 12, 0, 8)
        };
        panel.Children.Add(region);

        label("今日用量文件（可选；当前余额接口不提供）");
        var usage = new TextBox { Text = config.UsageFile, Padding = new Thickness(6) };
        panel.Children.Add(usage);

        label("配色阈值");
        panel.Children.Add(new TextBlock {
            Text = "额度低于警戒线时圆环由绿转黄；今日花费达到预警转黄、达到告警转红，超过告警为深红。",
            Foreground = face.Muted,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4)
        });

        var current = face.Limits;
        label("额度警戒线（剩余 %，默认 50）");
        var quotaWarn = new TextBox { Text = current.QuotaWarn.ToString("0.##", CultureInfo.InvariantCulture), Padding = new Thickness(6) };
        panel.Children.Add(quotaWarn);

        label("今日花费预警 / 告警（元，默认 2.5 / 5）");
        var costRow = new Grid();
        costRow.ColumnDefinitions.Add(new ColumnDefinition());
        costRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        costRow.ColumnDefinitions.Add(new ColumnDefinition());
        var costWarn = new TextBox { Text = current.CostWarn.ToString("0.##", CultureInfo.InvariantCulture), Padding = new Thickness(6) };
        var costDanger = new TextBox { Text = current.CostDanger.ToString("0.##", CultureInfo.InvariantCulture), Padding = new Thickness(6) };
        Grid.SetColumn(costWarn, 0);
        Grid.SetColumn(costDanger, 2);
        costRow.Children.Add(costWarn);
        costRow.Children.Add(costDanger);
        panel.Children.Add(costRow);

        var reset = new Button {
            Content = "恢复默认阈值",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0),
            Padding = new Thickness(10, 3, 10, 3)
        };
        reset.Click += (s, e) => {
            quotaWarn.Text = Limits.DefaultQuotaWarn.ToString("0.##", CultureInfo.InvariantCulture);
            costWarn.Text = Limits.DefaultCostWarn.ToString("0.##", CultureInfo.InvariantCulture);
            costDanger.Text = Limits.DefaultCostDanger.ToString("0.##", CultureInfo.InvariantCulture);
        };
        panel.Children.Add(reset);

        var button = new Button { Content = "保存并刷新", Margin = new Thickness(0, 20, 0, 0), Padding = new Thickness(8) };
        button.Click += async (s, e) => {
            Limits updated;
            string error;
            if (!Limits.TryParse(quotaWarn.Text, costWarn.Text, costDanger.Text, out updated, out error)) {
                MessageBox.Show(win, error, "配色阈值");
                return;
            }
            config.Glm = glm.Password.Trim();
            config.DeepSeek = deep.Password.Trim();
            config.International = region.IsChecked == true;
            config.UsageFile = usage.Text.Trim();
            updated.StoreTo(config);
            face.Limits = updated;
            face.InvalidateVisual();
            Save();
            win.Close();
            await Refresh();
        };
        panel.Children.Add(button);

        win.ShowDialog();
    }

    static void Check(bool ok) { if (!ok) throw new Exception("Check failed"); }

    static void Preview() {
        var face = new BarFace();
        face.Plan = new Meter("GLM", "", "示例") { FiveHour = 82, Weekly = 43 };
        face.Balance = new Meter("DeepSeek", "¥28.60", "示例");
        face.Usage = new TodayUsage { Date = DateTime.Now.ToString("yyyy-MM-dd"), Cost = .86m, Tokens = 124800, Currency = "CNY", Source = "示例数据" };
        face.Measure(new Size(132, 306));
        face.Arrange(new Rect(0, 0, 132, 306));

        var visual = new DrawingVisual();
        using (var d = visual.RenderOpen()) {
            d.DrawRectangle(new LinearGradientBrush(Paint.C("#26364C"), Paint.C("#91A1B9"), 30), null, new Rect(0, 0, 700, 420));
            Paint.Text(d, "COMPACT / GLASS", 20, Brushes.White, new Point(28, 20), true);
            Paint.Text(d, "示例数据 · 0–5 元连续渐变 · 超过 5 元深红", 11, Brushes.White, new Point(29, 52));

            decimal[] costs = { 0m, 2.5m, 5m, 6.8m };
            for (int i = 0; i < 4; i++) {
                face.Light = i % 2 == 1;
                face.Usage.Cost = costs[i];
                d.PushTransform(new TranslateTransform(32 + i * 164, 96));
                d.PushTransform(new ScaleTransform(UiScale, UiScale));
                face.Render(d);
                d.Pop();
                d.Pop();
                Paint.Text(d, "¥" + costs[i].ToString("0.00"), 12, Brushes.White, new Point(66 + i * 164, 341), true);
            }
            Paint.Text(d, "竖向 99×230  /  顶部横向 258×89   ·   显示大小随系统缩放变化", 11, Brushes.White, new Point(29, 388));
        }
        var bitmap = new RenderTargetBitmap(1400, 840, 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(Path.Combine(Root, "compact-preview.png"))) encoder.Save(file);
    }

    [STAThread]
    static void Main(string[] args) {
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
        var app = new Application();
        if (args.Length > 0 && args[0] == "--test") {
            try {
                GlassChecks.Run();
                Check(Paint.Quota(100) != Paint.Quota(75));
                double delta;
                Check(Nearest(new Rect(0, 0, 1920, 1080), -10, 300, 132, out delta) == "left" && delta == 0);
                Check(Nearest(new Rect(0, 0, 1920, 1080), 500, -7, 132, out delta) == "top" && delta == 0);
                Preview();
                File.WriteAllText(Path.Combine(Root, "glass-test.txt"),
                    "PASS: vector icons, dark/light render, nested rings, top/left docking geometry, continuous colors, configurable thresholds. Live transparency and pointer animation require desktop verification.");
            } catch (Exception e) {
                File.WriteAllText(Path.Combine(Root, "glass-test.txt"), e.ToString());
                Environment.ExitCode = 1;
            }
            return;
        }
        bool created;
        using (var mutex = new System.Threading.Mutex(true, "Local\\TokenBar_Desktop_v1", out created)) {
            if (!created) {
                MessageBox.Show("请先退出正在运行的旧版 Token Bar。");
                return;
            }
            app.Run(new GlassBar());
        }
    }
}
