using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScreenAnnotation.Rendering;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Color = System.Windows.Media.Color;

namespace ScreenAnnotation.App;

public enum CloseChoice { Save, Discard, Continue }

/// <summary>끌 때 판서가 있으면 묻는 작은 창: 저장(Enter) / 버리기(N) / 계속 판서(Esc).</summary>
public sealed class CloseConfirmWindow : Window
{
    internal CloseChoice Result { get; private set; } = CloseChoice.Continue;

    public CloseConfirmWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = false;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        buttons.Children.Add(MakeButton("저장 (Enter)", CloseChoice.Save));
        buttons.Children.Add(MakeButton("버리기 (N)", CloseChoice.Discard));
        buttons.Children.Add(MakeButton("계속 판서 (Esc)", CloseChoice.Continue));

        var body = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
        body.Children.Add(new TextBlock
        {
            Text = "판서를 저장할까요?", Foreground = Brushes.White, FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 16),
        });
        body.Children.Add(buttons);

        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x20, 0x20, 0x20)),
            CornerRadius = new CornerRadius(12),
            Child = body,
        };

        PreviewKeyDown += OnKey;
    }

    /// <summary>owner 모니터 가운데에 띄우고 선택을 돌려준다. 그냥 닫히면 Continue.</summary>
    public static CloseChoice Show(OverlayWindow owner, out CloseConfirmWindow dialog)
    {
        dialog = new CloseConfirmWindow { Owner = owner };
        dialog.ShowDialog();
        return dialog.Result;
    }

    internal void Choose(CloseChoice choice)
    {
        Result = choice;
        if (IsVisible) Close();
    }

    private Button MakeButton(string text, CloseChoice choice)
    {
        var b = new Button
        {
            Content = text, Margin = new Thickness(6, 0, 6, 0), Padding = new Thickness(14, 8, 14, 8),
            FontSize = 14, Focusable = false,
        };
        b.Click += (_, _) => Choose(choice);
        return b;
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter: Choose(CloseChoice.Save); break;
            case Key.N: Choose(CloseChoice.Discard); break;
            case Key.Escape: Choose(CloseChoice.Continue); break;
            default: return;
        }
        e.Handled = true;
    }
}
