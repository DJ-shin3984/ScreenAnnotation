using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Recognition.Dataset;
using Xunit.Abstractions;

namespace ScreenAnnotation.Recognition.Tests;

public class DatasetTests(ITestOutputHelper output)
{
    [Fact]
    public void RecordedTreePicksDeepestAndIoU()
    {
        var screen = new RecordedScreen
        {
            Windows = [new RecordedNode { X = 0, Y = 0, W = 800, H = 600, Children = [
                new RecordedNode { Type = "Pane", X = 0, Y = 0, W = 800, H = 600 },
                new RecordedNode { Type = "Image", X = 100, Y = 100, W = 200, H = 150 }] }],
        };
        RecordedTreeLocator.Deepest(screen, 150, 150).Should().Be(new RectPx(100, 100, 200, 150));
        RecordedTreeLocator.Deepest(screen, 50, 50).Should().BeNull("창 전체 크기 노드는 결과가 아님");
        RecordedTreeLocator.IoU(new RectPx(0, 0, 10, 10), new RectPx(5, 0, 10, 10)).Should().BeApproximately(1 / 3.0, 1e-9);
    }

    /// <summary>dataset/*.json 전체 성공률 (IoU ≥ 0.8). 파일이 없으면 통과하며 그렇다고 적는다.</summary>
    [Fact]
    public void DatasetSuccessRate()
    {
        var dir = FindDatasetDir();
        var files = dir is null ? [] : Directory.GetFiles(dir, "*.json");
        if (files.Length == 0) { output.WriteLine("dataset: 파일 없음 (T43 녹화 도구로 수집 필요)"); return; }

        int total = 0, ok = 0;
        var failures = new List<string>();
        foreach (var f in files)
        {
            var screen = RecordedScreen.Load(f);
            foreach (var c in screen.Clicks)
            {
                total++;
                var got = RecordedTreeLocator.Deepest(screen, c.X, c.Y);
                var iou = got is { } g ? RecordedTreeLocator.IoU(g, c.Expected.Bounds) : 0;
                if (iou >= 0.8) ok++; else failures.Add($"{Path.GetFileName(f)} ({c.X},{c.Y}) IoU={iou:F2} got={got}");
            }
        }
        output.WriteLine($"dataset success {ok}/{total}");
        foreach (var x in failures) output.WriteLine("  실패: " + x);
        total.Should().BeGreaterThan(0);
    }

    private static string? FindDatasetDir()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var candidate = Path.Combine(d.FullName, "dataset");
            if (Directory.Exists(candidate)) return candidate;
        }
        return null;
    }
}
