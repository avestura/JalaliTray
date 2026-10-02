using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JalaliTray;

/// <summary>Dev-only: renders a window's content to a PNG without touching the screen.</summary>
internal static class Snapshot
{
    public static void Save(Window w, string path)
    {
        var content = (FrameworkElement)w.Content;
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
