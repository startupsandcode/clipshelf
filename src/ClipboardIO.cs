using System;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClipShelf;

static class ClipboardReader
{
    const int MaxText = 10_000_000;
    const int MaxRich = 2_000_000;

    // Password managers and other privacy-aware apps mark sensitive copies with these formats.
    static readonly uint ExcludeFormat = Native.RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
    static readonly uint ViewerIgnoreFormat = Native.RegisterClipboardFormat("Clipboard Viewer Ignore");

    public static CapturedClip? Read(string? source)
    {
        if (Native.IsClipboardFormatAvailable(ExcludeFormat) || Native.IsClipboardFormatAvailable(ViewerIgnoreFormat)) return null;

        IDataObject? data = null;
        for (int attempt = 0; attempt < 6 && data == null; attempt++)
        {
            try { data = Clipboard.GetDataObject(); }
            catch (ExternalException) { Thread.Sleep(25); }
        }
        if (data == null) return null;

        if (Get(data, "CanIncludeInClipboardHistory") is MemoryStream flag && flag.Length >= 4 &&
            BitConverter.ToInt32(flag.ToArray(), 0) == 0)
        {
            return null;
        }

        if (Get(data, DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            var paths = string.Join("\n", files);
            return new CapturedClip("files", paths, null, null, null, null, Hash("f", Encoding.UTF8.GetBytes(paths)), source);
        }

        if (Get(data, DataFormats.UnicodeText) is string text && !string.IsNullOrWhiteSpace(text) && text.Length <= MaxText)
        {
            var html = Get(data, DataFormats.Html) as string;
            var rtf = Get(data, DataFormats.Rtf) as string;
            return new CapturedClip("text", text, Cap(html), Cap(rtf), null, null, Hash("t", Encoding.UTF8.GetBytes(text)), source);
        }

        var image = ReadImage(data);
        if (image != null)
        {
            var png = Imaging.EncodePng(image);
            var thumb = Imaging.EncodePng(Imaging.Thumbnail(image));
            return new CapturedClip("image", $"Image {image.PixelWidth}×{image.PixelHeight}", null, null, png, thumb, Hash("i", png), source);
        }
        return null;
    }

    static BitmapSource? ReadImage(IDataObject data)
    {
        // Prefer PNG (keeps transparency); fall back to the classic bitmap format.
        if (Get(data, "PNG") is MemoryStream png)
        {
            try { return Imaging.Decode(png.ToArray()); }
            catch { }
        }
        return Get(data, DataFormats.Bitmap) as BitmapSource;
    }

    static object? Get(IDataObject data, string format)
    {
        try { return data.GetDataPresent(format) ? data.GetData(format) : null; }
        catch { return null; }
    }

    static string? Cap(string? rich) => rich != null && rich.Length <= MaxRich ? rich : null;

    static string Hash(string kind, byte[] bytes) => kind + ":" + Convert.ToHexString(SHA256.HashData(bytes));
}

static class ClipboardWriter
{
    public static bool Write(StoredClip clip, bool plain)
    {
        var data = new DataObject();
        if (clip.Kind == "image" && clip.Image != null)
        {
            data.SetImage(Imaging.Decode(clip.Image));
            data.SetData("PNG", new MemoryStream(clip.Image));
        }
        else if (clip.Kind == "files" && !plain)
        {
            var files = new StringCollection();
            files.AddRange(clip.Text.Split('\n'));
            data.SetFileDropList(files);
            data.SetText(clip.Text);
        }
        else
        {
            data.SetText(clip.Text);
            if (!plain)
            {
                if (clip.Html != null) data.SetData(DataFormats.Html, clip.Html);
                if (clip.Rtf != null) data.SetData(DataFormats.Rtf, clip.Rtf);
            }
        }

        for (int attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(data, copy: true);
                return true;
            }
            catch (ExternalException)
            {
                Thread.Sleep(30);
            }
        }
        return false;
    }
}

static class Imaging
{
    public static BitmapSource Decode(byte[] bytes)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = new MemoryStream(bytes);
        image.EndInit();
        image.Freeze();
        return image;
    }

    public static byte[] EncodePng(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    public static BitmapSource Thumbnail(BitmapSource source)
    {
        double scale = Math.Min(1.0, Math.Min(440.0 / source.PixelWidth, 260.0 / source.PixelHeight));
        if (scale >= 1.0) return source;
        var thumb = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        thumb.Freeze();
        return thumb;
    }
}
