using System;
using System.Collections.Generic;
using Argus.Contracts;

namespace Argus.Graphics;

/// <summary>The basic shape drawn for a flag's icon.</summary>
/// <remarks>
/// Kept separate from <see cref="HealthFlags"/> itself because several flags can legitimately
/// share a shape (none do yet, but nothing stops it), and because <see cref="GetGlyphKind"/>
/// gives a way to test "which shape does this flag get" without needing an <c>ICanvas</c> at
/// all.
/// </remarks>
public enum AlarmGlyphKind
{
    /// <summary>No detector-specific shape assigned yet — a plain outlined circle.</summary>
    Placeholder,

    /// <summary>Two clock hands pointing backward. <see cref="HealthFlags.NonPositiveDeltaTime"/>.</summary>
    ReverseClock,

    /// <summary>Two overlapping squares. <see cref="HealthFlags.DuplicateSample"/>.</summary>
    Duplicate,

    /// <summary>Three misaligned horizontal bars. <see cref="HealthFlags.OutOfOrderSequence"/>.</summary>
    Shuffled,

    /// <summary>Two touching circles, read as an infinity loop. <see cref="HealthFlags.NonFiniteValue"/>.</summary>
    Infinity,

    /// <summary>A broken line with an arrowhead, jumping a gap. <see cref="HealthFlags.Teleport"/>.</summary>
    JumpArrow,

    /// <summary>A chevron pointing past its own bounds. <see cref="HealthFlags.ImplausibleSpeed"/>.</summary>
    Chevron,

    /// <summary>A squashed ellipse, not a circle. <see cref="HealthFlags.NonNormalisedQuaternion"/>.</summary>
    SquashedCircle,

    /// <summary>A ring of dots with one dot set apart. <see cref="HealthFlags.GroupOutlier"/>.</summary>
    Outlier,
}

/// <summary>
/// Draws a small, code-defined vector glyph per <see cref="HealthFlags"/>, so a legend or a
/// per-row alarm chip has something more recognisable than a colour alone to key on.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not image assets. A bitmap or SVG per flag is a second asset pipeline to
/// maintain, ships as binary content in the package, and is exactly the kind of MAUI resource
/// handling that has cost this project real CI time before (see the idiosyncrasies journal). A
/// shape drawn with a handful of <c>Microsoft.Maui.Graphics.ICanvas</c> primitives needs none of
/// that, recolours for free (the same colour the row's severity already resolved to, via
/// <see cref="ColorPolicy.GetColorForFlag"/>), and is trivial to extend.
/// </para>
/// <para>
/// Only the detectors <c>Argus.Core</c> has actually implemented get a distinct shape today; the
/// rest fall back to <see cref="AlarmGlyphKind.Placeholder"/> to keep this type total over every
/// flag rather than throwing. <c>Argus.Controls.LegendCatalogue</c> only lists the implemented
/// ones, so the placeholder is not normally seen — it exists for forward-compatibility, so
/// drawing an as-yet-unassigned flag degrades instead of failing. Give a newly implemented
/// detector's flag a real entry in <see cref="Glyphs"/> here alongside its golden case upstream.
/// </para>
/// </remarks>
public static class AlarmIconPainter
{
    private static readonly Dictionary<HealthFlags, AlarmGlyphKind> Glyphs = new Dictionary<HealthFlags, AlarmGlyphKind>
    {
        [HealthFlags.NonPositiveDeltaTime] = AlarmGlyphKind.ReverseClock,
        [HealthFlags.DuplicateSample] = AlarmGlyphKind.Duplicate,
        [HealthFlags.OutOfOrderSequence] = AlarmGlyphKind.Shuffled,
        [HealthFlags.NonFiniteValue] = AlarmGlyphKind.Infinity,
        [HealthFlags.Teleport] = AlarmGlyphKind.JumpArrow,
        [HealthFlags.ImplausibleSpeed] = AlarmGlyphKind.Chevron,
        [HealthFlags.NonNormalisedQuaternion] = AlarmGlyphKind.SquashedCircle,
        [HealthFlags.GroupOutlier] = AlarmGlyphKind.Outlier,
    };

    /// <summary>The shape assigned to a flag, or <see cref="AlarmGlyphKind.Placeholder"/> if none is yet.</summary>
    /// <param name="flag">A single-bit flag value.</param>
    public static AlarmGlyphKind GetGlyphKind(HealthFlags flag)
    {
        AlarmGlyphKind kind;
        return Glyphs.TryGetValue(flag, out kind) ? kind : AlarmGlyphKind.Placeholder;
    }

    /// <summary>Draws a flag's icon into <paramref name="bounds"/> of <paramref name="canvas"/>.</summary>
    /// <param name="canvas">The canvas to draw into.</param>
    /// <param name="bounds">The area to draw within. Square bounds read best; nothing here requires it.</param>
    /// <param name="flag">The flag whose icon to draw.</param>
    /// <param name="color">The colour to draw it in — typically <see cref="ColorPolicy.GetColorForFlag"/>'s answer for the same flag, so the icon and the row's severity colour agree.</param>
    public static void Draw(ICanvas canvas, RectF bounds, HealthFlags flag, Color color)
    {
        Draw(canvas, bounds, GetGlyphKind(flag), color);
    }

    /// <summary>Draws a specific shape into <paramref name="bounds"/> of <paramref name="canvas"/>.</summary>
    /// <param name="canvas">The canvas to draw into.</param>
    /// <param name="bounds">The area to draw within.</param>
    /// <param name="kind">The shape to draw.</param>
    /// <param name="color">The colour to draw it in.</param>
    public static void Draw(ICanvas canvas, RectF bounds, AlarmGlyphKind kind, Color color)
    {
        canvas.StrokeColor = color;
        canvas.FillColor = color;
        canvas.StrokeSize = Math.Max(1f, bounds.Width * 0.08f);

        switch (kind)
        {
            case AlarmGlyphKind.ReverseClock:
                DrawReverseClock(canvas, bounds);
                break;
            case AlarmGlyphKind.Duplicate:
                DrawDuplicate(canvas, bounds);
                break;
            case AlarmGlyphKind.Shuffled:
                DrawShuffled(canvas, bounds);
                break;
            case AlarmGlyphKind.Infinity:
                DrawInfinity(canvas, bounds);
                break;
            case AlarmGlyphKind.JumpArrow:
                DrawJumpArrow(canvas, bounds);
                break;
            case AlarmGlyphKind.Chevron:
                DrawChevron(canvas, bounds);
                break;
            case AlarmGlyphKind.SquashedCircle:
                DrawSquashedCircle(canvas, bounds);
                break;
            case AlarmGlyphKind.Outlier:
                DrawOutlier(canvas, bounds);
                break;
            default:
                DrawPlaceholder(canvas, bounds);
                break;
        }
    }

    private static void DrawReverseClock(ICanvas canvas, RectF b)
    {
        float cx = b.X + (b.Width / 2f);
        float cy = b.Y + (b.Height / 2f);
        float r = (Math.Min(b.Width, b.Height) / 2f) * 0.8f;

        canvas.DrawEllipse(cx - r, cy - r, r * 2, r * 2);
        canvas.DrawLine(cx, cy, cx - (r * 0.5f), cy + (r * 0.5f));
        canvas.DrawLine(cx, cy, cx, cy - (r * 0.7f));
    }

    private static void DrawDuplicate(ICanvas canvas, RectF b)
    {
        float w = b.Width * 0.55f;
        float h = b.Height * 0.55f;
        float offset = b.Width * 0.2f;

        canvas.DrawRectangle(b.X + offset, b.Y, w, h);
        canvas.DrawRectangle(b.X, b.Y + offset, w, h);
    }

    private static void DrawShuffled(ICanvas canvas, RectF b)
    {
        float y1 = b.Y + (b.Height * 0.22f);
        float y2 = b.Y + (b.Height * 0.5f);
        float y3 = b.Y + (b.Height * 0.78f);

        canvas.DrawLine(b.X, y1, b.X + (b.Width * 0.5f), y1);
        canvas.DrawLine(b.X, y2, b.X + b.Width, y2);
        canvas.DrawLine(b.X, y3, b.X + (b.Width * 0.75f), y3);
    }

    private static void DrawInfinity(ICanvas canvas, RectF b)
    {
        float r = b.Height * 0.28f;
        float cy = b.Y + (b.Height / 2f);
        float cx1 = b.X + (b.Width / 2f) - (r * 0.6f);
        float cx2 = b.X + (b.Width / 2f) + (r * 0.6f);

        canvas.DrawEllipse(cx1 - r, cy - r, r * 2, r * 2);
        canvas.DrawEllipse(cx2 - r, cy - r, r * 2, r * 2);
    }

    private static void DrawJumpArrow(ICanvas canvas, RectF b)
    {
        float y = b.Y + (b.Height / 2f);

        canvas.DrawLine(b.X, y, b.X + (b.Width * 0.35f), y);
        canvas.DrawLine(b.X + (b.Width * 0.55f), y, (b.X + b.Width) - (b.Width * 0.18f), y);

        var arrowhead = new PathF();
        arrowhead.MoveTo((b.X + b.Width) - (b.Width * 0.22f), y - (b.Height * 0.14f));
        arrowhead.LineTo(b.X + b.Width, y);
        arrowhead.LineTo((b.X + b.Width) - (b.Width * 0.22f), y + (b.Height * 0.14f));
        canvas.DrawPath(arrowhead);
    }

    private static void DrawChevron(ICanvas canvas, RectF b)
    {
        var chevron = new PathF();
        chevron.MoveTo(b.X + (b.Width * 0.22f), b.Y + (b.Height * 0.18f));
        chevron.LineTo((b.X + b.Width) - (b.Width * 0.22f), b.Y + (b.Height / 2f));
        chevron.LineTo(b.X + (b.Width * 0.22f), (b.Y + b.Height) - (b.Height * 0.18f));
        canvas.DrawPath(chevron);
    }

    private static void DrawSquashedCircle(ICanvas canvas, RectF b)
    {
        float w = b.Width * 0.8f;
        float h = b.Height * 0.45f;

        canvas.DrawEllipse(b.X + ((b.Width - w) / 2f), b.Y + ((b.Height - h) / 2f), w, h);
    }

    private static void DrawOutlier(ICanvas canvas, RectF b)
    {
        float cx = b.X + (b.Width / 2f);
        float cy = b.Y + (b.Height / 2f);
        float r = b.Width * 0.22f;
        float dot = b.Width * 0.09f;

        for (int i = 0; i < 5; i++)
        {
            double angle = i * (2 * Math.PI / 5);
            float x = cx + (r * (float)Math.Cos(angle));
            float y = cy + (r * (float)Math.Sin(angle));
            canvas.FillEllipse(x - dot, y - dot, dot * 2, dot * 2);
        }

        float outlierX = cx + (r * 2.1f);
        canvas.FillEllipse(outlierX - dot, cy - dot, dot * 2, dot * 2);
    }

    private static void DrawPlaceholder(ICanvas canvas, RectF b)
    {
        canvas.DrawEllipse(b.X + (b.Width * 0.2f), b.Y + (b.Height * 0.2f), b.Width * 0.6f, b.Height * 0.6f);
    }
}
