// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

using Markdig.Extensions.Mathematics;

using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Math;

/// <summary>
/// Renders Markdig <see cref="MathInline"/> nodes (<c>$...$</c>) as an inline image
/// produced by CSharpMath.SkiaSharp.
/// </summary>
public sealed class MathInlineRenderer : AvaloniaObjectRenderer<MathInline>
{
    protected override void Write(AvaloniaRenderer renderer, MathInline obj)
    {
        var source = obj.Content.ToString();

        var image = new Image { Stretch = Stretch.None };
        image.Classes.Add("markdown-math-inline");

        void OnThemeChanged(object? s, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name != nameof(Application.ActualThemeVariant)) return;
            ApplyTheme();
        }
        Application.Current!.PropertyChanged += OnThemeChanged;
        image.DetachedFromLogicalTree += (_, _) =>
            Application.Current?.PropertyChanged -= OnThemeChanged;

        ApplyTheme();
        renderer.WriteInline(image);

        void ApplyTheme()
        {
            try
            {
                image.Source = MathFormulaRenderer.Render(source, MathFormulaRenderer.GetThemeTextColor());
            }
            catch (Exception)
            {
                // Inline context can't safely re-enter the renderer's inline stack from an
                // async/theme-change callback the way the block renderer's Border can mutate
                // its own Child — leave the image showing its last successfully rendered
                // bitmap (or blank, on first-render failure) rather than risk corrupting
                // whatever inline collection is active at callback time.
            }
        }
    }
}
