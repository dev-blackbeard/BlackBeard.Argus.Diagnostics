using Argus.Contracts;
using Argus.Graphics;
using Microsoft.Maui.Graphics;

namespace Argus.Controls.Maui;

/// <summary>Adapts <see cref="AlarmIconPainter"/> to <see cref="IDrawable"/>, for a <c>GraphicsView</c>.</summary>
internal sealed class AlarmIconDrawable : IDrawable
{
    private readonly HealthFlags _flag;
    private readonly Color _color;

    /// <summary>Creates a drawable for one flag's icon.</summary>
    /// <param name="flag">The flag to draw.</param>
    /// <param name="color">The colour to draw it in.</param>
    public AlarmIconDrawable(HealthFlags flag, Color color)
    {
        _flag = flag;
        _color = color;
    }

    /// <inheritdoc />
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        AlarmIconPainter.Draw(canvas, dirtyRect, _flag, _color);
    }
}
