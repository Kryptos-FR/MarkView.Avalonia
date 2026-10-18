// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

using MarkView.Avalonia.Math;

using Xunit;

namespace MarkView.Avalonia.Math.Tests;

/// <summary>Formulas follow the viewer's text scale: they are drawn again at the scaled size, not stretched.</summary>
public class MathTextScaleTests
{
    private static Bitmap Formula(MarkdownViewer viewer, string imageClass) =>
        Assert.IsAssignableFrom<Bitmap>(viewer.GetVisualDescendants().OfType<Image>()
            .Single(i => i.Classes.Contains(imageClass) || i.Parent is Border b && b.Classes.Contains(imageClass)).Source);

    [AvaloniaTheory]
    [InlineData("Inline $x^2+y^2$ formula", "markdown-math-inline")]
    [InlineData("$$\nx^2+y^2\n$$", "markdown-math-block")]
    public void A_formula_is_drawn_again_at_the_scaled_size(string markdown, string imageClass)
    {
        var viewer = new MarkdownViewer().UseMath();
        viewer.Markdown = markdown;
        var window = new Window { Content = viewer };
        window.Show();
        try
        {
            var unscaled = Formula(viewer, imageClass).PixelSize;

            viewer.TextScale = 2;
            var scaled = Formula(viewer, imageClass).PixelSize;

            Assert.InRange(scaled.Width, unscaled.Width * 2 - 4, unscaled.Width * 2 + 4);
            Assert.InRange(scaled.Height, unscaled.Height * 2 - 4, unscaled.Height * 2 + 4);
        }
        finally
        {
            window.Close();
        }
    }
}
