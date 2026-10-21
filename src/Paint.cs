using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Xml;

// Shared drawing helpers: palette, colour ramps, text and vector icons.
static class Paint {
    static readonly Color Green = C("#65DDB0"), Amber = C("#F3C469"), Red = C("#F27179");
    static readonly Color Danger = C("#BB304B"), Unknown = C("#7F8B9E");

    public static Color C(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }
    public static Brush B(string hex) { return new SolidColorBrush(C(hex)); }

    public static Color Mix(Color a, Color b, double t) {
        t = Math.Max(0, Math.Min(1, t));
        return Color.FromRgb(
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
    }

    // Quota ramp: green at 100% remaining, yellow at the warning line, red at 0%.
    public static Color Quota(double? remaining, double warn = 50) {
        if (!remaining.HasValue) return Unknown;
        warn = Math.Max(1, Math.Min(100, warn));
        return remaining <= warn
            ? Mix(Red, Amber, remaining.Value / warn)
            : Mix(Amber, Green, (remaining.Value - warn) / (100 - warn));
    }

    // Spend ramp: green at 0, yellow at the warning amount, red at the danger amount.
    public static Color Expense(decimal? cost, double warn = 2.5, double danger = 5) {
        if (!cost.HasValue) return Unknown;
        if (danger <= warn) danger = warn + 1;
        if (cost.Value > (decimal)danger) return Danger;
        double amount = Math.Max(0, (double)cost.Value);
        return amount <= warn
            ? Mix(Green, Amber, amount / Math.Max(warn, .0001))
            : Mix(Amber, Red, (amount - warn) / (danger - warn));
    }

    public static void Text(DrawingContext d, string text, double size, Brush ink, Point at, bool bold = false, double max = 300) {
        var typeface = new Typeface(new FontFamily("Segoe UI, Microsoft YaHei UI"), FontStyles.Normal,
            bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal);
        // The obsolete overload keeps the legacy 1.0 pixel-per-DIP metrics; the bar is scaled by a
        // Viewbox, so switching to a DPI-aware overload would move glyph hinting for existing users.
#pragma warning disable 0618
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, size, ink);
#pragma warning restore 0618
        formatted.MaxTextWidth = max;
        formatted.Trimming = TextTrimming.CharacterEllipsis;
        d.DrawText(formatted, at);
    }

    public static Geometry Icon(string name) {
        using (var stream = typeof(Paint).Assembly.GetManifestResourceStream(name + ".svg")) {
            var xml = new XmlDocument { XmlResolver = null };
            xml.Load(stream);
            var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
            foreach (XmlNode path in xml.GetElementsByTagName("path"))
                group.Children.Add(Geometry.Parse(path.Attributes["d"].Value));
            group.Freeze();
            return group;
        }
    }

    public static void Logo(DrawingContext d, Geometry shape, Brush brush, Rect r) {
        d.PushTransform(new TranslateTransform(r.X, r.Y));
        d.PushTransform(new ScaleTransform(r.Width / 24, r.Height / 24));
        d.DrawGeometry(brush, null, shape);
        d.Pop();
        d.Pop();
    }

    public static void Arc(DrawingContext d, Point center, double radius, double? value, Brush track, double warn = 50) {
        d.DrawEllipse(null, new Pen(track, 5), center, radius, radius);
        if (!value.HasValue || value <= 0) return;
        var pen = new Pen(new SolidColorBrush(Quota(value, warn)), 5) {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        if (value >= 100) {
            d.DrawEllipse(null, pen, center, radius, radius);
            return;
        }
        double angle = value.Value * 2 * Math.PI / 100;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open()) {
            context.BeginFigure(new Point(center.X, center.Y - radius), false, false);
            context.ArcTo(
                new Point(center.X + radius * Math.Sin(angle), center.Y - radius * Math.Cos(angle)),
                new Size(radius, radius), 0, value > 50, SweepDirection.Clockwise, true, false);
        }
        d.DrawGeometry(null, pen, geometry);
    }
}
