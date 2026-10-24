using System;
using System.Collections.Generic;
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
        formatted.MaxLineCount = 1;
        formatted.Trimming = TextTrimming.CharacterEllipsis;
        d.DrawText(formatted, at);
    }

    public static void Centered(DrawingContext d, string text, double size, Brush ink, Rect area) {
        var typeface = new Typeface(new FontFamily("Segoe UI, Microsoft YaHei UI"), FontStyles.Normal,
            FontWeights.SemiBold, FontStretches.Normal);
#pragma warning disable 0618
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, size, ink);
#pragma warning restore 0618
        d.DrawText(formatted, new Point(area.X + (area.Width - formatted.Width) / 2, area.Y + (area.Height - formatted.Height) / 2));
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

    // ---- full-colour SVG icons -------------------------------------------------
    // The bundled logos come in two variants: a single-colour file (currentColor) and a
    // colour file where paths carry their own fill and may reference a linear gradient.

    class SvgShape {
        public Geometry Geometry;
        public string Fill;
        public SvgGradient Gradient;
    }

    class SvgGradient {
        public double X1, Y1, X2, Y2;
        public readonly List<Color> Colors = new List<Color>();
        public readonly List<double> Offsets = new List<double>();
    }

    static readonly Dictionary<string, List<SvgShape>> svgCache = new Dictionary<string, List<SvgShape>>();

    public static bool HasSvg(string name) {
        return Load(name) != null;
    }

    public static bool Svg(DrawingContext d, string name, Rect r, Brush ink) {
        var shapes = Load(name);
        if (shapes == null) return false;
        d.PushTransform(new TranslateTransform(r.X, r.Y));
        d.PushTransform(new ScaleTransform(r.Width / 24, r.Height / 24));
        foreach (var shape in shapes) {
            Brush brush = ink;
            if (shape.Gradient != null) brush = GradientBrush(shape.Gradient);
            else if (!string.IsNullOrEmpty(shape.Fill) && shape.Fill != "currentColor") brush = new SolidColorBrush(C(shape.Fill));
            d.DrawGeometry(brush, null, shape.Geometry);
        }
        d.Pop();
        d.Pop();
        return true;
    }

    static List<SvgShape> Load(string name) {
        List<SvgShape> cached;
        if (svgCache.TryGetValue(name, out cached)) return cached;

        List<SvgShape> shapes = null;
        using (var stream = typeof(Paint).Assembly.GetManifestResourceStream(name + ".svg")) {
            if (stream != null) {
                var xml = new XmlDocument { XmlResolver = null };
                xml.Load(stream);

                var gradients = new Dictionary<string, SvgGradient>();
                foreach (XmlNode node in xml.GetElementsByTagName("linearGradient")) {
                    var gradient = new SvgGradient();
                    gradient.X1 = Percent(node.Attributes["x1"], 0);
                    gradient.Y1 = Percent(node.Attributes["y1"], 0);
                    gradient.X2 = Percent(node.Attributes["x2"], 1);
                    gradient.Y2 = Percent(node.Attributes["y2"], 0);
                    foreach (XmlNode stop in node.ChildNodes) {
                        if (stop.Name != "stop" || stop.Attributes["stop-color"] == null) continue;
                        gradient.Colors.Add(C(stop.Attributes["stop-color"].Value));
                        gradient.Offsets.Add(Percent(stop.Attributes["offset"], 0));
                    }
                    if (node.Attributes["id"] != null && gradient.Colors.Count > 0)
                        gradients[node.Attributes["id"].Value] = gradient;
                }

                var root = xml.DocumentElement;
                string rootFill = root == null ? "currentColor" : root.GetAttribute("fill");
                FillRule rule = root != null && root.GetAttribute("fill-rule") == "evenodd" ? FillRule.EvenOdd : FillRule.Nonzero;

                shapes = new List<SvgShape>();
                foreach (XmlNode path in xml.GetElementsByTagName("path")) {
                    if (path.Attributes["d"] == null) continue;
                    var shape = new SvgShape();
                    shape.Geometry = Geometry.Parse(path.Attributes["d"].Value);
                    var pathGeometry = shape.Geometry as PathGeometry;
                    if (pathGeometry != null) pathGeometry.FillRule = rule;
                    shape.Fill = path.Attributes["fill"] != null ? path.Attributes["fill"].Value
                        : string.IsNullOrEmpty(rootFill) ? "currentColor" : rootFill;
                    if (shape.Fill.StartsWith("url(#", StringComparison.Ordinal)) {
                        int end = shape.Fill.IndexOf(')');
                        string key = shape.Fill.Substring(5, end - 5);
                        SvgGradient gradient;
                        if (gradients.TryGetValue(key, out gradient)) shape.Gradient = gradient;
                    }
                    shapes.Add(shape);
                }
            }
        }
        svgCache[name] = shapes;
        return shapes;
    }

    static double Percent(XmlAttribute attribute, double fallback) {
        if (attribute == null) return fallback;
        string text = attribute.Value.Trim();
        double value;
        if (text.EndsWith("%", StringComparison.Ordinal)) {
            return double.TryParse(text.Substring(0, text.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                ? value / 100 : fallback;
        }
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback;
    }

    static Brush GradientBrush(SvgGradient gradient) {
        var brush = new LinearGradientBrush {
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
            StartPoint = new Point(gradient.X1, gradient.Y1),
            EndPoint = new Point(gradient.X2, gradient.Y2)
        };
        for (int i = 0; i < gradient.Colors.Count; i++)
            brush.GradientStops.Add(new GradientStop(gradient.Colors[i], gradient.Offsets[i]));
        return brush;
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
