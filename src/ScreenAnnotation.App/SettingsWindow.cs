using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScreenAnnotation.Core.Input;
using ScreenAnnotation.Core.Settings;
using ScreenAnnotation.Core.Tools;
using ScreenAnnotation.Rendering;
using ScreenAnnotation.Storage;
using ScreenAnnotation.Windows.Startup;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Cursors = System.Windows.Input.Cursors;
using Ellipse = System.Windows.Shapes.Ellipse;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Label = System.Windows.Controls.Label;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace ScreenAnnotation.App;

/// <summary>트레이 "설정". 바꾸는 즉시 SettingsStore에 반영하고 디스크에 저장한다.</summary>
public sealed class SettingsWindow : Window
{
    private readonly SettingsStore _store;
    private readonly SettingsRepository _repo;
    private readonly IHotkeyService _hotkey;
    private readonly ToolState _tools;

    private readonly TextBox _hotkeyBox = new() { IsReadOnly = true, Width = 200, FontSize = 14 };
    private readonly TextBlock _error = new() { Foreground = Brushes.Red, Margin = new Thickness(0, 4, 0, 0) };
    private readonly Dictionary<string, List<Ellipse>> _dots = new();
    private HotkeyGesture? _pending;

    internal string ErrorText => _error.Text;
    internal void SetPendingHotkey(HotkeyGesture g) { _pending = g; _hotkeyBox.Text = g.ToString(); }
    internal void ApplyHotkey() => OnApplyHotkey();

    public SettingsWindow(SettingsStore store, SettingsRepository repo, IHotkeyService hotkey, ToolState tools)
    {
        _store = store; _repo = repo; _hotkey = hotkey; _tools = tools;
        Title = "Screen Annotation 설정";
        Width = 440; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;

        var s = store.Current;
        var panel = new StackPanel { Margin = new Thickness(16) };

        // 단축키
        panel.Children.Add(Header("켜기/끄기 단축키"));
        _hotkeyBox.Text = s.Hotkey;
        _hotkeyBox.PreviewKeyDown += OnHotkeyKeyDown;
        var apply = new Button { Content = "적용", Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(12, 2, 12, 2) };
        apply.Click += (_, _) => OnApplyHotkey();
        panel.Children.Add(Row(_hotkeyBox, apply, new TextBlock { Text = "  칸을 클릭한 뒤 조합을 누르세요", VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray }));
        panel.Children.Add(_error);

        // 도구별 기본 색
        panel.Children.Add(Header("도구별 기본 색"));
        foreach (var (key, label) in new[] { ("Pen", "펜"), ("Highlighter", "형광펜"), ("Shape", "도형") })
            panel.Children.Add(ColorRow(key, label, s));

        // 사라지는 시간
        panel.Children.Add(Header("사라지는 펜 유지 시간"));
        var secLabel = new TextBlock { Text = $"{s.EphemeralSeconds:0.0}초", Width = 50, VerticalAlignment = VerticalAlignment.Center };
        var slider = new Slider { Minimum = 1, Maximum = 10, TickFrequency = 0.5, IsSnapToTickEnabled = true, Value = s.EphemeralSeconds, Width = 300 };
        slider.ValueChanged += (_, e) =>
        {
            secLabel.Text = $"{e.NewValue:0.0}초";
            Save(c => c with { EphemeralSeconds = e.NewValue });
        };
        panel.Children.Add(Row(slider, secLabel));

        // 저장 위치
        panel.Children.Add(Header("저장 위치"));
        var pathBox = new TextBox { Text = s.SavePath, Width = 300 };
        pathBox.LostFocus += (_, _) => { if (pathBox.Text.Trim().Length > 0) Save(c => c with { SavePath = pathBox.Text.Trim() }); };
        var browse = new Button { Content = "찾아보기", Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(12, 2, 12, 2) };
        browse.Click += (_, _) =>
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = pathBox.Text };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            pathBox.Text = dlg.SelectedPath;
            Save(c => c with { SavePath = dlg.SelectedPath });
        };
        panel.Children.Add(Row(pathBox, browse));

        // 시작 시 실행
        panel.Children.Add(Header("시작"));
        var run = new CheckBox { Content = "윈도우 시작 시 실행", IsChecked = s.RunAtStartup || StartupRegistration.IsEnabled() };
        run.Checked += (_, _) => { StartupRegistration.Set(true); Save(c => c with { RunAtStartup = true }); };
        run.Unchecked += (_, _) => { StartupRegistration.Set(false); Save(c => c with { RunAtStartup = false }); };
        panel.Children.Add(run);

        var close = new Button { Content = "닫기", Width = 80, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        close.Click += (_, _) => Close();
        panel.Children.Add(close);
        Content = panel;
    }

    private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin) return;

        var mods = HotkeyModifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mods |= HotkeyModifiers.Control;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) mods |= HotkeyModifiers.Shift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) mods |= HotkeyModifiers.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) mods |= HotkeyModifiers.Win;

        var name = key switch
        {
            Key.Space => "Space",
            >= Key.A and <= Key.Z => key.ToString(),
            >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
            >= Key.F1 and <= Key.F12 => key.ToString(),
            _ => null,
        };
        if (name is null) { _error.Text = "지원하지 않는 키입니다 (A~Z, 0~9, F1~F12, Space)"; return; }
        if (mods == HotkeyModifiers.None) { _error.Text = "Ctrl·Shift·Alt·Win 중 하나는 포함해야 합니다"; return; }

        _error.Text = "";
        SetPendingHotkey(new HotkeyGesture(mods, name));
    }

    private void OnApplyHotkey()
    {
        if (_pending is not { } g) return;
        if (g.Modifiers == HotkeyModifiers.None) { _error.Text = "Ctrl·Shift·Alt·Win 중 하나는 포함해야 합니다"; return; }

        var old = HotkeyGesture.TryParse(_store.Current.Hotkey, out var o) ? o : HotkeyGesture.Default;
        _hotkey.Unregister();
        if (_hotkey.Register(g))
        {
            _error.Text = "";
            Save(c => c with { Hotkey = g.ToString() });
            return;
        }
        _hotkey.Register(old);
        _hotkeyBox.Text = old.ToString();
        _pending = null;
        _error.Text = $"{g}는 다른 프로그램이 사용 중입니다. 다른 조합을 고르세요";
    }

    private StackPanel ColorRow(string key, string label, Settings s)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        row.Children.Add(new TextBlock { Text = label, Width = 60, VerticalAlignment = VerticalAlignment.Center });
        var dots = new List<Ellipse>();
        var current = s.ToolDefaults.TryGetValue(key, out var d) ? d.Color : Palette.Colors[0];
        foreach (var hex in Palette.Colors)
        {
            var dot = new Ellipse
            {
                Width = 22, Height = 22, Margin = new Thickness(3),
                Fill = ColorHex.Brush(hex), Stroke = Brushes.Gray, StrokeThickness = string.Equals(hex, current, StringComparison.OrdinalIgnoreCase) ? 3 : 1,
                Cursor = Cursors.Hand, Tag = hex,
            };
            dot.MouseLeftButtonUp += (_, _) => PickColor(key, hex);
            dots.Add(dot);
            row.Children.Add(dot);
        }
        _dots[key] = dots;
        return row;
    }

    private void PickColor(string key, string hex)
    {
        foreach (var dot in _dots[key]) dot.StrokeThickness = (string)dot.Tag == hex ? 3 : 1;
        Save(c =>
        {
            var defaults = new Dictionary<string, ToolDefault>(c.ToolDefaults);
            var width = defaults.TryGetValue(key, out var d) ? d.Width : 3;
            defaults[key] = new ToolDefault(hex, width);
            return c with { ToolDefaults = defaults };
        });
        _tools.ApplyDefaults(_store.Current);
    }

    private void Save(Func<Settings, Settings> change)
    {
        _store.Update(change);
        _repo.Save(_store.Current);
    }

    private static TextBlock Header(string text) => new() { Text = text, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 12, 0, 6) };

    private static StackPanel Row(params UIElement[] items)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var i in items) row.Children.Add(i);
        return row;
    }
}
