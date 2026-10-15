// T43 녹화 도구: 현재 화면(주 모니터) PNG + 창 목록 + UIA 트리(깊이 ≤ 12)를 dataset/<name>.json 으로 저장한다.
// 사용: ScreenAnnotation.DatasetRecorder <name> [출력 폴더]  → 3초 뒤 녹화. 클릭 지점·기대 사각형(clicks)은 JSON을 열어 손으로 적는다.
using System.IO;
using System.Text.Json;
using Interop.UIAutomationClient;
using Microsoft.Extensions.Logging.Abstractions;
using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Recognition.Dataset;
using ScreenAnnotation.Rendering;
using ScreenAnnotation.Windows.Capture;
using ScreenAnnotation.Windows.Monitors;
using ScreenAnnotation.Windows.Windows;

if (args.Length < 1) { Console.WriteLine("usage: DatasetRecorder <name> [outDir]"); return 1; }
var name = args[0];
var outDir = args.Length > 1 ? args[1] : "dataset";
Directory.CreateDirectory(outDir);

DpiAwareness.EnablePerMonitorV2();
Console.WriteLine("3초 뒤 녹화합니다. 대상 화면을 앞에 두세요.");
Thread.Sleep(3000);

using var monitors = new MonitorService(NullLogger<MonitorService>.Instance);
var primary = monitors.Monitors.First(m => m.IsPrimary);
using var capture = new CaptureService(NullLogger<CaptureService>.Instance);
var frame = (await capture.CaptureAllAsync([primary])).First();
var png = Path.Combine(outDir, name + ".png");
PngWriter.Save(frame, png);

var snapshot = new WindowSnapshotService(NullLogger<WindowSnapshotService>.Instance).Take();
var uia = new CUIAutomation8();
var walker = uia.RawViewWalker;
var screen = new RecordedScreen { Name = name, Png = Path.GetFileName(png) };
foreach (var w in snapshot.Windows.Where(w => primary.Bounds.Contains(w.Bounds.X + 1, w.Bounds.Y + 1)))
{
    try { screen.Windows.Add(Dump(uia.ElementFromHandle(w.Handle), 0)); }
    catch (Exception ex) { Console.WriteLine($"건너뜀 {w.Title}: {ex.Message}"); }
}
var json = Path.Combine(outDir, name + ".json");
File.WriteAllText(json, JsonSerializer.Serialize(screen, RecordedScreen.Json));
Console.WriteLine($"저장: {json} (창 {screen.Windows.Count}개). clicks[]에 x,y,expected{{x,y,w,h}}를 추가하세요.");
return 0;

RecordedNode Dump(IUIAutomationElement e, int depth)
{
    var r = e.CurrentBoundingRectangle;
    var node = new RecordedNode { Type = e.CurrentControlType.ToString(), Name = e.CurrentName ?? "", X = r.left, Y = r.top, W = r.right - r.left, H = r.bottom - r.top };
    if (depth >= 12) return node;
    for (var c = walker.GetFirstChildElement(e); c is not null; c = walker.GetNextSiblingElement(c))
    {
        try { node.Children.Add(Dump(c, depth + 1)); } catch { /* 사라진 요소 */ }
        if (node.Children.Count > 500) break; // ponytail: 거대한 트리(브라우저) 상한
    }
    return node;
}
