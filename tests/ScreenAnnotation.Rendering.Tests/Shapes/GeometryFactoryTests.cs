using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenAnnotation.Core.Annotations;
using ScreenAnnotation.Rendering.Shapes;

namespace ScreenAnnotation.Rendering.Tests.Shapes;

public class GeometryFactoryTests
{
    private static readonly Point S = new(20, 30);
    private static readonly Point E = new(140, 100);

    [Theory]
    [InlineData(ShapeKind.Rect)]
    [InlineData(ShapeKind.Triangle)]
    [InlineData(ShapeKind.Ellipse)]
    [InlineData(ShapeKind.Cylinder)]
    public void BoundsEqualNormalizedBox(ShapeKind kind)
    {
        var g = GeometryFactory.Build(kind, E, S, false, 2); // 역방향 드래그
        var b = g.Solid.Bounds;
        b.Left.Should().BeApproximately(20, 0.01);
        b.Top.Should().BeApproximately(30, 0.01);
        b.Right.Should().BeApproximately(140, 0.01);
        b.Bottom.Should().BeApproximately(100, 0.01);
    }

    [Fact]
    public void ConstrainMakesSquareAndCircle()
    {
        var (_, e) = GeometryFactory.Constrain(ShapeKind.Rect, S, E);
        e.Should().Be(new Point(140, 150));
        var (_, c) = GeometryFactory.Constrain(ShapeKind.Ellipse, S, new Point(-100, 40));
        c.Should().Be(new Point(-100, 150));
    }

    [Fact]
    public void ConstrainTriangleIsEquilateral()
    {
        var (_, e) = GeometryFactory.Constrain(ShapeKind.Triangle, new Point(0, 0), new Point(100, 10));
        e.X.Should().Be(100);
        e.Y.Should().BeApproximately(100 * Math.Sqrt(3) / 2, 0.001);
    }

    [Fact]
    public void ConstrainSnapsLineTo45Degrees()
    {
        var (_, e) = GeometryFactory.Constrain(ShapeKind.Line, new Point(0, 0), new Point(100, 10));
        e.X.Should().BeApproximately(Math.Sqrt(100 * 100 + 10 * 10), 0.001);
        e.Y.Should().BeApproximately(0, 0.001);
        var (_, d) = GeometryFactory.Constrain(ShapeKind.Arrow, new Point(0, 0), new Point(100, 90));
        d.X.Should().BeApproximately(d.Y, 0.001);
    }

    [Fact]
    public void CylinderHasDashedBackHalf()
    {
        GeometryFactory.Build(ShapeKind.Cylinder, S, E, false, 2).Dashed.Should().NotBeNull();
        GeometryFactory.Build(ShapeKind.Rect, S, E, false, 2).Dashed.Should().BeNull();
    }

    [Fact]
    public void ArrowReachesEndPoint()
    {
        var b = GeometryFactory.Build(ShapeKind.Arrow, S, E, false, 4).Solid.Bounds;
        b.Right.Should().BeApproximately(E.X, 0.01);
        b.Bottom.Should().BeGreaterThanOrEqualTo(E.Y - 0.01);
        b.Left.Should().BeLessThanOrEqualTo(S.X + 0.01);
    }

    [Fact]
    public void RenderMatchesExpectedImages()
    {
        var dir = Path.Combine(Path.GetDirectoryName(typeof(GeometryFactoryTests).Assembly.Location)!, "Shapes", "Expected");
        Exception? failure = null;
        var t = new Thread(() =>
        {
            try
            {
                foreach (var kind in Enum.GetValues<ShapeKind>())
                {
                    var actual = Render(kind);
                    var path = Path.Combine(dir, $"{kind}.png");
                    if (!File.Exists(path)) { Directory.CreateDirectory(dir); File.WriteAllBytes(path, actual); continue; }
                    File.ReadAllBytes(path).Should().Equal(actual, $"{kind} 렌더가 기대 이미지와 같아야 함");
                }
            }
            catch (Exception ex) { failure = ex; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        failure.Should().BeNull();
    }

    private static byte[] Render(ShapeKind kind)
    {
        var g = GeometryFactory.Build(kind, S, E, false, 3);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            var pen = new Pen(Brushes.Red, 3);
            dc.DrawGeometry(kind == ShapeKind.Arrow ? Brushes.Red : null, pen, g.Solid);
            if (g.Dashed is not null) dc.DrawGeometry(null, new Pen(Brushes.Red, 3) { DashStyle = DashStyles.Dash }, g.Dashed);
        }
        var rtb = new RenderTargetBitmap(200, 120, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }
}
