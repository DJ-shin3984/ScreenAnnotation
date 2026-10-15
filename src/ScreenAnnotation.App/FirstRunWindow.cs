using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using CheckBox = System.Windows.Controls.CheckBox;
using Button = System.Windows.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace ScreenAnnotation.App;

/// <summary>첫 실행 안내: 단축키 소개 + VS Code 충돌 시 변경 방법. 표시 여부는 마커 파일로 기억한다.</summary>
public sealed class FirstRunWindow : Window
{
    /// <summary>테스트에서 바꿔 끼운다.</summary>
    internal static string MarkerPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenAnnotation", "firstrun.done");

    private static readonly (string Key, string What)[] Keys =
    [
        ("Ctrl+Shift+Space", "켜기(화면 정지 + 펜) / 끄기"),
        ("Tab / Shift+Tab", "도구 순환(펜 → 형광펜 → 도형) / 변형 순환(일반 → 사라지는 펜 → 레이저)"),
        ("우클릭", "색상 팔레트 (도형 도구에서는 도형 팔레트)"),
        ("휠 / Ctrl+휠", "굵기 조절 / 확대·축소 (Ctrl+0 원래대로)"),
        ("1~9", "색 바로 선택"),
        ("T / 빈 곳 더블클릭", "텍스트 입력"),
        ("E", "지우개 켜기·끄기"),
        ("Ctrl+Z / Ctrl+Y", "Undo / Redo"),
        ("Ctrl+S / Ctrl+C", "PNG 저장 / 클립보드 복사"),
        ("Esc", "진행 중 취소, 펜으로 복귀 (앱은 꺼지지 않음)"),
    ];

    public static bool ShouldShow() => !File.Exists(MarkerPath);

    public static void MarkShown()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath)!);
        File.WriteAllText(MarkerPath, DateTime.Now.ToString("O"));
    }

    public FirstRunWindow()
    {
        Title = "Screen Annotation 시작하기";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;

        var grid = new Grid { Margin = new Thickness(8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < Keys.Length; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition());
            var k = new TextBlock { Text = Keys[i].Key, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 2, 16, 2) };
            var w = new TextBlock { Text = Keys[i].What, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) };
            Grid.SetRow(k, i); Grid.SetRow(w, i); Grid.SetColumn(w, 1);
            grid.Children.Add(k); grid.Children.Add(w);
        }

        var dontShow = new CheckBox { Content = "다시 보지 않기", IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
        var ok = new Button { Content = "확인", Width = 90, Padding = new Thickness(0, 4, 0, 4), IsDefault = true };
        ok.Click += (_, _) => { if (dontShow.IsChecked == true) MarkShown(); Close(); };

        var bottom = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(ok, Dock.Right);
        bottom.Children.Add(ok);
        bottom.Children.Add(dontShow);

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Children =
            {
                new TextBlock { Text = "트레이에서 실행 중입니다. 아무 화면에서나 Ctrl+Shift+Space를 누르세요.", FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap },
                grid,
                new TextBlock
                {
                    Text = "VS Code 등에서 Ctrl+Shift+Space가 겹치면 트레이 아이콘 → 설정에서 단축키를 바꾸세요.",
                    TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DarkSlateGray, Margin = new Thickness(0, 8, 0, 0),
                },
                bottom,
            },
        };
    }
}
