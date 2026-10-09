using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace ClipShelf;

public enum ClipAction { Paste, PastePlain, CopyOnly }

public partial class MainWindow : Window
{
    const int PageSize = 400;
    readonly Store _store;
    bool _pinnedOnly;
    bool _resetting;

    public event Action<long, ClipAction>? ClipChosen;

    public MainWindow(Store store)
    {
        _store = store;
        InitializeComponent();
        Deactivated += (_, _) => Hide();
    }

    ClipItem? Selected => List.SelectedItem as ClipItem;

    public void ShowPicker()
    {
        _resetting = true;
        Search.Text = "";
        _resetting = false;
        _pinnedOnly = false;
        Reload();
        PositionOnActiveScreen();
        Show();
        Activate();
        Search.Focus();
    }

    public void Reload(int selectIndex = 0)
    {
        var query = Search.Text.Trim();
        var items = _store.Query(query, _pinnedOnly, PageSize);
        for (int i = 0; i < items.Count; i++) items[i].Index = i;
        List.ItemsSource = items;

        Placeholder.Visibility = Search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        Empty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Empty.Text = query.Length > 0 ? $"No clips match \"{query}\""
            : _pinnedOnly ? "No pinned clips yet. Select one and press Ctrl+P."
            : "Nothing yet. Copy something and it will show up here.";
        Status.Text = $"{(_pinnedOnly ? "Pinned" : "All clips")}   ·   {_store.Count():N0} saved";

        if (items.Count > 0) Select(Math.Clamp(selectIndex, 0, items.Count - 1));
    }

    void Select(int index)
    {
        List.SelectedIndex = index;
        List.ScrollIntoView(List.SelectedItem);
    }

    void PositionOnActiveScreen()
    {
        // Screen coordinates are device pixels; WPF positions in device-independent units.
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        var toDip = HwndSource.FromHwnd(hwnd).CompositionTarget.TransformFromDevice;
        var area = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
        var topLeft = toDip.Transform(new Point(area.Left, area.Top));
        var bottomRight = toDip.Transform(new Point(area.Right, area.Bottom));
        Left = topLeft.X + 12;
        Width = bottomRight.X - topLeft.X - 24;
        Top = bottomRight.Y - Height;
    }

    void Choose(ClipAction action, ClipItem? item = null)
    {
        item ??= Selected;
        if (item != null) ClipChosen?.Invoke(item.Id, action);
    }

    void TogglePin()
    {
        if (Selected is not { } item) return;
        _store.SetPinned(item.Id, !item.Pinned);
        Reload(List.SelectedIndex);
    }

    void DeleteSelected()
    {
        if (Selected is not { } item) return;
        _store.Delete(item.Id);
        Reload(List.SelectedIndex);
    }

    void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        e.Handled = true;

        switch (e.Key)
        {
            case Key.Escape: Hide(); break;
            case Key.Right: if (List.Items.Count > 0) Select(Math.Min(List.SelectedIndex + 1, List.Items.Count - 1)); break;
            case Key.Left: if (List.Items.Count > 0) Select(Math.Max(List.SelectedIndex - 1, 0)); break;
            case Key.Home when ctrl || Search.Text.Length == 0: if (List.Items.Count > 0) Select(0); break;
            case Key.End when ctrl || Search.Text.Length == 0: if (List.Items.Count > 0) Select(List.Items.Count - 1); break;
            case Key.Enter: Choose(shift ? ClipAction.PastePlain : ClipAction.Paste); break;
            case Key.Tab: _pinnedOnly = !_pinnedOnly; Reload(); break;
            case Key.Delete when ctrl || Search.Text.Length == 0: DeleteSelected(); break;
            case Key.P when ctrl: TogglePin(); break;
            case Key.C when ctrl && Search.SelectionLength == 0: Choose(ClipAction.CopyOnly); break;
            case >= Key.D1 and <= Key.D9 when ctrl:
            {
                int index = e.Key - Key.D1;
                if (index < List.Items.Count) Choose(ClipAction.Paste, (ClipItem)List.Items[index]);
                break;
            }
            default: e.Handled = false; break;
        }
    }

    void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_resetting) Reload();
    }

    void List_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (FindScrollViewer(List) is not { } viewer) return;
        viewer.ScrollToHorizontalOffset(viewer.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    void List_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Choose(ClipAction.Paste);
    void List_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => Search.Focus();
    void ContextMenu_Closed(object sender, RoutedEventArgs e) => Search.Focus();
    void Menu_Paste(object sender, RoutedEventArgs e) => Choose(ClipAction.Paste);
    void Menu_PastePlain(object sender, RoutedEventArgs e) => Choose(ClipAction.PastePlain);
    void Menu_Copy(object sender, RoutedEventArgs e) => Choose(ClipAction.CopyOnly);
    void Menu_Pin(object sender, RoutedEventArgs e) => TogglePin();
    void Menu_Delete(object sender, RoutedEventArgs e) => DeleteSelected();

    static ScrollViewer? FindScrollViewer(DependencyObject node)
    {
        if (node is ScrollViewer viewer) return viewer;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            if (FindScrollViewer(VisualTreeHelper.GetChild(node, i)) is { } found) return found;
        }
        return null;
    }
}
