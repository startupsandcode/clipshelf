using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;
using Microsoft.Win32;

namespace ClipShelf;

public sealed class Settings
{
    public string Hotkey { get; set; } = "Ctrl+Shift+V";
    public bool Paused { get; set; }

    /// <summary>Unpinned clips not used for this many days are deleted automatically. 0 turns cleanup off.</summary>
    public int RetentionDays { get; set; } = 30;
    public bool RetentionNoticeShown { get; set; }

    [JsonIgnore] public string FilePath { get; private set; } = "";
    [JsonIgnore] public bool IsNew { get; private set; }

    public static Settings Load(string folder)
    {
        var path = Path.Combine(folder, "settings.json");
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new Settings();
                loaded.FilePath = path;
                return loaded;
            }
        }
        catch
        {
            // Unreadable settings fall through to defaults.
        }
        var fresh = new Settings { FilePath = path, IsNew = true };
        fresh.Save();
        return fresh;
    }

    public void Save() =>
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));

    /// <summary>Parses strings like "Ctrl+Shift+V" or "Alt+Space" into RegisterHotKey arguments.</summary>
    public static bool TryParseHotkey(string text, out uint modifiers, out uint vk)
    {
        const uint NoRepeat = 0x4000;
        modifiers = NoRepeat;
        vk = 0;
        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= 0x2; break;
                case "shift": modifiers |= 0x4; break;
                case "alt": modifiers |= 0x1; break;
                case "win": modifiers |= 0x8; break;
                default:
                    if (!Enum.TryParse(raw, ignoreCase: true, out Key key)) return false;
                    vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                    break;
            }
        }
        return vk != 0;
    }
}

static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Name = "ClipShelf";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(Name) is string;
        }
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(Name, $"\"{Environment.ProcessPath}\"");
        else key.DeleteValue(Name, throwOnMissingValue: false);
    }
}
