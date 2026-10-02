using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace JalaliTray;

public static class StartupService
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "JalaliTray";

    public static void Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;
            if (enabled && Environment.ProcessPath is { } path)
                key.SetValue(ValueName, $"\"{path}\"");
            else
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch { /* policy-restricted registry: ignore */ }
    }
}

public static class ThemeService
{
    public static bool WindowsUsesDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    public static void Apply(Application app, string theme)
    {
        bool dark = theme == "Dark" || (theme == "System" && WindowsUsesDark());
        app.ThemeMode = theme switch { "Light" => ThemeMode.Light, "Dark" => ThemeMode.Dark, _ => ThemeMode.System };

        void Set(string key, string hex) =>
            app.Resources[key] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));

        if (dark)
        {
            Set("Bg", "#202020"); Set("Fg", "#F3F3F3"); Set("Muted", "#A0A0A0");
            Set("Border", "#3A3A3A"); Set("Accent", "#4C9AFF"); Set("Holiday", "#FF6B6B");
            Set("Hover", "#2F2F2F"); Set("OnAccent", "#0B0B0B");
        }
        else
        {
            Set("Bg", "#FAFAFA"); Set("Fg", "#1B1B1B"); Set("Muted", "#6B6B6B");
            Set("Border", "#DADADA"); Set("Accent", "#1E6FD9"); Set("Holiday", "#D32F2F");
            Set("Hover", "#EAEAEA"); Set("OnAccent", "#FFFFFF");
        }
    }
}

public static class IconRenderer
{
    public static Icon Render(string text, System.Drawing.Color bg, System.Drawing.Color fg, string fontName, bool bold, int size)
    {
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            using (var path = RoundedRect(size, size * 0.2f))
            using (var brush = new SolidBrush(bg))
                g.FillPath(brush, path);

            using var format = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            System.Drawing.FontStyle style = bold ? System.Drawing.FontStyle.Bold : System.Drawing.FontStyle.Regular;

            float fontSize = size * 0.74f;
            Font font;
            while (true)
            {
                font = new Font(fontName, fontSize, style, GraphicsUnit.Pixel);
                if (g.MeasureString(text, font, PointF.Empty, format).Width <= size - 2 || fontSize <= 6) break;
                font.Dispose();
                fontSize -= 1;
            }

            using (font)
            using (var fgBrush = new SolidBrush(fg))
                g.DrawString(text, font, fgBrush, new RectangleF(0, 0, size, size), format);
        }
        return ToIcon(bmp);
    }

    static GraphicsPath RoundedRect(int size, float r)
    {
        var p = new GraphicsPath();
        float d = r * 2;
        p.AddArc(0, 0, d, d, 180, 90);
        p.AddArc(size - d, 0, d, d, 270, 90);
        p.AddArc(size - d, size - d, d, d, 0, 90);
        p.AddArc(0, size - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // Wraps a PNG in an ICO container so no GDI icon handle needs to be freed.
    static Icon ToIcon(Bitmap bmp)
    {
        using var png = new MemoryStream();
        bmp.Save(png, ImageFormat.Png);
        var data = png.ToArray();

        using var ico = new MemoryStream();
        using var w = new BinaryWriter(ico);
        w.Write((short)0); w.Write((short)1); w.Write((short)1);
        w.Write((byte)(bmp.Width >= 256 ? 0 : bmp.Width));
        w.Write((byte)(bmp.Height >= 256 ? 0 : bmp.Height));
        w.Write((byte)0); w.Write((byte)0);
        w.Write((short)1); w.Write((short)32);
        w.Write(data.Length); w.Write(22);
        w.Write(data);
        w.Flush();
        ico.Position = 0;
        return new Icon(ico);
    }
}
