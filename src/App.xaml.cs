using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using WF = System.Windows.Forms;

namespace ClipShelf;

public partial class App : Application
{
    const int HotkeyId = 0xC5;

    Mutex? _mutex;
    string _folder = "";
    Settings _settings = new();
    Store? _store;
    MainWindow? _window;
    HwndSource? _messages;
    WF.NotifyIcon? _tray;
    WF.ToolStripMenuItem? _pauseItem;
    WF.ToolStripMenuItem? _autostartItem;
    readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(150) };
    IntPtr _copySource;
    IntPtr _pasteTarget;
    DateTime _ignoreUntil;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _mutex = new Mutex(true, @"Local\ClipShelf.SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            Shutdown();
            return;
        }

        _folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipShelf");
        Directory.CreateDirectory(_folder);
        DispatcherUnhandledException += (_, args) => { Log(args.Exception); args.Handled = true; };

        _settings = Settings.Load(_folder);
        _store = new Store(_folder);
        _window = new MainWindow(_store);
        _window.ClipChosen += OnClipChosen;
        _debounce.Tick += (_, _) => { _debounce.Stop(); Capture(); };

        // Hidden message-only window: receives clipboard-change notifications and the global hotkey.
        _messages = new HwndSource(new HwndSourceParameters("ClipShelfMessages") { ParentWindow = new IntPtr(-3), WindowStyle = 0 });
        _messages.AddHook(WndProc);
        Native.AddClipboardFormatListener(_messages.Handle);

        CreateTray();
        if (!Settings.TryParseHotkey(_settings.Hotkey, out uint mods, out uint vk) ||
            !Native.RegisterHotKey(_messages.Handle, HotkeyId, mods, vk))
        {
            Notify($"Couldn't register {_settings.Hotkey}. Another app may own it. Change \"Hotkey\" in settings.json and restart.");
        }
        else if (_settings.IsNew)
        {
            Notify($"Running in the tray. Press {_settings.Hotkey} to open your clipboard history.");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_messages != null)
        {
            Native.RemoveClipboardFormatListener(_messages.Handle);
            Native.UnregisterHotKey(_messages.Handle, HotkeyId);
            _messages.Dispose();
        }
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        _store?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_CLIPBOARDUPDATE)
        {
            if (!_settings.Paused && DateTime.UtcNow >= _ignoreUntil)
            {
                // Remember who copied now; read the data a beat later, since apps often update the clipboard in bursts.
                _copySource = Native.GetForegroundWindow();
                _debounce.Stop();
                _debounce.Start();
            }
            handled = true;
        }
        else if (msg == Native.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            TogglePicker(fromHotkey: true);
            handled = true;
        }
        return IntPtr.Zero;
    }

    void Capture()
    {
        var clip = ClipboardReader.Read(Native.AppName(_copySource));
        if (clip == null) return;
        _store!.Save(clip);
        if (_window!.IsVisible) _window.Reload();
    }

    void TogglePicker(bool fromHotkey)
    {
        if (_window!.IsVisible)
        {
            _window.Hide();
            return;
        }
        // From the tray there is no app to paste into, so choosing a clip just copies it.
        _pasteTarget = fromHotkey ? Native.GetForegroundWindow() : IntPtr.Zero;
        _window.ShowPicker();
    }

    async void OnClipChosen(long id, ClipAction action)
    {
        var clip = _store!.Get(id);
        if (clip == null) return;
        _window!.Hide();

        _ignoreUntil = DateTime.UtcNow.AddMilliseconds(700);
        if (!ClipboardWriter.Write(clip, plain: action == ClipAction.PastePlain))
        {
            Notify("Couldn't write to the clipboard. Try again.");
            return;
        }
        _store.Touch(id);

        if (action == ClipAction.CopyOnly || _pasteTarget == IntPtr.Zero) return;
        Native.SetForegroundWindow(_pasteTarget);
        await Task.Delay(90);
        Native.SendCtrlV();
    }

    void CreateTray()
    {
        var menu = new WF.ContextMenuStrip();
        menu.Items.Add($"Open history ({_settings.Hotkey})", null, (_, _) => TogglePicker(fromHotkey: false));
        menu.Items.Add(new WF.ToolStripSeparator());
        _pauseItem = new WF.ToolStripMenuItem("Pause capture", null, (_, _) => TogglePause()) { Checked = _settings.Paused };
        menu.Items.Add(_pauseItem);
        _autostartItem = new WF.ToolStripMenuItem("Start with Windows", null, (_, _) =>
        {
            Autostart.Set(!Autostart.IsEnabled);
            _autostartItem!.Checked = Autostart.IsEnabled;
        }) { Checked = Autostart.IsEnabled };
        menu.Items.Add(_autostartItem);
        menu.Items.Add("Open data folder", null, (_, _) => Process.Start("explorer.exe", _folder));
        menu.Items.Add("Clear history (keeps pinned)...", null, (_, _) => ClearHistory());
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add("Quit ClipShelf", null, (_, _) => Shutdown());

        _tray = new WF.NotifyIcon { Icon = LoadIcon(), ContextMenuStrip = menu, Visible = true };
        _tray.MouseClick += (_, args) => { if (args.Button == WF.MouseButtons.Left) TogglePicker(fromHotkey: false); };
        UpdateTrayText();
    }

    void TogglePause()
    {
        _settings.Paused = !_settings.Paused;
        _settings.Save();
        _pauseItem!.Checked = _settings.Paused;
        UpdateTrayText();
    }

    void UpdateTrayText() => _tray!.Text = _settings.Paused ? "ClipShelf (paused)" : $"ClipShelf: {_settings.Hotkey}";

    void ClearHistory()
    {
        var answer = MessageBox.Show("Delete every clip that isn't pinned? This can't be undone.", "ClipShelf",
            MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (answer == MessageBoxResult.OK) _store!.ClearUnpinned();
    }

    void Notify(string message) => _tray?.ShowBalloonTip(4000, "ClipShelf", message, WF.ToolTipIcon.Info);

    static System.Drawing.Icon LoadIcon()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ClipShelf.icon.ico");
        return stream != null
            ? new System.Drawing.Icon(stream, WF.SystemInformation.SmallIconSize)
            : System.Drawing.SystemIcons.Application;
    }

    void Log(Exception ex)
    {
        try { File.AppendAllText(Path.Combine(_folder, "error.log"), $"{DateTime.Now:O} {ex}\n\n"); }
        catch { }
    }
}
