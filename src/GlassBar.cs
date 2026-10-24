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
    readonly Dictionary<string, Row> rows = new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);
    readonly List<Row> liveRows = new List<Row>();
    readonly DailyLog daily = DailyLog.Load(Path.Combine(Root, "daily.json"));
    Config config = new Config();
    DeepSeekConsole console;
    MiMoConsole mimo;
    TaskbarStrip strip;
    WinForms.NotifyIcon tray;
    System.Drawing.Icon trayIcon;
    bool busy, dragging, moved, preview;
    Point start, origin;
    double velocity;
    Rect lastArea = Rect.Empty;
    string state = "未刷新";
    int cycle;

    public GlassBar(bool demo = false) {
        preview = demo;
        Title = "Token Bar · Glass";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = false;

        if (!demo) LoadConfig();
        face.Limits = Limits.From(config);
        compact.Child = face;
        Content = compact;
        face.Light = config.Theme == "light";
        face.Edge = config.Edge ?? "right";
        RebuildRows();
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
        // Taskbar mode and the floating bar never show at the same time: whichever surface is
        // visible owns the screen, so showing one hides the other.
        IsVisibleChanged += (s, e) => {
            if (!config.Taskbar || strip == null) return;
            if (IsVisible) {
                strip.Hide();
            } else {
                strip.Show();
                strip.UpdateLayout();
                strip.Place(config.TaskbarX);
            }
        };
        Closed += (s, e) => {
            DisposeTray();
            hover.IsOpen = false;
            refresh.Stop();
            spring.Stop();
            if (console != null) console.Shutdown();
            if (mimo != null) mimo.Shutdown();
            if (strip != null) strip.Close();
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
        if (config.MimoConnected) await ConnectMimo(false);
        await Refresh();
        EnsureStrip();
        // Taskbar mode replaces the floating bar; it stays reachable from the tray menu.
        if (config.Taskbar) Hide();
    }

    // ---- layout -------------------------------------------------------

    void Layout() {
        double width = face.Horizontal ? face.ContentWidth : 132;
        double height = face.Horizontal ? 118 : face.ContentHeight;
        face.Width = width;
        face.Height = height;
        Width = width * UiScale;
        Height = height * UiScale;
        face.InvalidateMeasure();
        face.InvalidateVisual();
    }

    void Orient(string edge) {
        bool horizontal = edge == "top";
        bool changed = face.Horizontal != horizontal;
        double center = Left + Width / 2;
        face.Horizontal = horizontal;
        Layout();
        if (changed && !double.IsNaN(center) && !double.IsNaN(Left)) Left = center - Width / 2;
    }

    void RebuildRows() {
        liveRows.Clear();
        foreach (var info in Catalog.All) {
            if (!Catalog.Configured(info, config)) continue;
            Row row;
            if (!rows.TryGetValue(info.Id, out row)) {
                row = new Row { Id = info.Id, Label = info.Label, Meter = new Meter(info.Label, "待刷新", "稍后自动刷新") };
                rows[info.Id] = row;
            }
            liveRows.Add(row);
        }

        if (liveRows.Count == 0) {
            face.Primary = new Row { Id = "glm", Label = "未配置", Meter = new Meter("", "打开设置", "右键浮条或托盘图标，选择「连接设置」填写 API Key") };
            face.Rows = new List<Row>();
            UpdateStrip();
            return;
        }

        Row primary = null;
        var wanted = Catalog.Get(config.Primary ?? "glm");
        foreach (var row in liveRows) if (wanted != null && row.Id == wanted.Id) primary = row;
        if (primary == null) foreach (var row in liveRows) if (row.Id == "glm") primary = row;
        if (primary == null) foreach (var row in liveRows) if (row.Meter.FiveHour.HasValue || row.Meter.Weekly.HasValue) { primary = row; break; }
        if (primary == null && liveRows.Count > 0) primary = liveRows[0];

        var rest = new List<Row>();
        foreach (var row in liveRows) if (row != primary) rest.Add(row);

        face.Primary = primary;
        if (config.Display == "cycle" && rest.Count > 0) {
            cycle = ((cycle % rest.Count) + rest.Count) % rest.Count;
            face.Rows = new List<Row> { rest[cycle] };
        } else {
            face.Rows = rest;
        }
        UpdateStrip();
    }

    // ---- taskbar strip -------------------------------------------------

    void UpdateStrip() {
        if (strip == null) return;
        strip.Update(new List<Row>(liveRows), face.Light, face.Limits);
    }

    void EnsureStrip() {
        if (preview) return;
        if (!config.Taskbar) {
            if (strip != null && strip.IsVisible) strip.Hide();
            return;
        }
        try {
            if (strip == null) {
                strip = new TaskbarStrip();
                strip.ProviderClicked = ShowProviderFromStrip;
                strip.MenuRequested = () => BuildTrayMenu().Show(WinForms.Cursor.Position);
                strip.PositionChanged = x => { config.TaskbarX = x; Save(); };
            }
            UpdateStrip();
            if (!strip.IsVisible) strip.Show();
            // The window sizes itself to the content, so measure before placing it.
            strip.UpdateLayout();
            strip.Place(config.TaskbarX);
        } catch (Exception error) {
            // A broken strip must not take the whole widget down with it.
            state = "任务栏条创建失败：" + error.Message;
            try { if (strip != null) strip.Close(); } catch { }
            strip = null;
            config.Taskbar = false;
        }
    }

    void ToggleTaskbar() {
        config.Taskbar = !config.Taskbar;
        EnsureStrip();
        if (config.Taskbar) {
            hover.IsOpen = false;
            Hide();
        } else {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }
        Save();
    }

    // A click on a strip tile reveals the floating bar with that provider's card open.
    void ShowProviderFromStrip(string id) {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        for (int i = 0; i < face.Rows.Count; i++) {
            if (face.Rows[i].Id != id) continue;
            face.Hover = 100 + i;
            ShowHover(face.Hover);
            return;
        }
        if (face.Primary != null && face.Primary.Id == id) {
            face.Hover = 1;
            ShowHover(1);
        }
    }

    Row GetRow(string id) {
        Row row;
        if (rows.TryGetValue(id, out row)) return row;
        return null;
    }

    // ---- mouse --------------------------------------------------------

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
        lastArea = new Rect(transform.Transform(new Point(work.Left, work.Top)), transform.Transform(new Point(work.Right, work.Bottom)));
        return lastArea;
    }

    void Clamp() {
        var area = Area();
        Left = Math.Max(area.Left - 10 * UiScale, Math.Min(Left, area.Right - Width + 10 * UiScale));
        Top = Math.Max(area.Top - 7 * UiScale, Math.Min(Top, area.Bottom - Height));
    }

    // Glue a docked bar to its edge after the content height changed, without resampling the mouse.
    void Dock() {
        if (lastArea.IsEmpty) return;
        if (face.Edge == "left") Left = lastArea.Left - 10 * UiScale;
        else if (face.Edge == "right") Left = lastArea.Right - Width + 10 * UiScale;
        else if (face.Edge == "top") Top = lastArea.Top - 7 * UiScale;
        Left = Math.Max(lastArea.Left - 10 * UiScale, Math.Min(Left, lastArea.Right - Width + 10 * UiScale));
        Top = Math.Max(lastArea.Top - 7 * UiScale, Math.Min(Top, lastArea.Bottom - Height));
    }

    public static string Nearest(Rect area, double x, double y, double width, out double distance, double scale = 1) {
        double l = Math.Abs(x + 10 * scale - area.Left),
               r = Math.Abs(x + width - 10 * scale - area.Right),
               t = Math.Abs(y + 7 * scale - area.Top);
        distance = Math.Min(l, Math.Min(r, t));
        return distance == t ? "top" : distance == l ? "left" : "right";
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
        else if (hit >= 100 && config.Display == "cycle") {
            cycle++;
            RebuildRows();
            Layout();
            Dock();
        } else ShowHover(hit);
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

    // ---- tray ---------------------------------------------------------

    void CreateTray() {
        using (var stream = typeof(GlassBar).Assembly.GetManifestResourceStream("tokenbar.ico"))
        using (var original = new System.Drawing.Icon(stream)) {
            trayIcon = (System.Drawing.Icon)original.Clone();
        }
        Icon = Imaging.CreateBitmapSourceFromHIcon(trayIcon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        tray = new WinForms.NotifyIcon { Icon = trayIcon, Text = "Token Bar", Visible = true };
        tray.ContextMenuStrip = BuildTrayMenu();
        tray.DoubleClick += (s, e) => { Show(); WindowState = WindowState.Normal; Activate(); };
    }

    // Shared by the tray icon and the taskbar strip; WinForms menus work without activation,
    // which a WPF ContextMenu cannot guarantee for the no-activate strip window.
    WinForms.ContextMenuStrip BuildTrayMenu() {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("显示浮条", null, (s, e) => { Show(); WindowState = WindowState.Normal; Activate(); });
        menu.Items.Add("隐藏浮条", null, (s, e) => { hover.IsOpen = false; Hide(); });
        var taskbarItem = new WinForms.ToolStripMenuItem("任务栏模式");
        taskbarItem.Checked = config.Taskbar;
        taskbarItem.Click += (s, e) => { ToggleTaskbar(); taskbarItem.Checked = config.Taskbar; };
        menu.Items.Add(taskbarItem);
        menu.Opening += (s, e) => taskbarItem.Checked = config.Taskbar;
        menu.Items.Add("连接设置", null, (s, e) => { Show(); Settings(); });
        menu.Items.Add("刷新", null, async (s, e) => await Refresh());
        menu.Items.Add("连接 DeepSeek 控制台", null, async (s, e) => await ConnectConsole(true));
        menu.Items.Add("连接 MiMo 控制台", null, async (s, e) => await ConnectMimo(true));
        menu.Items.Add("切换深色 / 浅色", null, (s, e) => ToggleTheme());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("退出", null, (s, e) => Close());
        return menu;
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

    // ---- menus --------------------------------------------------------

    void Menu() {
        hover.IsOpen = false;
        var menu = new ContextMenu();
        Action<string, Action> add = (label, action) => {
            var item = new MenuItem { Header = label };
            item.Click += (s, e) => action();
            menu.Items.Add(item);
        };
        add("连接设置", () => Settings());
        add("刷新", async () => await Refresh());
        add("连接 DeepSeek 控制台", async () => await ConnectConsole(true));
        add("连接 MiMo 控制台", async () => await ConnectMimo(true));
        add(config.Taskbar ? "隐藏任务栏条" : "显示任务栏条", () => ToggleTaskbar());
        add(config.Display == "cycle" ? "切换为逐行列表" : "切换为轮换显示", () => ToggleDisplay());
        add(face.Light ? "切换 Dark" : "切换 Light", () => ToggleTheme());
        add(Topmost ? "取消置顶" : "保持置顶", () => Topmost = !Topmost);
        menu.Items.Add(new Separator());
        add("退出", () => Close());
        menu.PlacementTarget = face;
        menu.IsOpen = true;
    }

    void ToggleDisplay() {
        config.Display = config.Display == "cycle" ? "list" : "cycle";
        cycle = 0;
        RebuildRows();
        Layout();
        Dock();
        Save();
    }

    void ToggleTheme() {
        face.Light = !face.Light;
        face.InvalidateVisual();
        UpdateStrip();
        hover.IsOpen = false;
        Save();
    }

    // ---- settings -----------------------------------------------------

    void Settings() {
        hover.IsOpen = false;
        var window = new SettingsWindow(this, config, face.Light, Icon, () => {
            face.Limits = Limits.From(config);
            RebuildRows();
            Layout();
            Dock();
            EnsureStrip();
            if (config.Taskbar) {
                hover.IsOpen = false;
                Hide();
            } else {
                Show();
            }
            Save();
            var ignored = Refresh();
        }, async id => {
            if (id == "mimo") await ConnectMimo(true);
        });
        window.ShowDialog();
    }

    // ---- consoles -----------------------------------------------------

    async Task ConnectConsole(bool show) {
        try {
            config.ConsoleConnected = true;
            var row = GetRow("deepseek");
            if (row == null) {
                row = new Row { Id = "deepseek", Label = "DEEPSEEK" };
                rows["deepseek"] = row;
            }
            row.Meter = new Meter("DeepSeek", "待登录", "请在控制台窗口登录 DeepSeek");
            row.Usage = null;
            if (console == null) {
                console = new DeepSeekConsole(this,
                    snapshot => {
                        row.Meter = snapshot.Balance;
                        row.Usage = snapshot.Usage;
                        state = "控制台已同步 " + DateTime.Now.ToString("HH:mm");
                        face.InvalidateVisual();
                    },
                    reason => {
                        row.Usage = null;
                        if (reason.StartsWith("usage:") && (row.Meter.Value.StartsWith("¥") || row.Meter.Value.StartsWith("$")))
                            row.Meter.Detail += "\n用量查询失败：" + reason;
                        else
                            row.Meter = new Meter("DeepSeek", reason.StartsWith("auth:") ? "待登录" : "读取失败", reason);
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
            RebuildRows();
            Layout();
            Dock();
            Save();
        } catch {
            if (console != null) {
                console.Shutdown();
                console = null;
            }
            config.ConsoleConnected = false;
            var row = GetRow("deepseek");
            if (row != null) row.Meter = new Meter("DeepSeek", "未连接", "控制台组件未能启动。请确认已安装 Microsoft Edge WebView2 Runtime。");
            if (show) MessageBox.Show(this, "控制台组件未能启动。请确认已安装 Microsoft Edge WebView2 Runtime。", "控制台连接");
        }
        face.InvalidateVisual();
    }

    async Task ConnectMimo(bool show) {
        try {
            if (mimo == null) {
                mimo = new MiMoConsole(this,
                    meter => {
                        var row = GetRow("mimo");
                        if (row != null) row.Meter = meter;
                        state = "MiMo 已同步 " + DateTime.Now.ToString("HH:mm");
                        face.InvalidateVisual();
                    },
                    reason => {
                        var row = GetRow("mimo");
                        if (row != null) row.Meter = new Meter("MiMo", "待登录", reason);
                        face.InvalidateVisual();
                    });
                mimo.ShowActivated = show;
                mimo.Show();
                await mimo.Start();
                if (!show) mimo.Hide();
            } else {
                if (show) {
                    mimo.Show();
                    mimo.Activate();
                }
                await mimo.Fetch();
            }
            if (!preview) config.MimoConnected = true;
            RebuildRows();
            Layout();
            Dock();
            Save();
        } catch (Exception error) {
            MessageBox.Show(this, error.Message, "MiMo 控制台");
        }
        face.InvalidateVisual();
    }

    // ---- refresh ------------------------------------------------------

    async Task<Meter> FetchMimo() {
        if (mimo == null) throw new Exception("请在设置中连接 MiMo 控制台");
        await mimo.Fetch();
        var row = GetRow("mimo");
        if (row == null) throw new Exception("MiMo 未返回数据");
        return row.Meter;
    }

    async Task Refresh() {
        if (busy) return;
        busy = true;
        face.Busy = true;
        RebuildRows();
        Layout();
        face.InvalidateVisual();

        var jobs = new List<Task>();
        foreach (var info in Catalog.All) {
            if (!Catalog.Configured(info, config)) continue;
            jobs.Add(RefreshOne(info));
        }
        try {
            await Task.WhenAll(jobs);
        } catch {
            state = "刷新失败，请检查网络或 Key";
        }

        var deep = GetRow("deepseek");
        if (deep != null && !config.ConsoleConnected) {
            try { deep.Usage = TodayUsage.Load(config.UsageFile); } catch { deep.Usage = null; }
        }
        state = "更新于 " + DateTime.Now.ToString("HH:mm") + " · 每 5 分钟刷新";
        RebuildRows();
        Layout();
        Dock();
        busy = false;
        face.Busy = false;
        face.InvalidateVisual();
        if (hover.IsOpen) ShowHover(face.Hover);
    }

    async Task RefreshOne(ProviderInfo info) {
        Row row;
        if (!rows.TryGetValue(info.Id, out row)) return;
        // DeepSeek's console session replaces the API-key balance and adds today's usage.
        if (info.Id == "deepseek" && config.ConsoleConnected && console != null) {
            try { await console.Refresh(); } catch { }
            return;
        }
        try {
            row.Meter = await Fetch.Run(info, config, FetchMimo);
            TrackDaily(row);
        } catch (Exception error) {
            row.Meter = new Meter(info.Label, "读取失败", error.Message);
        }
        face.InvalidateVisual();
    }

    // Providers that only return a balance get an estimated daily spend from the balance drop.
    void TrackDaily(Row row) {
        if (preview || row.Meter == null || !row.Meter.Amount.HasValue) return;
        decimal spend = daily.Sample(row.Id, row.Meter.Amount.Value);
        if (spend <= 0) {
            row.Spend = null;
            row.SpendCost = null;
            return;
        }
        string currency = string.IsNullOrEmpty(row.Meter.Currency) ? "CNY" : row.Meter.Currency;
        string money = currency == "CNY" ? "¥" : currency == "USD" ? "$" : currency + " ";
        row.SpendCost = spend;
        row.Spend = "今日 ≈" + money + spend.ToString("0.00");
    }

    // ---- hover --------------------------------------------------------

    void ShowHover(int hit) {
        if (dragging || hit < 0) {
            hover.IsOpen = false;
            return;
        }
        Row row = null;
        bool isPrimary = false;
        if (hit == 1 && face.Primary != null) {
            row = face.Primary;
            isPrimary = true;
        } else if (hit >= 100) {
            int index = hit - 100;
            if (index < 0 || index >= face.Rows.Count) {
                hover.IsOpen = false;
                return;
            }
            row = face.Rows[index];
        } else if (hit == 3) {
            hover.IsOpen = false;
            hover.Child = Card("切换深色 / 浅色");
            Place(hit);
            return;
        } else if (hit == 4) {
            hover.IsOpen = false;
            hover.Child = Card("连接、刷新与退出");
            Place(hit);
            return;
        } else {
            hover.IsOpen = false;
            hover.Child = Card("拖动浮条 · 靠近上 / 左 / 右边缘吸附\n" + state);
            Place(hit);
            return;
        }
        hover.IsOpen = false;
        hover.Child = Card(Details(row, isPrimary));
        Place(hit);
    }

    void Place(int hit) {
        hover.PlacementTarget = face;
        hover.Placement = face.Horizontal ? PlacementMode.Bottom : face.Edge == "right" ? PlacementMode.Left : PlacementMode.Right;
        hover.HorizontalOffset = 8;
        double offset = 35 * UiScale;
        if (hit >= 100) offset = (face.Primary != null ? 142 : 0) * UiScale + (hit - 100) * 56 * UiScale + 8;
        hover.VerticalOffset = face.Horizontal ? 8 : offset;
        hover.IsOpen = true;
    }

    Border Card(string text) {
        return new Border {
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
    }

    string Details(Row row, bool isPrimary) {
        var lines = new List<string>();
        lines.Add(row.Label + (isPrimary ? " · 主额度" : ""));
        lines.Add("");
        lines.Add(row.Meter.Detail);
        if (row.Usage != null) {
            lines.Add("");
            lines.Add(BarFace.SpendText(row));
            lines.Add("Token " + (row.Usage.Tokens.HasValue ? row.Usage.Tokens.Value.ToString("N0") : "暂无数据"));
            if (!string.IsNullOrEmpty(row.Usage.Source)) lines.Add(row.Usage.Source);
        } else if (row.Spend != null) {
            lines.Add("");
            lines.Add(row.Spend + "（按当日余额差估算）");
        }
        lines.Add("");
        lines.Add(state);
        return string.Join("\n", lines);
    }

    // ---- persistence --------------------------------------------------

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

    static void Check(bool ok) { if (!ok) throw new Exception("Check failed"); }

    static void Preview() {
        var face = new BarFace();
        face.Primary = new Row { Id = "glm", Label = "ZHIPU", Meter = new Meter("GLM", "", "") { FiveHour = 82, Weekly = 43 } };
        face.Rows.Add(new Row {
            Id = "deepseek", Label = "DEEPSEEK",
            Meter = new Meter("DeepSeek", "¥28.60", ""),
            Usage = new TodayUsage { Date = DateTime.Now.ToString("yyyy-MM-dd"), Cost = .86m, Tokens = 124800, Currency = "CNY", Source = "示例数据" }
        });
        face.Rows.Add(new Row { Id = "kimi", Label = "KIMI", Meter = new Meter("Kimi", "¥49.59", "") });
        face.Rows.Add(new Row { Id = "minimax", Label = "MINIMAX", Meter = new Meter("MiniMax", "5h 76%", "") { FiveHour = 76, Weekly = 61 } });
        face.Rows.Add(new Row { Id = "mimo", Label = "MIMO", Meter = new Meter("MiMo", "月 91%", "") { Weekly = 91 } });
        face.Width = 132;
        face.Height = face.ContentHeight;
        face.Measure(new Size(132, face.ContentHeight));
        face.Arrange(new Rect(0, 0, 132, face.ContentHeight));

        var visual = new DrawingVisual();
        using (var d = visual.RenderOpen()) {
            d.DrawRectangle(new LinearGradientBrush(Paint.C("#26364C"), Paint.C("#91A1B9"), 30), null, new Rect(0, 0, 420, 700));
            Paint.Text(d, "供应商列表 / 主额度环", 18, Brushes.White, new Point(24, 16), true);
            Paint.Text(d, "GLM 主环 + DeepSeek / Kimi / MiniMax / MiMo 逐行", 11, Brushes.White, new Point(24, 44));

            double[] x = { 30, 220 };
            for (int i = 0; i < 2; i++) {
                face.Light = i == 1;
                d.PushTransform(new TranslateTransform(x[i], 70));
                d.PushTransform(new ScaleTransform(UiScale, UiScale));
                face.Render(d);
                d.Pop();
                d.Pop();
            }
            double bottom = 70 + face.ContentHeight * UiScale + 14;
            Paint.Text(d, "深色", 11, Brushes.White, new Point(60, bottom), true);
            Paint.Text(d, "浅色", 11, Brushes.White, new Point(250, bottom), true);

            var horizontal = new BarFace { Horizontal = true, Light = false, Primary = face.Primary };
            horizontal.Rows.Add(face.Rows[0]);
            horizontal.Rows.Add(face.Rows[1]);
            horizontal.Width = horizontal.ContentWidth;
            horizontal.Height = 118;
            horizontal.Measure(new Size(horizontal.Width, 118));
            horizontal.Arrange(new Rect(0, 0, horizontal.Width, 118));
            Paint.Text(d, "顶部横向：吸附到屏幕上边缘时", 11, Brushes.White, new Point(24, bottom + 34));
            d.PushTransform(new TranslateTransform(24, bottom + 52));
            d.PushTransform(new ScaleTransform(UiScale, UiScale));
            horizontal.Render(d);
            d.Pop();
            d.Pop();

            // Taskbar strip: every provider flat in one row.
            var flat = new TaskbarFace { Light = false };
            flat.Rows.Add(face.Primary);
            foreach (var row in face.Rows) flat.Rows.Add(row);
            flat.Width = flat.ContentWidth;
            flat.Height = flat.ContentHeight;
            flat.Measure(new Size(flat.Width, flat.Height));
            flat.Arrange(new Rect(0, 0, flat.Width, flat.Height));
            Paint.Text(d, "任务栏模式：贴在任务栏上方，所有用量平铺一行", 11, Brushes.White, new Point(12, bottom + 150));
            d.PushTransform(new TranslateTransform(12, bottom + 168));
            d.PushTransform(new ScaleTransform(UiScale, UiScale));
            flat.Render(d);
            d.Pop();
            d.Pop();
        }
        var bitmap = new RenderTargetBitmap(420, 700, 96, 96, PixelFormats.Pbgra32);
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
                    "PASS: vector icons, dark/light render, nested rings, top/left docking geometry, continuous colors, configurable thresholds, provider rows and parsers, taskbar strip. Live transparency and pointer animation require desktop verification.");
            } catch (Exception e) {
                File.WriteAllText(Path.Combine(Root, "glass-test.txt"), e.ToString());
                Environment.ExitCode = 1;
            }
            return;
        }

        // A widget should log a bad frame instead of vanishing; crash.txt keeps the stack.
        app.DispatcherUnhandledException += (s, e) => {
            LogCrash(e.Exception);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) => LogCrash(e.ExceptionObject as Exception);

        bool created;
        using (var mutex = new System.Threading.Mutex(true, "Local\\TokenBar_Desktop_v1", out created)) {
            if (!created) {
                MessageBox.Show("请先退出正在运行的旧版 Token Bar。");
                return;
            }
            app.Run(new GlassBar());
        }
    }

    static void LogCrash(Exception error) {
        try {
            string path = Path.Combine(Root, "crash.txt");
            if (File.Exists(path) && new FileInfo(path).Length > 64000) File.Delete(path);
            File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + (error == null ? "unknown error" : error.ToString()) + Environment.NewLine);
        } catch { }
    }
}
