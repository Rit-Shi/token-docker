using System;
using System.Collections.Generic;
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

// One provider's state as shown in the bar.
class Row {
    public string Id, Label;
    public Meter Meter = new Meter("", "未配置", "请在设置中连接");
    public TodayUsage Usage;
}

// Draws the bar itself. Coordinates are logical units; GlassBar scales the whole visual.
class BarFace : FrameworkElement {
    static readonly Geometry Zhipu = Paint.Icon("zhipu"), DeepSeek = Paint.Icon("deepseek");

    const double TopHeight = 142;   // primary quota block
    const double RowHeight = 54;
    const double FooterHeight = 54;

    public bool Light, Horizontal, Busy;
    public Limits Limits = new Limits();
    public string Edge = "right";
    public double Pull;
    public int Hover = -1;
    public Row Primary;
    public List<Row> Rows = new List<Row>();

    public Brush Ink { get { return Paint.B(Light ? "#20314A" : "#F0F5FF"); } }
    public Brush Muted { get { return Paint.B(Light ? "#5E718A" : "#9CADC7"); } }
    Brush Track { get { return Paint.B(Light ? "#284A657C" : "#445F718B"); } }
    Brush Divider { get { return Paint.B(Light ? "#30466182" : "#405D7696"); } }

    public double ContentHeight {
        get { return (Primary != null ? TopHeight : 0) + Rows.Count * RowHeight + FooterHeight; }
    }

    public double ContentWidth {
        get { return Horizontal ? 118 + Rows.Count * 86 + 54 : 132; }
    }

    static bool IsToday(TodayUsage usage) {
        return usage != null && usage.Date == DateTime.Now.ToString("yyyy-MM-dd");
    }

    public static string SpendText(Row row) {
        if (!IsToday(row.Usage)) return "今日 —";
        if (row.Usage.DisplayCost != null) return "今日 " + row.Usage.DisplayCost;
        if (row.Usage.Cost.HasValue)
            return "今日 " + (row.Usage.Currency == "CNY" ? "¥" : "$") + row.Usage.Cost.Value.ToString("0.00");
        return "今日 —";
    }

    public Brush SpendInk(Row row) {
        decimal? cost = null;
        if (IsToday(row.Usage)) cost = row.Usage.CnyCost ?? (row.Usage.Currency == "CNY" ? row.Usage.Cost : null);
        return new SolidColorBrush(Paint.Expense(cost, Limits.CostWarn, Limits.CostDanger));
    }

    bool HasWindows(Row row) {
        return row.Meter.FiveHour.HasValue || row.Meter.Weekly.HasValue;
    }

    // Icon or a letter tile for providers without a bundled vector logo.
    void Glyph(DrawingContext d, string id, Rect r, Brush ink) {
        if (id == "glm") { Paint.Logo(d, Zhipu, ink, r); return; }
        if (id == "deepseek") { Paint.Logo(d, DeepSeek, Paint.B(Light ? "#466AFF" : "#91ADFF"), r); return; }
        string letter = id == "kimi" ? "K" : id == "minimax" ? "M" : "Mi";
        d.DrawRoundedRectangle(null, new Pen(Muted, 1.1), r, r.Width * .3, r.Width * .3);
        Paint.Centered(d, letter, r.Height * .52, ink, r);
    }

    public int Hit(Point p) {
        if (Horizontal) return HitHorizontal(p);
        double top = Primary != null ? TopHeight : 0;
        if (p.Y < top) return p.Y < 37 ? 0 : 1;
        int index = (int)((p.Y - top) / RowHeight);
        if (index >= 0 && index < Rows.Count && p.Y < Height - FooterHeight + 8) return 100 + index;
        if (p.Y >= Height - FooterHeight + 8) return p.X < 66 ? 3 : 4;
        return 0;
    }

    int HitHorizontal(Point p) {
        if (p.Y < 18) return 0;
        if (p.X < 118) return 1;
        if (p.X > Width - 46) return p.Y < 58 ? 3 : 4;
        int index = (int)((p.X - 118) / 86);
        return index < Rows.Count ? 100 + index : 0;
    }

    public Geometry Shell(double height) {
        double radius = 27, left = 10, top = 7, width = 112, bottom = height - 6;
        double right = left + width;
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
        double height = !double.IsNaN(Height) && Height > 0 ? Height : ContentHeight;
        var shell = Shell(height);

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

        if (Primary != null) DrawPrimary(d);
        for (int i = 0; i < Rows.Count; i++) DrawRow(d, i, Primary != null ? TopHeight : 0);
        DrawFooter(d, height);
        if (Busy) d.DrawEllipse(Paint.B("#74E0BD"), null, new Point(95, 25), 2.5, 2.5);
        DrawPull(d, height);
    }

    void DrawPrimary(DrawingContext d) {
        var meter = Primary.Meter;
        if (HasWindows(Primary)) {
            Paint.Arc(d, new Point(66, 77), 35, meter.FiveHour, Track, Limits.QuotaWarn);
            Paint.Arc(d, new Point(66, 77), 27, meter.Weekly, Track, Limits.QuotaWarn);
            Glyph(d, Primary.Id, new Rect(48, 59, 36, 36), Ink);
            Paint.Centered(d, Primary.Label, 9, Muted, new Rect(10, 114, 112, 12));
        } else {
            Glyph(d, Primary.Id, new Rect(44, 48, 44, 44), Ink);
            Paint.Centered(d, meter.Value, 16, Ink, new Rect(10, 100, 112, 22));
            Paint.Centered(d, Primary.Label, 9, Muted, new Rect(10, 122, 112, 12));
        }
        d.DrawLine(new Pen(Divider, 1), new Point(31, 135), new Point(101, 135));
    }

    void DrawRow(DrawingContext d, int index, double top) {
        var row = Rows[index];
        double y = top + index * RowHeight;
        if (index > 0) d.DrawLine(new Pen(Divider, 1), new Point(31, y), new Point(101, y));

        var center = new Point(28, y + RowHeight / 2 - 4);
        if (HasWindows(row)) {
            Paint.Arc(d, center, 16, row.Meter.FiveHour, Track, Limits.QuotaWarn);
            Paint.Arc(d, center, 13, row.Meter.Weekly, Track, Limits.QuotaWarn);
            Glyph(d, row.Id, new Rect(center.X - 9, center.Y - 9, 18, 18), Ink);
        } else {
            Glyph(d, row.Id, new Rect(center.X - 11, center.Y - 11, 22, 22), Ink);
        }

        Paint.Text(d, row.Label, 8, Muted, new Point(50, y + 7), true, 60);
        Paint.Text(d, row.Meter.Value, 12, Ink, new Point(50, y + 22), true, 66);
        if (row.Usage != null) Paint.Text(d, SpendText(row), 8, SpendInk(row), new Point(50, y + 40), false, 66);
    }

    void DrawFooter(DrawingContext d, double height) {
        d.DrawLine(new Pen(Divider, 1), new Point(31, height - FooterHeight + 8), new Point(101, height - FooterHeight + 8));
        double y = height - 23;

        // Theme switch uses vector sun/moon rather than font glyphs.
        if (Light) {
            d.DrawEllipse(null, new Pen(Muted, 1.4), new Point(45, y), 4, 4);
            for (int i = 0; i < 8; i++) {
                double angle = i * Math.PI / 4;
                d.DrawLine(new Pen(Muted, 1),
                    new Point(45 + 7 * Math.Cos(angle), y + 7 * Math.Sin(angle)),
                    new Point(45 + 9 * Math.Cos(angle), y + 9 * Math.Sin(angle)));
            }
        } else {
            d.DrawEllipse(Muted, null, new Point(45, y), 7, 7);
            d.DrawEllipse(Paint.B("#FF243247"), null, new Point(48, y - 3), 6, 6);
        }
        for (int i = 0; i < 3; i++) d.DrawEllipse(Muted, null, new Point(84, y + i * 6 - 6), 1.3, 1.3);
    }

    // The pull hint stretches as the bar approaches a screen edge.
    void DrawPull(DrawingContext d, double height) {
        if (Math.Abs(Pull) <= .05) return;
        if (Edge == "top") {
            double strength = Math.Min(1, Math.Abs(Pull) / 8);
            var pen = new Pen(Paint.B("#BCA0D4FF"), 1.5);
            var geometry = new StreamGeometry();
            using (var c = geometry.Open()) {
                c.BeginFigure(new Point(35, 7), false, false);
                c.BezierTo(new Point(49, 7 - 6 * strength), new Point(83, 7 - 6 * strength), new Point(97, 7), true, false);
            }
            d.DrawGeometry(null, pen, geometry);
            return;
        }
        double weight = Math.Min(1, Math.Abs(Pull) / 8);
        double start = height * .36, middle = height * .54, end = height * .72;
        var sidePen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(70 + 150 * weight), 140, 199, 255)), 1.4);
        var path = new StreamGeometry();
        using (var c = path.Open()) {
            if (Edge == "left") {
                c.BeginFigure(new Point(10, start), false, false);
                c.BezierTo(new Point(10 - 7 * weight, middle - 7), new Point(10 - 7 * weight, middle + 7), new Point(10, end), true, false);
            } else {
                c.BeginFigure(new Point(122, start), false, false);
                c.BezierTo(new Point(122 + 7 * weight, middle - 7), new Point(122 + 7 * weight, middle + 7), new Point(122, end), true, false);
            }
        }
        d.DrawGeometry(null, sidePen, path);
    }

    void RenderHorizontal(DrawingContext d) {
        double width = ContentWidth;
        var rect = new Rect(7, 7, width - 14, 104);
        var gradient = new LinearGradientBrush(
            Paint.C(Light ? "#DDF8FCFF" : "#DF253247"),
            Paint.C(Light ? "#BCE3EFFA" : "#BC111E30"), 65);
        d.DrawRoundedRectangle(gradient, new Pen(Paint.B(Light ? "#EFFFFFFF" : "#707F96B3"), 1), rect, 27, 27);

        if (Primary != null && HasWindows(Primary)) {
            Paint.Arc(d, new Point(64, 59), 35, Primary.Meter.FiveHour, Track, Limits.QuotaWarn);
            Paint.Arc(d, new Point(64, 59), 27, Primary.Meter.Weekly, Track, Limits.QuotaWarn);
            Glyph(d, Primary.Id, new Rect(47, 42, 34, 34), Ink);
        } else if (Primary != null) {
            Glyph(d, Primary.Id, new Rect(42, 38, 40, 40), Ink);
        }

        d.DrawLine(new Pen(Divider, 1), new Point(112, 30), new Point(112, 88));
        for (int i = 0; i < Rows.Count; i++) {
            var row = Rows[i];
            double x = 118 + i * 86;
            var center = new Point(x + 16, 32);
            if (HasWindows(row)) {
                Paint.Arc(d, center, 13, row.Meter.FiveHour, Track, Limits.QuotaWarn);
                Paint.Arc(d, center, 10, row.Meter.Weekly, Track, Limits.QuotaWarn);
                Glyph(d, row.Id, new Rect(center.X - 7, center.Y - 7, 14, 14), Ink);
            } else {
                Glyph(d, row.Id, new Rect(center.X - 9, center.Y - 9, 18, 18), Ink);
            }
            Paint.Text(d, row.Label, 8, Muted, new Point(x + 34, 22), true, 48);
            Paint.Text(d, row.Meter.Value, 11, Ink, new Point(x + 4, 48), true, 78);
            if (row.Usage != null) Paint.Text(d, SpendText(row), 8, SpendInk(row), new Point(x + 4, 68), false, 78);
        }

        d.DrawEllipse(null, new Pen(Muted, 1.3), new Point(width - 30, 39), 6, 6);
        for (int i = 0; i < 3; i++) d.DrawEllipse(Muted, null, new Point(width - 46 + i * 6, 77), 1.2, 1.2);
        if (Busy) d.DrawEllipse(Paint.B("#74E0BD"), null, new Point(width - 30, 20), 2.5, 2.5);
    }
}
