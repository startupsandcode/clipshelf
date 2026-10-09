using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClipShelf;

/// <summary>One card in the picker. Built fresh on every reload, so no change notification is needed.</summary>
public sealed class ClipItem
{
    static readonly Brush TextBrush = Frozen(0x3A, 0x6F, 0xD8);
    static readonly Brush LinkBrush = Frozen(0x2B, 0x9A, 0x66);
    static readonly Brush ImageBrush = Frozen(0x8A, 0x55, 0xD9);
    static readonly Brush FilesBrush = Frozen(0xC9, 0x7F, 0x16);

    public long Id { get; init; }
    public string Kind { get; init; } = "text";
    public string Text { get; init; } = "";
    public string? Source { get; init; }
    public DateTime Used { get; init; }
    public bool Pinned { get; init; }
    public BitmapSource? Thumb { get; init; }
    public long Length { get; init; }
    public int Index { get; set; }

    bool IsLink => Kind == "text" && !Text.Trim().Contains(' ') &&
                   Uri.TryCreate(Text.Trim(), UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public string KindLabel => Kind switch
    {
        "image" => "Image",
        "files" => Text.Split('\n').Length == 1 ? "File" : $"{Text.Split('\n').Length} files",
        _ => IsLink ? "Link" : "Text",
    };

    public Brush HeaderBrush => Kind switch
    {
        "image" => ImageBrush,
        "files" => FilesBrush,
        _ => IsLink ? LinkBrush : TextBrush,
    };

    public string SourceLabel => Source ?? "Unknown app";
    public string Preview => Kind == "image" ? "" : Text.Replace("\t", "    ").TrimStart('\r', '\n');
    public string Shortcut => Index < 9 ? $"Ctrl+{Index + 1}" : "";

    public string Meta
    {
        get
        {
            var detail = Kind switch
            {
                "image" => Text.Replace("Image ", ""),
                "files" => "",
                _ => Length == 1 ? "1 char" : $"{Length:N0} chars",
            };
            return detail.Length == 0 ? Ago(Used) : $"{Ago(Used)}  ·  {detail}";
        }
    }

    public Visibility TextVisibility => Kind == "image" ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ImageVisibility => Kind == "image" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PinVisibility => Pinned ? Visibility.Visible : Visibility.Collapsed;

    static string Ago(DateTime when)
    {
        var span = DateTime.Now - when;
        if (span.TotalSeconds < 60) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return when.ToString(when.Year == DateTime.Now.Year ? "MMM d" : "MMM d, yyyy");
    }

    static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
