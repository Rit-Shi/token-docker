using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

// Flat layout for the taskbar strip: every provider side by side in one row, plus the same
// theme and menu buttons the floating bar has.
class TaskbarFace : FrameworkElement {
    static readonly Geometry Zhipu = Paint.Icon("zhipu"), DeepSeek = Paint.Icon("deepseek");

    public const double TileWidth = 104;
    public const double TileHeight = 38;
    public const double ControlsWidth = 46;

    // Hits beyond the provider tiles.
    public const int HitTheme = -2, HitMenu = -3;

    public bool Light;
    public Limits Limits = new Limits();
    public List<Row> Rows = new List<Row>();
    public int Hover = -1;

    public Brush Ink { get { return Paint.B(Light ? "#20314A" : "#F0F5FF"); } }
    public Brush Muted { get { return Paint.B(Light ? "#5E718A" : "#9CADC7"); } }
    Brush Divider { get { return Paint.B(Light ? "#38304661" : "#405D7696"); } }

    public double ContentWidth { get { return 10 + Rows.Count * TileWidth + ControlsWidth; } }
    public double ContentHeight { get { return TileHeight; } }

    // Provider tile index, -1 for the background, HitTheme / HitMenu for the buttons.
    public int Hit(Point p) {
        double controls = ContentWidth - ControlsWidth;
        if (p.X >= controls) return p.X < controls + 26 ? HitTheme : HitMenu;
        if (p.X < 10) return -1;
        int index = (int)((p.X - 10) / TileWidth);
        return index >= 0 && index < Rows.Count ? index : -1;
    }

    bool HasWindows(Row row) {
        return row.Meter.FiveHour.HasValue || row.Meter.Weekly.HasValue;
    }

    // Plan providers colour their value by the quota ramp so a glance is enough.
    Brush ValueInk(Row row) {
        if (!HasWindows(row)) return Ink;
        return new SolidColorBrush(Paint.Quota(row.Meter.FiveHour, Limits.QuotaWarn));
    }

    double ValueSize(Row row) {
        if (HasWindows(row)) return 13;
        string text = row.Meter.Compact;
        return text != null && text.Length <= 7 ? 14 : 12;
    }

    void Glyph(DrawingContext d, string id, Rect r, Brush ink) {
        if (id == "glm") { Paint.Logo(d, Zhipu, ink, r); return; }
        if (id == "deepseek") { Paint.Logo(d, DeepSeek, Paint.B(Light ? "#466AFF" : "#91ADFF"), r); return; }
        if (id == "kimi" || id == "minimax") {
            if (Paint.Svg(d, Light ? id + "-color" : id, r, ink)) return;
        }
        Paint.Centered(d, id == "kimi" ? "K" : "M", r.Height * .68, ink, r);
    }

    // Same sun/moon as the floating bar footer.
    void ThemeGlyph(DrawingContext d, Point center) {
        if (Light) {
            d.DrawEllipse(null, new Pen(Muted, 1.3), center, 3.5, 3.5);
            for (int i = 0; i < 8; i++) {
                double angle = i * Math.PI / 4;
                d.DrawLine(new Pen(Muted, 1),
                    new Point(center.X + 6 * Math.Cos(angle), center.Y + 6 * Math.Sin(angle)),
                    new Point(center.X + 8 * Math.Cos(angle), center.Y + 8 * Math.Sin(angle)));
            }
        } else {
            d.DrawEllipse(Muted, null, center, 6, 6);
            d.DrawEllipse(Paint.B(Light ? "#F7FBFF" : "#243247"), null, new Point(center.X + 2.5, center.Y - 2.5), 5, 5);
        }
    }

    void ButtonBack(DrawingContext d, Rect area) {
        d.DrawRoundedRectangle(Paint.B(Light ? "#14203049" : "#22FFFFFF"), null, area, 8, 8);
    }

    protected override void OnRender(DrawingContext d) {
        base.OnRender(d);
        Render(d);
    }

    public void Render(DrawingContext d) {
        double width = ContentWidth, height = ContentHeight;
        var shell = new Rect(0, 0, width, height);

        d.DrawRoundedRectangle(Paint.B(Light ? "#E6F7FBFF" : "#E6202D40"), new Pen(Paint.B(Light ? "#C8FFFFFF" : "#707F96B3"), 1), shell, 14, 14);

        for (int i = 0; i < Rows.Count; i++) {
            var row = Rows[i];
            double x = 10 + i * TileWidth;
            if (i > 0) d.DrawLine(new Pen(Divider, 1), new Point(x - 6, 8), new Point(x - 6, height - 8));
            if (i == Hover) d.DrawRoundedRectangle(Paint.B(Light ? "#14203049" : "#22FFFFFF"), null, new Rect(x - 4, 4, TileWidth - 2, height - 8), 10, 10);

            Glyph(d, row.Id, new Rect(x, 9, 20, 20), Ink);
            Paint.Text(d, row.Label, 7, Muted, new Point(x + 26, 8), true, 70);
            Paint.Text(d, row.Meter.Compact, ValueSize(row), ValueInk(row), new Point(x + 26, 18), true, 72);
        }

        // Buttons, mirrored from the floating bar's footer.
        double controls = width - ControlsWidth;
        d.DrawLine(new Pen(Divider, 1), new Point(controls, 8), new Point(controls, height - 8));
        var themeArea = new Rect(controls + 3, 6, 20, height - 12);
        var menuArea = new Rect(controls + 25, 6, 18, height - 12);
        if (Hover == HitTheme) ButtonBack(d, themeArea);
        if (Hover == HitMenu) ButtonBack(d, menuArea);
        ThemeGlyph(d, new Point(themeArea.X + themeArea.Width / 2, height / 2));
        for (int i = 0; i < 3; i++) d.DrawEllipse(Muted, null, new Point(menuArea.X + menuArea.Width / 2, height / 2 - 5 + i * 5), 1.4, 1.4);
    }
}
