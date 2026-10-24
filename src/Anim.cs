using System;
using System.Diagnostics;
using System.Windows.Threading;

// Minimal 60 fps tween used for the floating bar <-> taskbar strip transition.
static class Anim {
    public static void Tween(double durationMs, Action<double> step) {
        if (durationMs <= 0) {
            step(1);
            return;
        }
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        var watch = Stopwatch.StartNew();
        timer.Tick += (s, e) => {
            double t = Math.Min(1, watch.Elapsed.TotalMilliseconds / durationMs);
            step(1 - Math.Pow(1 - t, 3));   // ease-out cubic
            if (t >= 1) timer.Stop();
        };
        timer.Start();
    }
}
