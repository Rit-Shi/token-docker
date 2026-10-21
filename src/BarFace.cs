using System;
using System.IO;
using System.Windows;
using System.Windows.Media;

class TodayUsage {
    public string Date, Currency, UpdatedAt, Scope = "today", Source, DisplayCost;
    public decimal? Cost, CnyCost;
    public long? Tokens;

    public static TodayUsage Load(string file) {
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file)) return null;
        var usage = Data.Json().Deserialize<TodayUsage>(File.ReadAllText(file));
        if (usage == null || usage.Date != DateTime.Now.ToString("yyyy-MM-dd") || usage.Scope != "today"
            || usage.Cost < 0 || usage.Tokens < 0 || (usage.Currency != "CNY" && usage.Currency != "USD")) return null;
        return usage;
    }
}

// Draws the bar itself. Coordinates are logical units; GlassBar scales the whole visual.
class BarFace : FrameworkElement {
    static readonly Geometry Zhipu = Paint.Icon("zhipu"), DeepSeek = Paint.Icon("deepseek");

    public bool Light, Horizontal;
    public Meter Plan = new Meter("GLM", "未配置", "请在设置中连接 GLM 套餐");
    public Meter Balance = new Meter("DeepSeek", "未配置", "请在设置中连接 DeepSeek");
    public TodayUsage Usage;
    public string Edge = "right";
    public double Pull;
    public int Hover = -1;
    public bool Busy;

    public Brush Ink { get { return Paint.B(Light ? "#20314A" : "#F0F5FF"); } }
    public Brush Muted { get { return Paint.B(Light ? "#5E718A" : "#9CADC7"); } }

    bool Today { get { return Usage != null && Usage.Date == DateTime.Now.ToString("yyyy-MM-dd"); } }

    public string Spend {
        get {
            if (!Today) return "—";
            if (Usage.DisplayCost != null) return Usage.DisplayCost;
            if (Usage.Cost.HasValue) return (Usage.Currency == "CNY" ? "¥" : "$") + Usage.Cost.Value.ToString("0.00");
            return "—";
        }
    }

    public Brush SpendInk {
        get {
            decimal? cost = Today ? (Usage.CnyCost ?? (Usage.Currency == "CNY" ? Usage.Cost : null)) : null;
            return new SolidColorBrush(Paint.Expense(cost));
        }
    }

    public int Hit(Point p) {
        if (Horizontal) {
            if (p.Y < 18) return 0;
            if (p.X < 115) return 1;
            if (p.X < 282) return 2;
            return p.Y < 58 ? 3 : 4;
        }
        if (p.Y < 37) return 0;
        if (p.Y < 135) return 1;
        if (p.Y < 260) return 2;
        if (p.Y < 303) return p.X < 66 ? 3 : 4;
        return 0;
    }

    public Geometry Shell() {
        double radius = 27, left = 10, top = 7, width = 112, height = 293;
        double right = left + width, bottom = top + height;
        var geometry = new StreamGeometry();
        using (var c = geometry.Open()) {
            c.BeginFigure(new Point(left + radius, top), true, true);
            c.LineTo(new Point(right - radius, top), true, false);
            c.ArcTo(new Point(right, top + radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
            c.LineTo(new Point(right, bottom - radius), true, false);
            c.ArcTo(new Point(right - radius, bottom), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
            c.LineTo(new Point(left + radius, bottom), true, false);
            c.ArcTo(new Point(left, bottom - radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
            c.LineTo(new Point(left, top + radius), true, false);
            c.ArcTo(new Point(left + radius, top), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
        }
        return geometry;
    }

    protected override void OnRender(DrawingContext d) {
        base.OnRender(d);
        Render(d);
    }

    public void Render(DrawingContext d) {
        if (Horizontal) {
            RenderHorizontal(d);
            return;
        }
        var shell = Shell();
        var glass = new LinearGradientBrush();
        glass.StartPoint = new Point(0, 0);
        glass.EndPoint = new Point(1, 1);
        glass.GradientStops.Add(new GradientStop(Paint.C(Light ? "#DDF8FCFF" : "#DF253247"), 0));
        glass.GradientStops.Add(new GradientStop(Paint.C(Light ? "#BCE3EFFA" : "#BC111E30"), .65));
        glass.GradientStops.Add(new GradientStop(Paint.C(Light ? "#DCF6F8FF" : "#DE27354A"), 1));

        d.PushTransform(new TranslateTransform(0, 3));
        d.DrawGeometry(Paint.B("#25000000"), null, shell);
        d.Pop();
        d.DrawGeometry(glass, new Pen(Paint.B(Light ? "#EFFFFFFF" : "#707F96B3"), 1), shell);

        d.PushClip(shell);
        d.DrawEllipse(Paint.B(Light ? "#38FFFFFF" : "#117FC6FF"), null, new Point(36, 10), 88, 70);
        d.Pop();

        var grip = new Pen(Muted, 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        d.DrawLine(grip, new Point(55, 23), new Point(77, 23));

        Brush track = Paint.B(Light ? "#284A657C" : "#445F718B");
        Paint.Arc(d, new Point(66, 77), 35, Plan.FiveHour, track);
        Paint.Arc(d, new Point(66, 77), 27, Plan.Weekly, track);
        Paint.Logo(d, Zhipu, Ink, new Rect(49, 60, 34, 34));

        Paint.Text(d, "ZHIPU", 9, Muted, new Point(48, 115), true);
        d.DrawLine(new Pen(Paint.B(Light ? "#30466182" : "#405D7696"), 1), new Point(31, 135), new Point(101, 135));

        Paint.Logo(d, DeepSeek, Paint.B(Light ? "#466AFF" : "#91ADFF"), new Rect(46, 148, 40, 32));
        Paint.Text(d, "余额", 9, Muted, new Point(26, 195));
        Paint.Text(d, Balance.Value, Balance.Value.Length > 9 ? 10 : 14, Ink, new Point(52, 189), true, 65);
        Paint.Text(d, "今日", 9, Muted, new Point(26, 222));
        Paint.Text(d, Spend, 12, SpendInk, new Point(53, 216), true, 65);
        if (Usage == null) Paint.Text(d, "用量待接入", 8, Muted, new Point(43, 241), false, 70);

        d.DrawLine(new Pen(Paint.B(Light ? "#30466182" : "#405D7696"), 1), new Point(31, 260), new Point(101, 260));
        DrawThemeGlyph(d);
        for (int i = 0; i < 3; i++) d.DrawEllipse(Muted, null, new Point(84, 277 + i * 6), 1.3, 1.3);
        if (Busy) d.DrawEllipse(Paint.B("#74E0BD"), null, new Point(95, 25), 2.5, 2.5);
        DrawPull(d);
    }

    // Theme switch uses vector sun/moon rather than font glyphs.
    void DrawThemeGlyph(DrawingContext d) {
        if (Light) {
            d.DrawEllipse(null, new Pen(Muted, 1.4), new Point(45, 283), 4, 4);
            for (int i = 0; i < 8; i++) {
                double angle = i * Math.PI / 4;
                d.DrawLine(new Pen(Muted, 1),
                    new Point(45 + 7 * Math.Cos(angle), 283 + 7 * Math.Sin(angle)),
                    new Point(45 + 9 * Math.Cos(angle), 283 + 9 * Math.Sin(angle)));
            }
        } else {
            d.DrawEllipse(Muted, null, new Point(45, 283), 7, 7);
            d.DrawEllipse(Paint.B("#FF243247"), null, new Point(48, 280), 6, 6);
        }
    }

    // The pull hint stretches as the bar approaches a screen edge.
    void DrawPull(DrawingContext d) {
        if (Math.Abs(Pull) <= .05) return;
        double strength = Math.Min(1, Math.Abs(Pull) / 8);
        var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(70 + 150 * strength), 140, 199, 255)), 1.4);
        var geometry = new StreamGeometry();
        using (var c = geometry.Open()) {
            if (Edge == "left") {
                c.BeginFigure(new Point(10, 115), false, false);
                c.BezierTo(new Point(10 - 7 * strength, 139), new Point(10 - 7 * strength, 195), new Point(10, 222), true, false);
            } else if (Edge == "right") {
                c.BeginFigure(new Point(122, 115), false, false);
                c.BezierTo(new Point(122 + 7 * strength, 139), new Point(122 + 7 * strength, 195), new Point(122, 222), true, false);
            } else {
                c.BeginFigure(new Point(35, 7), false, false);
                c.BezierTo(new Point(49, 7 - 6 * strength), new Point(83, 7 - 6 * strength), new Point(97, 7), true, false);
            }
        }
        d.DrawGeometry(null, pen, geometry);
    }

    void RenderHorizontal(DrawingContext d) {
        var rect = new Rect(7, 7, 330, 104);
        var gradient = new LinearGradientBrush(
            Paint.C(Light ? "#DDF8FCFF" : "#DF253247"),
            Paint.C(Light ? "#BCE3EFFA" : "#BC111E30"), 65);
        d.DrawRoundedRectangle(gradient, new Pen(Paint.B(Light ? "#EFFFFFFF" : "#707F96B3"), 1), rect, 27, 27);

        Brush track = Paint.B(Light ? "#284A657C" : "#445F718B");
        Paint.Arc(d, new Point(64, 59), 35, Plan.FiveHour, track);
        Paint.Arc(d, new Point(64, 59), 27, Plan.Weekly, track);
        Paint.Logo(d, Zhipu, Ink, new Rect(47, 42, 34, 34));

        d.DrawLine(new Pen(Paint.B(Light ? "#30466182" : "#405D7696"), 1), new Point(113, 30), new Point(113, 88));
        Paint.Logo(d, DeepSeek, Paint.B(Light ? "#466AFF" : "#91ADFF"), new Rect(126, 40, 38, 34));
        Paint.Text(d, "余额", 10, Muted, new Point(174, 35));
        Paint.Text(d, Balance.Value, 14, Ink, new Point(209, 30), true, 78);
        Paint.Text(d, "今日", 10, Muted, new Point(174, 63));
        Paint.Text(d, Spend, 13, SpendInk, new Point(209, 59), true, 78);

        d.DrawEllipse(null, new Pen(Muted, 1.3), new Point(310, 39), 6, 6);
        for (int i = 0; i < 3; i++) d.DrawEllipse(Muted, null, new Point(304 + i * 6, 77), 1.2, 1.2);
        DrawHorizontalPull(d);
    }

    void DrawHorizontalPull(DrawingContext d) {
        if (Math.Abs(Pull) <= .05) return;
        var pen = new Pen(Paint.B("#BCA0D4FF"), 1.5);
        var geometry = new StreamGeometry();
        using (var c = geometry.Open()) {
            c.BeginFigure(new Point(112, 7), false, false);
            c.BezierTo(new Point(142, 7 - Math.Abs(Pull)), new Point(202, 7 - Math.Abs(Pull)), new Point(232, 7), true, false);
        }
        d.DrawGeometry(null, pen, geometry);
    }
}
