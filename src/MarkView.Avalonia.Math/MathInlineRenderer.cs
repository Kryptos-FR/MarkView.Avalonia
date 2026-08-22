// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
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

        if (!ApplyTheme(isFirstRender: true))
        {
            // First render failed before the image was ever written to the renderer's inline
            // stack — we're still inside the synchronous initial Write() call here, so writing a
            // plain-text fallback instead is safe (unlike from the later theme-change callback,
            // which must never re-enter the stack — see the comment inside ApplyTheme below).
            // We deliberately haven't subscribed to theme-change notifications yet at this point:
            // the discarded image is never attached to the visual tree, so DetachedFromLogicalTree
            // would never fire and an earlier subscription would leak the handler on
            // Application.Current forever.
            renderer.WriteInline(new Run(source));
            return;
        }

        void OnThemeChanged(object? s, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name != nameof(Application.ActualThemeVariant)) return;
            ApplyTheme(isFirstRender: false);
        }
        Application.Current!.PropertyChanged += OnThemeChanged;
        image.DetachedFromLogicalTree += (_, _) =>
            Application.Current?.PropertyChanged -= OnThemeChanged;

        renderer.WriteInline(image);

        bool ApplyTheme(bool isFirstRender)
        {
            try
            {
                image.Source = MathFormulaRenderer.Render(source, MathFormulaRenderer.GetThemeTextColor());
                return true;
            }
            catch (Exception)
            {
                if (!isFirstRender)
                {
                    // Inline context can't safely re-enter the renderer's inline stack from this
                    // async/theme-change callback — unlike the block renderer's Border, an inline
                    // Image has no room for a fallback panel. Leave the image showing its last
                    // successfully rendered bitmap rather than risk corrupting whatever inline
                    // collection is active at callback time.
                }
                return false;
            }
        }
    }
}
