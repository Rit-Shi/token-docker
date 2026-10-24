using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// Connection settings: providers, display mode and colour thresholds.
class SettingsWindow : Window {
    readonly Config config;
    readonly bool light;
    readonly Action commit;
    readonly Action<string> connect;
    readonly List<Action> apply = new List<Action>();
    readonly HashSet<string> hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    ComboBox display, primary;
    readonly List<string> primaryIds = new List<string>();

    public SettingsWindow(Window owner, Config config, bool light, ImageSource icon, Action commit, Action<string> connect) {
        this.config = config;
        this.light = light;
        this.commit = commit;
        this.connect = connect;
        foreach (var id in (config.Hidden ?? "").Split(',')) if (id.Trim() != "") hidden.Add(id.Trim());

        Owner = owner;
        ShowInTaskbar = false;
        Icon = icon;
        Title = "Token Bar · 连接";
        Width = 470;
        Height = 680;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Topmost = true;
        Background = Paint.B(light ? "#F4F7FC" : "#202C3D");

        Build();
    }

    Brush Ink { get { return Paint.B(light ? "#20314A" : "#F0F5FF"); } }
    Brush Muted { get { return Paint.B(light ? "#5E718A" : "#9CADC7"); } }

    void Build() {
        var panel = new StackPanel { Margin = new Thickness(22) };
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        Header(panel, "供应商");
        Hint(panel, "填入 Key 后就会显示在浮条上；取消勾选只是隐藏，不会删除 Key。");
        foreach (var info in Catalog.All) Provider(panel, info);

        Header(panel, "展示");
        Hint(panel, "逐行列表会随启用的供应商变高；轮换只显示一个，点击该行切换。");
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        display = new ComboBox { Width = 110, Margin = new Thickness(0, 0, 12, 0) };
        display.Items.Add("逐行列表");
        display.Items.Add("轮换");
        display.SelectedIndex = config.Display == "cycle" ? 1 : 0;
        row.Children.Add(display);

        primary = new ComboBox { Width = 120 };
        foreach (var info in Catalog.All) {
            if (!info.Plan) continue;
            primary.Items.Add(info.Label);
            primaryIds.Add(info.Id);
            if (info.Id == (config.Primary ?? "glm")) primary.SelectedIndex = primaryIds.Count - 1;
        }
        if (primary.SelectedIndex < 0 && primaryIds.Count > 0) primary.SelectedIndex = 0;
        row.Children.Add(new TextBlock { Text = "主额度", Foreground = Muted, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(primary);
        panel.Children.Add(row);

        Header(panel, "配色阈值");
        Hint(panel, "额度低于警戒线时圆环由绿转黄；今日花费达到预警转黄、达到告警转红，超过告警为深红。");

        var limits = Limits.From(config);
        quotaWarn = new TextBox { Text = limits.QuotaWarn.ToString("0.##", CultureInfo.InvariantCulture), Padding = new Thickness(6) };
        Label(panel, "额度警戒线（剩余 %，默认 50）");
        panel.Children.Add(quotaWarn);

        Label(panel, "今日花费预警 / 告警（元，默认 2.5 / 5）");
        var costRow = new Grid();
        costRow.ColumnDefinitions.Add(new ColumnDefinition());
        costRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        costRow.ColumnDefinitions.Add(new ColumnDefinition());
        costWarn = new TextBox { Text = limits.CostWarn.ToString("0.##", CultureInfo.InvariantCulture), Padding = new Thickness(6) };
        costDanger = new TextBox { Text = limits.CostDanger.ToString("0.##", CultureInfo.InvariantCulture), Padding = new Thickness(6) };
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

        var save = new Button { Content = "保存并刷新", Margin = new Thickness(0, 18, 0, 0), Padding = new Thickness(8) };
        save.Click += (s, e) => Commit();
        panel.Children.Add(save);
    }

    TextBox quotaWarn, costWarn, costDanger;

    void Header(StackPanel panel, string text) {
        panel.Children.Add(new TextBlock {
            Text = text,
            Foreground = Ink,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 16, 0, 2)
        });
    }

    void Hint(StackPanel panel, string text) {
        panel.Children.Add(new TextBlock {
            Text = text,
            Foreground = Muted,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4)
        });
    }

    void Label(StackPanel panel, string text) {
        panel.Children.Add(new TextBlock { Text = text, Foreground = Ink, Margin = new Thickness(0, 8, 0, 5) });
    }

    void Provider(StackPanel panel, ProviderInfo info) {
        var show = new CheckBox {
            Content = info.Label,
            IsChecked = !hidden.Contains(info.Id),
            Foreground = Ink,
            Margin = new Thickness(0, 10, 0, 0)
        };
        panel.Children.Add(show);
        apply.Add(() => {
            if (show.IsChecked == true) hidden.Remove(info.Id);
            else hidden.Add(info.Id);
        });

        if (info.Console) {
            var button = new Button {
                Content = config.MimoConnected ? "重新登录 / 刷新 MiMo 控制台" : "连接 MiMo 控制台",
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 6, 0, 0),
                Padding = new Thickness(10, 3, 10, 3)
            };
            button.Click += (s, e) => connect(info.Id);
            panel.Children.Add(button);
            Hint(panel, "MiMo 只提供网页会话接口，需要在这个窗口里登录小米账号。");
            return;
        }

        var key = new PasswordBox {
            Password = Catalog.Key(info, config),
            Padding = new Thickness(6),
            Margin = new Thickness(0, 6, 0, 0)
        };
        var copy = new Button {
            Content = "复制",
            Margin = new Thickness(8, 6, 0, 0),
            Padding = new Thickness(12, 3, 12, 3),
            VerticalAlignment = VerticalAlignment.Top,
            IsEnabled = key.Password.Length > 0,
            ToolTip = "复制这行 Key，方便粘贴到别处"
        };
        copy.Click += (s, e) => CopyKey(key.Password, copy);
        key.PasswordChanged += (s, e) => copy.IsEnabled = key.Password.Length > 0;

        var keyRow = new Grid();
        keyRow.ColumnDefinitions.Add(new ColumnDefinition());
        keyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(key, 0);
        Grid.SetColumn(copy, 1);
        keyRow.Children.Add(key);
        keyRow.Children.Add(copy);
        panel.Children.Add(keyRow);
        apply.Add(() => {
            string value = key.Password.Trim();
            switch (info.Id) {
                case "glm": config.Glm = value; break;
                case "deepseek": config.DeepSeek = value; break;
                case "kimi": config.Kimi = value; break;
                case "minimax": config.MiniMax = value; break;
            }
        });

        var extra = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        CheckBox international = null;
        if (info.Id == "glm" || info.Id == "kimi" || info.Id == "minimax") {
            international = new CheckBox {
                Content = "国际站",
                Foreground = Ink,
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = info.Id == "glm" ? config.International : info.Id == "kimi" ? config.KimiInternational : config.MiniMaxInternational
            };
            extra.Children.Add(international);
            apply.Add(() => {
                bool value = international.IsChecked == true;
                if (info.Id == "glm") config.International = value;
                else if (info.Id == "kimi") config.KimiInternational = value;
                else config.MiniMaxInternational = value;
            });
        }

        if (info.Plan && info.Balance) {
            var mode = new ComboBox { Width = 96, Margin = new Thickness(14, 0, 0, 0) };
            mode.Items.Add("自动");
            mode.Items.Add("余额");
            mode.Items.Add("套餐");
            string current = info.Id == "kimi" ? config.KimiMode : config.MiniMaxMode;
            mode.SelectedIndex = current == "balance" ? 1 : current == "plan" ? 2 : 0;
            extra.Children.Add(mode);
            apply.Add(() => {
                string value = mode.SelectedIndex == 1 ? "balance" : mode.SelectedIndex == 2 ? "plan" : "auto";
                if (info.Id == "kimi") config.KimiMode = value;
                else config.MiniMaxMode = value;
            });
        }
        if (extra.Children.Count > 0) panel.Children.Add(extra);
    }

    // Copies the key in the box (including unsaved edits) and confirms on the button itself,
    // so no dialog interrupts the flow.
    static async void CopyKey(string value, Button button) {
        string text = (value ?? "").Trim();
        if (text.Length == 0) return;
        bool copied = false;
        for (int attempt = 0; attempt < 3 && !copied; attempt++) {
            copied = TrySetClipboard(text);
            if (!copied) await Task.Delay(80);
        }
        if (!copied) return;
        button.Content = "已复制";
        await Task.Delay(1200);
        button.Content = "复制";
    }

    // Another process can hold the clipboard for a moment; retry instead of failing silently.
    static bool TrySetClipboard(string text) {
        try {
            Clipboard.SetText(text);
            return true;
        } catch {
            return false;
        }
    }

    void Commit() {
        Limits limits;
        string error;
        if (!Limits.TryParse(quotaWarn.Text, costWarn.Text, costDanger.Text, out limits, out error)) {
            MessageBox.Show(this, error, "配色阈值");
            return;
        }
        foreach (var action in apply) action();
        limits.StoreTo(config);
        config.Hidden = string.Join(",", new List<string>(hidden).ToArray());
        config.Display = display.SelectedIndex == 1 ? "cycle" : "list";
        if (primary.SelectedIndex >= 0 && primary.SelectedIndex < primaryIds.Count)
            config.Primary = primaryIds[primary.SelectedIndex];
        Close();
        if (commit != null) commit();
    }
}
