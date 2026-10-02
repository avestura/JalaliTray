using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JalaliTray;

/// <summary>Dev-only: renders a window's content to a PNG without touching the screen.</summary>
internal static class Snapshot
{
    static string GdiProbe() { try { return FontService.Gdi("Vazirmatn")?.Name ?? "null"; } catch (Exception ex) { return ex.ToString(); } }

    static System.Collections.Generic.IEnumerable<string> Describe(Window w)
    {
        yield return "window: " + w.FontFamily.Source;
        yield return "gdi: " + GdiProbe();
        foreach (var c in LogicalTreeHelper.GetChildren(w).OfType<object>()) { }
        foreach (var b in FindAll<System.Windows.Controls.Control>(w)) yield return b.GetType().Name + ": " + b.FontFamily.Source;
    }

    static System.Collections.Generic.IEnumerable<T> FindAll<T>(DependencyObject root) where T : DependencyObject
    {
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var ch = VisualTreeHelper.GetChild(root, i);
            if (ch is T t) yield return t;
            foreach (var d in FindAll<T>(ch)) yield return d;
        }
    }

    public static void Save(Window w, string path)
    {
        var content = (FrameworkElement)w.Content;
        File.WriteAllText(path + ".txt", string.Join(Environment.NewLine, Describe(w)));
        int width = (int)Math.Ceiling(w.ActualWidth), height = (int)Math.Ceiling(w.ActualHeight);
        var bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var bg = new DrawingVisual();
        using (var dc = bg.RenderOpen())
            dc.DrawRectangle(w.Background, null, new Rect(0, 0, width, height));
        bmp.Render(bg);
        bmp.Render(content);
        var enc = new PngBitmapEncoder();
        // Rendering an RTL element on its own mirrors it once more than the window does; undo that.
        BitmapSource final = bmp;
        if (content.FlowDirection == FlowDirection.RightToLeft)
            final = new TransformedBitmap(bmp, new ScaleTransform(-1, 1));
        enc.Frames.Add(BitmapFrame.Create(final));
        using var fs = File.Create(path);
        enc.Save(fs);
    }
}
