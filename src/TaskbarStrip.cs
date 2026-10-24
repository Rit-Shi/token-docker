using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

// A slim strip that sits on top of the Windows taskbar and shows every provider in one row.
class TaskbarStrip : Window {
    const int GwlExStyle = -20;
    const int WsExTransparent = 0x00000020;
    const int WsExToolWindow = 0x00000080;
    const int WsExNoActivate = 0x08000000;

    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr handle, int index);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr handle, int index, int value);

    readonly TaskbarFace face = new TaskbarFace();
    readonly DispatcherTimer watch = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
    bool dragging, moved, clickThrough = true;
    Point start;
    double origin;

    public Action<string> ProviderClicked;
    public Action MenuRequested;
    public Action ThemeRequested;
    public Action<int> PositionChanged;
    // Pulling the strip away from its edge turns it back into the floating bar.
    public Action<double, double> DetachRequested;
    public bool AtTop;

    public TaskbarStrip() {
        Title = "Token Bar · 任务栏";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = false;
        Content = face;

        SourceInitialized += (s, e) => {
            ApplyExStyle();
            watch.Start();
        };
        Closed += (s, e) => watch.Stop();
        watch.Tick += (s, e) => Track();

        face.MouseLeftButtonDown += OnMouseDown;
        face.MouseMove += OnMouseMove;
        face.MouseLeftButtonUp += OnMouseUp;
        face.MouseRightButtonUp += (s, e) => { if (MenuRequested != null) MenuRequested(); };
        face.MouseLeave += (s, e) => { face.Hover = -1; face.ToolTip = null; face.InvalidateVisual(); };
        ToolTipService.SetInitialShowDelay(face, 350);
        ToolTipService.SetShowDuration(face, 20000);
    }

    public void Update(System.Collections.Generic.List<Row> rows, bool light, Limits limits) {
        face.Rows = rows;
        face.Light = light;
        face.Limits = limits;
        face.Width = face.ContentWidth;
        face.Height = face.ContentHeight;
        Width = face.ContentWidth;
        Height = face.ContentHeight;
        face.InvalidateMeasure();
        face.InvalidateVisual();
    }

    // The strip never takes focus, so the app you are working in keeps the caret.
    void ApplyExStyle() {
        var handle = new WindowInteropHelper(this).Handle;
        int style = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, style | WsExNoActivate | WsExToolWindow | WsExTransparent);
    }

    // Click-through while the cursor is elsewhere, interactive while it is over the strip.
    // A transparent window never receives mouse messages, so this has to be polled.
    void Track() {
        if (dragging || !IsVisible) return;
        var source = PresentationSource.FromVisual(this);
        if (source == null) return;
        var p = WinForms.Control.MousePosition;
        var at = source.CompositionTarget.TransformFromDevice.Transform(new Point(p.X, p.Y));
        bool over = at.X >= Left && at.X <= Left + Width && at.Y >= Top && at.Y <= Top + Height;
        SetClickThrough(!over);
        if (!over && face.Hover != -1) {
            face.Hover = -1;
            face.InvalidateVisual();
        }
    }

    void SetClickThrough(bool value) {
        if (clickThrough == value) return;
        clickThrough = value;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        int style = GetWindowLong(handle, GwlExStyle);
        if (value) style |= WsExTransparent;
        else style &= ~WsExTransparent;
        SetWindowLong(handle, GwlExStyle, style);
    }

    // The strip anchors to the top or bottom of the screen; the edge is chosen by where the
    // floating bar was dropped and stored in the settings.
    public void Place(int rememberedX, bool atTop) {
        AtTop = atTop;
        var screen = WinForms.Screen.PrimaryScreen;
        var bounds = screen.Bounds;
        var work = screen.WorkingArea;
        var source = PresentationSource.FromVisual(this);
        var transform = source == null ? Matrix.Identity : source.CompositionTarget.TransformFromDevice;
        var screenRect = new Rect(transform.Transform(new Point(bounds.Left, bounds.Top)), transform.Transform(new Point(bounds.Right, bounds.Bottom)));
        var workRect = new Rect(transform.Transform(new Point(work.Left, work.Top)), transform.Transform(new Point(work.Right, work.Bottom)));

        double x = rememberedX >= 0 ? rememberedX : workRect.Right - Width - 12;
        x = Math.Max(workRect.Left, Math.Min(x, workRect.Right - Width));
        Left = x;
        if (atTop) Top = workRect.Top > screenRect.Top ? workRect.Top + 4 : screenRect.Top + 4;
        else Top = workRect.Bottom - Height - 4;
    }

    // Slide + fade in from the floating bar's previous position.
    public void AnimateIn(double offsetY) {
        var translate = new TranslateTransform(0, offsetY);
        face.RenderTransform = translate;
        Opacity = 0;
        Anim.Tween(200, t => {
            Opacity = t;
            translate.Y = offsetY * (1 - t);
            if (t >= 1) face.RenderTransform = Transform.Identity;
        });
    }

    void OnMouseDown(object sender, MouseButtonEventArgs e) {
        dragging = true;
        moved = false;
        start = e.GetPosition(this);
        origin = Left;
        face.CaptureMouse();
    }

    void OnMouseMove(object sender, MouseEventArgs e) {
        var p = e.GetPosition(this);
        if (dragging) {
            double deltaX = p.X - start.X;
            double deltaY = p.Y - start.Y;
            if (Math.Abs(deltaX) > 3 || Math.Abs(deltaY) > 3) moved = true;

            // Pulling inward (away from the docked edge) detaches the strip.
            double away = AtTop ? deltaY : -deltaY;
            if (away > 40) {
                dragging = false;
                face.ReleaseMouseCapture();
                if (DetachRequested != null) DetachRequested(Left, Top);
                return;
            }
            if (!moved) return;

            var source = PresentationSource.FromVisual(this);
            var transform = source == null ? Matrix.Identity : source.CompositionTarget.TransformFromDevice;
            var work = WinForms.Screen.PrimaryScreen.WorkingArea;
            var workRect = new Rect(transform.Transform(new Point(work.Left, work.Top)), transform.Transform(new Point(work.Right, work.Bottom)));
            Left = Math.Max(workRect.Left, Math.Min(origin + deltaX, workRect.Right - Width));
            return;
        }
        int hit = face.Hit(p);
        if (hit == face.Hover) return;
        face.Hover = hit;
        face.ToolTip = hit >= 0 && hit < face.Rows.Count ? Details(face.Rows[hit]) : null;
        face.InvalidateVisual();
    }

    // Hovering a tile shows the same detail the floating bar would, including the estimate.
    static string Details(Row row) {
        string text = row.Label + "\n" + row.Meter.Detail;
        if (row.Spend != null) text += "\n" + row.Spend + "（按当日余额差估算）";
        return text;
    }

    void OnMouseUp(object sender, MouseButtonEventArgs e) {
        if (!dragging) return;
        dragging = false;
        face.ReleaseMouseCapture();
        if (moved) {
            if (PositionChanged != null) PositionChanged((int)Left);
            return;
        }
        int hit = face.Hit(e.GetPosition(face));
        if (hit == TaskbarFace.HitTheme) {
            if (ThemeRequested != null) ThemeRequested();
        } else if (hit == TaskbarFace.HitMenu) {
            if (MenuRequested != null) MenuRequested();
        } else if (hit >= 0 && hit < face.Rows.Count && ProviderClicked != null) {
            ProviderClicked(face.Rows[hit].Id);
        }
    }
}
