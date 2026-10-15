using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Recognition;
using Xunit.Abstractions;

namespace ScreenAnnotation.Recognition.Tests;

public class ImageRegionDetectorTests(ITestOutputHelper output)
{
    private const int W = 800, H = 600;

    private static byte[] Image(Action<byte[]> paint, byte bg = 255)
    {
        var img = new byte[W * H * 4];
        for (var i = 0; i < img.Length; i += 4) { img[i] = bg; img[i + 1] = bg; img[i + 2] = bg; img[i + 3] = 255; }
        paint(img);
        return img;
    }

    private static void Set(byte[] img, int x, int y, byte b, byte g, byte r)
    {
        var i = (y * W + x) * 4;
        img[i] = b; img[i + 1] = g; img[i + 2] = r;
    }

    private static void Fill(byte[] img, RectPx rc, byte b, byte g, byte r)
    {
        for (var y = rc.Y; y < rc.Bottom; y++)
            for (var x = rc.X; x < rc.Right; x++)
                Set(img, x, y, b, g, r);
    }

    private static RectPx? Detect(byte[] img, int x, int y) => ImageRegionDetector.Detect(img, W, H, W * 4, x, y);

    [Fact]
    public void BlueRectOnWhite_IsFoundWithin3Px()
    {
        var img = Image(i => Fill(i, new RectPx(300, 200, 120, 80), 255, 0, 0));
        var rc = Detect(img, 350, 240);
        rc.Should().NotBeNull();
        rc!.Value.X.Should().BeCloseTo(300, 3);
        rc.Value.Y.Should().BeCloseTo(200, 3);
        rc.Value.Width.Should().BeCloseTo(120, 3);
        rc.Value.Height.Should().BeCloseTo(80, 3);
    }

    [Fact]
    public void ScatteredIconOnTwoToneBackground_IsJoinedByDilation()
    {
        var img = Image(i =>
        {
            Fill(i, new RectPx(0, 0, W, H / 2), 240, 240, 240); // 위 절반 밝은 회색, 아래 절반 흰색
            var rnd = new Random(7);
            for (var n = 0; n < 400; n++) // 40×40 안에 흩어진 어두운 점
                Set(i, 500 + rnd.Next(40), 100 + rnd.Next(40), 20, 20, 20);
            // 모서리는 확실히 채워 경계가 40×40이 되게
            Set(i, 500, 100, 20, 20, 20); Set(i, 539, 139, 20, 20, 20);
        });
        var rc = Detect(img, 520, 120);
        rc.Should().NotBeNull();
        rc!.Value.Width.Should().BeCloseTo(40, 4);
        rc.Value.Height.Should().BeCloseTo(40, 4);
    }

    [Fact]
    public void PlainBackground_ReturnsNull()
    {
        var img = Image(i => Fill(i, new RectPx(10, 10, 20, 20), 0, 0, 255));
        Detect(img, 600, 400).Should().BeNull();
    }

    [Fact]
    public void RegionLargerThan80Percent_ReturnsNull()
    {
        var img = Image(i => Fill(i, new RectPx(20, 20, 700, 520), 0, 128, 0));
        Detect(img, 400, 300).Should().BeNull();
    }

    [Fact]
    public void SuccessRateReport()
    {
        // T43 데이터셋이 아직 없어 합성 이미지 4종으로 성공률을 낸다.
        var cases = new (Func<byte[]> Img, int X, int Y, bool ExpectHit)[]
        {
            (() => Image(i => Fill(i, new RectPx(300, 200, 120, 80), 255, 0, 0)), 350, 240, true),
            (() => Image(i => Fill(i, new RectPx(100, 100, 64, 64), 0, 0, 0)), 130, 130, true),
            (() => Image(i => Fill(i, new RectPx(10, 10, 20, 20), 0, 0, 255)), 600, 400, false),
            (() => Image(i => Fill(i, new RectPx(20, 20, 700, 520), 0, 128, 0)), 400, 300, false),
        };
        var ok = cases.Count(c => (Detect(c.Img(), c.X, c.Y) is not null) == c.ExpectHit);
        output.WriteLine($"ImageRegionDetector synthetic success {ok}/{cases.Length}");
        ok.Should().Be(cases.Length);
    }
}
