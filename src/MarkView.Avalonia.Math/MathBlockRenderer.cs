// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

using Markdig.Extensions.Mathematics;

using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Math;

/// <summary>
/// Renders Markdig <see cref="MathBlock"/> nodes (<c>$$...$$</c>) as a centred image
/// produced by CSharpMath.SkiaSharp.
/// </summary>
public sealed class MathBlockRenderer : AvaloniaObjectRenderer<MathBlock>
{
    protected override void Write(AvaloniaRenderer renderer, MathBlock obj)
    {
        var source = ExtractSource(obj);

        var image = new Image
        {
            Stretch = Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var border = new Border { Child = image };
        border.Classes.Add("markdown-math-block");

        void OnThemeChanged(object? s, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name != nameof(Application.ActualThemeVariant)) return;
            ApplyTheme();
        }
        Application.Current!.PropertyChanged += OnThemeChanged;
        border.DetachedFromLogicalTree += (_, _) =>
            Application.Current?.PropertyChanged -= OnThemeChanged;

        ApplyTheme();
        renderer.WriteBlock(border);

        void ApplyTheme()
        {
            try
            {
                image.Source = MathFormulaRenderer.Render(source, MathFormulaRenderer.GetThemeTextColor());
            }
            catch (Exception ex)
            {
                var panel = new StackPanel { Spacing = 4 };
                panel.Children.Add(new TextBlock { Text = $"Math render error: {ex.Message}" });
                panel.Children.Add(new TextBlock { Text = source });
                border.Child = panel;
                border.Classes.Clear();
                border.Classes.Add("markdown-math-fallback");
            }
        }
    }

    private static string ExtractSource(MathBlock block)
    {
        if (block.Lines.Lines == null)
            return string.Empty;

        var lines = block.Lines;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Count; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(lines.Lines[i].Slice.AsSpan());
        }
        return sb.ToString();
    }
}
