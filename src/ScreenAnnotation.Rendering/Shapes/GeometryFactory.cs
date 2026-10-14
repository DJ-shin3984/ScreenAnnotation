using System.Windows;
using System.Windows.Media;
using ScreenAnnotation.Core.Annotations;

namespace ScreenAnnotation.Rendering.Shapes;

/// <summary>Solid는 실선으로 그린다. Dashed는 원기둥 뒤쪽 반타원(점선)만. 좌표는 start/end와 같은 공간(물리 픽셀).</summary>
public sealed record ShapeGeometry(Geometry Solid, Geometry? Dashed);

public static class GeometryFactory
{
    private const double CylinderTopRatio = 0.2; // 윗면 타원 높이 = 상자 높이의 20%

    public static ShapeGeometry Build(ShapeKind kind, Point start, Point end, bool constrained, double strokeWidth)
    {
        if (constrained) (start, end) = Constrain(kind, start, end);
        var box = new Rect(start, end);
        return kind switch
        {
            ShapeKind.Rect => new(Frozen(c => c.DrawRect(box)), null),
            ShapeKind.Triangle => new(Frozen(c =>
            {
                c.BeginFigure(new Point(box.Left + box.Width / 2, box.Top), false, true);
                c.LineTo(box.BottomRight, true, false);
                c.LineTo(box.BottomLeft, true, false);
            }), null),
            ShapeKind.Ellipse => new(Frozen(c => c.DrawEllipse(box)), null),
            ShapeKind.Cylinder => BuildCylinder(box),
            ShapeKind.Arrow => new(BuildArrow(start, end, strokeWidth), null),
            ShapeKind.Line => new(Frozen(c => { c.BeginFigure(start, false, false); c.LineTo(end, true, false); }), null),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    /// <summary>Shift 규칙: 정사각형·정삼각형·정원·정원기둥, 화살표·직선은 45° 단위.</summary>
    public static (Point Start, Point End) Constrain(ShapeKind kind, Point start, Point end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        switch (kind)
        {
            case ShapeKind.Rect:
            case ShapeKind.Ellipse:
            case ShapeKind.Cylinder:
            {
                var s = Math.Max(Math.Abs(dx), Math.Abs(dy));
                return (start, new Point(start.X + Math.Sign(dx) * s, start.Y + Math.Sign(dy) * s));
            }
            case ShapeKind.Triangle:
            {
                var h = Math.Abs(dx) * Math.Sqrt(3) / 2; // 너비 유지, 높이 = 정삼각형
                return (start, new Point(end.X, start.Y + (dy < 0 ? -h : h)));
            }
            case ShapeKind.Arrow:
            case ShapeKind.Line:
            {
                var len = Math.Sqrt(dx * dx + dy * dy);
                if (len == 0) return (start, end);
                var angle = Math.Round(Math.Atan2(dy, dx) / (Math.PI / 4)) * (Math.PI / 4);
                return (start, new Point(start.X + Math.Cos(angle) * len, start.Y + Math.Sin(angle) * len));
            }
            default:
                return (start, end);
        }
    }

    private static ShapeGeometry BuildCylinder(Rect box)
    {
        var ry = box.Height * CylinderTopRatio / 2;
        var rx = box.Width / 2;
        var cx = box.Left + rx;
        var topCy = box.Top + ry;
        var botCy = box.Bottom - ry;

        var solid = Frozen(c =>
        {
            c.DrawEllipse(new Rect(box.Left, box.Top, box.Width, ry * 2));
            c.BeginFigure(new Point(box.Left, topCy), false, false);
            c.LineTo(new Point(box.Left, botCy), true, false);
            c.BeginFigure(new Point(box.Right, topCy), false, false);
            c.LineTo(new Point(box.Right, botCy), true, false);
            // 아랫면 앞쪽 반타원 (왼쪽 → 오른쪽, 아래로 볼록)
            c.BeginFigure(new Point(box.Left, botCy), false, false);
            c.ArcTo(new Point(box.Right, botCy), new Size(rx, ry), 0, false, SweepDirection.Counterclockwise, true, false);
        });
        var dashed = Frozen(c =>
        {
            // 아랫면 뒤쪽 반타원
            c.BeginFigure(new Point(box.Left, botCy), false, false);
            c.ArcTo(new Point(box.Right, botCy), new Size(rx, ry), 0, false, SweepDirection.Clockwise, true, false);
        });
        return new(solid, dashed);
    }

    private static Geometry BuildArrow(Point start, Point end, double strokeWidth)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len == 0) return Frozen(c => { c.BeginFigure(start, false, false); c.LineTo(end, true, false); });

        var head = Math.Max(8, strokeWidth * 4); // 머리 길이 = 굵기 비례
        var ux = dx / len; var uy = dy / len;
        var basePt = new Point(end.X - ux * head, end.Y - uy * head);
        var half = head / 2;
        var left = new Point(basePt.X - uy * half, basePt.Y + ux * half);
        var right = new Point(basePt.X + uy * half, basePt.Y - ux * half);

        // 머리는 닫힌 삼각형: 호출자가 Fill까지 해 준다.
        return Frozen(c =>
        {
            c.BeginFigure(start, false, false);
            c.LineTo(basePt, true, false);
            c.BeginFigure(end, true, true);
            c.LineTo(left, true, false);
            c.LineTo(right, true, false);
        });
    }

    private static StreamGeometry Frozen(Action<StreamGeometryContext> draw)
    {
        var g = new StreamGeometry();
        using (var c = g.Open()) draw(c);
        g.Freeze();
        return g;
    }

    private static void DrawRect(this StreamGeometryContext c, Rect r)
    {
        c.BeginFigure(r.TopLeft, false, true);
        c.LineTo(r.TopRight, true, false);
        c.LineTo(r.BottomRight, true, false);
        c.LineTo(r.BottomLeft, true, false);
    }

    private static void DrawEllipse(this StreamGeometryContext c, Rect r)
    {
        var size = new Size(r.Width / 2, r.Height / 2);
        var left = new Point(r.Left, r.Top + r.Height / 2);
        var right = new Point(r.Right, r.Top + r.Height / 2);
        c.BeginFigure(left, false, true);
        c.ArcTo(right, size, 0, false, SweepDirection.Clockwise, true, false);
        c.ArcTo(left, size, 0, false, SweepDirection.Clockwise, true, false);
    }
}
