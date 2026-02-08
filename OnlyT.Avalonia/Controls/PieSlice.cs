using System;
using Avalonia;
using Avalonia.Media;

namespace OnlyT.Avalonia.Controls;

internal static class PieSlice
{
    public static PathGeometry Get(double angle, Point centrePt, int innerRadius, int outerRadius)
    {
        var figure = new PathFigure
        {
            StartPoint = GetStartPoint(angle, centrePt, innerRadius),
            IsClosed = true,
            IsFilled = true
        };

        figure.Segments!.Add(CreateLine1(angle, centrePt, outerRadius));
        figure.Segments.Add(CreateArc1(angle, centrePt, outerRadius));
        figure.Segments.Add(CreateLine2(centrePt, innerRadius));
        figure.Segments.Add(CreateArc2(angle, centrePt, innerRadius));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.FillRule = FillRule.EvenOdd;
        return geometry;
    }

    private static Point GetStartPoint(double angle, Point centrePt, int innerRadius)
    {
        var radians = angle * (Math.PI / 180);
        var startX = centrePt.X + (Math.Sin(radians) * innerRadius);
        var startY = centrePt.Y - (Math.Cos(radians) * innerRadius);
        return new Point(startX, startY);
    }

    private static LineSegment CreateLine1(double angle, Point centrePt, int outerRadius)
    {
        var radians = angle * (Math.PI / 180);
        var endX = centrePt.X + (Math.Sin(radians) * outerRadius);
        var endY = centrePt.Y - (Math.Cos(radians) * outerRadius);
        return new LineSegment { Point = new Point(endX, endY) };
    }

    private static ArcSegment CreateArc1(double angle, Point centrePt, int outerRadius)
    {
        var endX = centrePt.X;
        var endY = centrePt.Y - outerRadius;
        return new ArcSegment
        {
            Point = new Point(endX, endY),
            Size = new Size(outerRadius, outerRadius),
            RotationAngle = 360 - angle,
            IsLargeArc = angle < 180,
            SweepDirection = SweepDirection.Clockwise
        };
    }

    private static LineSegment CreateLine2(Point centrePt, int innerRadius)
    {
        var endX = centrePt.X;
        var endY = centrePt.Y - innerRadius;
        return new LineSegment { Point = new Point(endX, endY) };
    }

    private static ArcSegment CreateArc2(double angle, Point centrePt, int innerRadius)
    {
        var endPt = GetStartPoint(angle, centrePt, innerRadius);
        return new ArcSegment
        {
            Point = endPt,
            Size = new Size(innerRadius, innerRadius),
            RotationAngle = 360 - angle,
            IsLargeArc = angle < 180,
            SweepDirection = SweepDirection.CounterClockwise
        };
    }
}
