// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls.Documents;
using Avalonia.Media;

using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Demo.ColorExtension;

/// <summary>
/// Renders a <see cref="ColorSpanInline"/> as a <see cref="Run"/> with <see cref="Run.Foreground"/>
/// set from <see cref="ColorSpanInline.Color"/>. An unparseable colour name falls back to the
/// inherited foreground instead of throwing.
/// </summary>
public sealed class ColorSpanRenderer : AvaloniaObjectRenderer<ColorSpanInline>
{
    protected override void Write(AvaloniaRenderer renderer, ColorSpanInline obj)
    {
        var run = new Run(obj.Content);
        if (Color.TryParse(obj.Color, out var color))
            run.Foreground = new SolidColorBrush(color);

        renderer.WriteInline(run);
    }
}
