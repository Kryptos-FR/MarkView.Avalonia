// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

using CSharpMath.Avalonia;

using Markdig.Extensions.Mathematics;

using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Math;

/// <summary>
/// Renders Markdig <see cref="MathBlock"/> nodes (<c>$$...$$</c>) as a centred
/// <see cref="MathView"/> produced by CSharpMath.Avalonia.
/// </summary>
public sealed class MathBlockRenderer : AvaloniaObjectRenderer<MathBlock>
{
    protected override void Write(AvaloniaRenderer renderer, MathBlock obj)
    {
        var source = ExtractSource(obj);

        try
        {
            MathFormulaRenderer.EnsureSafeToRender(source);

            var mathView = new MathView
            {
                FontSize = 16f,
                DisplayErrorInline = true,
                TextColor = MathFormulaRenderer.GetThemeTextColor(),
                LaTeX = source,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            var border = new Border { Child = mathView };
            border.Classes.Add("markdown-math-block");

            void OnThemeChanged(object? s, AvaloniaPropertyChangedEventArgs e)
            {
                if (e.Property.Name != nameof(Application.ActualThemeVariant)) return;
                mathView.TextColor = MathFormulaRenderer.GetThemeTextColor();
            }
            Application.Current!.PropertyChanged += OnThemeChanged;
            border.DetachedFromLogicalTree += (_, _) =>
                Application.Current?.PropertyChanged -= OnThemeChanged;

            renderer.WriteBlock(border);
        }
        catch (Exception ex)
        {
            var panel = new StackPanel { Spacing = 4 };
            panel.Children.Add(new TextBlock { Text = $"Math render error: {ex.Message}" });
            panel.Children.Add(new TextBlock { Text = source });
            var fallback = new Border { Child = panel };
            fallback.Classes.Add("markdown-math-fallback");
            renderer.WriteBlock(fallback);
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
