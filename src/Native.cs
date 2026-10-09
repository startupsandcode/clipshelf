using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ClipShelf;

static class Native
{
    public const int WM_CLIPBOARDUPDATE = 0x031D;
    public const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern uint RegisterClipboardFormat(string name);

    [DllImport("user32.dll")]
    public static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vk);

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint SendInput(uint count, INPUT[] inputs, int size);

    /// <summary>Friendly name of the app that owns a window, e.g. "Google Chrome".</summary>
    public static string? AppName(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return null;
        GetWindowThreadProcessId(hwnd, out uint pid);
        try
        {
            using var process = Process.GetProcessById((int)pid);
            try
            {
                var description = process.MainModule?.FileVersionInfo.FileDescription;
                if (!string.IsNullOrWhiteSpace(description)) return description.Trim();
            }
            catch
            {
                // Elevated or protected processes don't expose their modules.
            }
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Types Ctrl+V into whatever has focus, releasing Shift first so it isn't read as Ctrl+Shift+V.</summary>
    public static void SendCtrlV()
    {
        const ushort Ctrl = 0x11, V = 0x56;
        var inputs = new List<INPUT>();
        foreach (ushort shift in new ushort[] { 0xA0, 0xA1 })
        {
            if ((GetAsyncKeyState(shift) & 0x8000) != 0) inputs.Add(Key(shift, up: true));
        }
        inputs.Add(Key(Ctrl, up: false));
        inputs.Add(Key(V, up: false));
        inputs.Add(Key(V, up: true));
        inputs.Add(Key(Ctrl, up: true));
        SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
    }

    static INPUT Key(ushort vk, bool up) => new()
    {
        type = 1,
        u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = up ? 2u : 0u } },
    };

    [StructLayout(LayoutKind.Sequential)]
    struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public IntPtr dwExtraInfo;
    }
}
