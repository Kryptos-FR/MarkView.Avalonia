// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// The icon of a copy button: a <see cref="PathIcon"/> that draws its <see cref="PathIcon.Data"/> itself
/// instead of through a template.
/// </summary>
/// <remarks>
/// A document has one copy button per code block and display formula, and the template of a
/// <see cref="PathIcon"/> (a border, a view box and a path) is most of what each one costs. This icon
/// looks the same: the geometry is scaled uniformly to fit and centred, in the
/// <see cref="TemplatedControl.Foreground"/>. It is styled as a <see cref="PathIcon"/>, so the styles
/// that give the icon its geometry and size still apply.
/// </remarks>
internal sealed class CopyIcon : PathIcon
{
    static CopyIcon()
    {
        AffectsRender<CopyIcon>(DataProperty, ForegroundProperty);
    }

    public CopyIcon()
    {
        // A local value wins over the template the application's theme gives every PathIcon.
        Template = null;
    }

    protected override Type StyleKeyOverride => typeof(PathIcon);

    public override void Render(DrawingContext context)
    {
        if (Data is not { } data || Foreground is not { } brush)
            return;
        var bounds = data.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        var size = Bounds.Size;
        var scale = Math.Min(size.Width / bounds.Width, size.Height / bounds.Height);
        var offset = new Vector(
            (size.Width - bounds.Width * scale) / 2 - bounds.X * scale,
            (size.Height - bounds.Height * scale) / 2 - bounds.Y * scale);
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset)))
            context.DrawGeometry(brush, null, data);
    }
}
