// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

using Markdig.Extensions.Mathematics;
using Markdig.Renderers;

using MarkView.Avalonia.Math.Parsing;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

namespace MarkView.Avalonia.Math;

/// <summary>
/// Renders Markdig <see cref="MathBlock"/> nodes (<c>$$...$$</c>, and <c>\[...\]</c> on lines of its own
/// once <c>UseMathBracketDelimiters()</c> is enabled) as a centred image
/// produced by CSharpMath.SkiaSharp. While a document is streaming, a block that is still provisional
/// (see <see cref="AvaloniaRenderer.IsProvisional"/>) is rendered as a plain code block, its source as
/// text, until a blank line or another block follows it. With <see cref="AvaloniaRenderer.ShowCodeBlockCopyButton"/>,
/// the formula is written inside a <c>markdown-math-container</c> grid, with a copy button for its TeX source
/// in the top right corner.
/// </summary>
public sealed class MathBlockRenderer : AvaloniaObjectRenderer<MathBlock>
{
    protected override void Write(AvaloniaRenderer renderer, MathBlock obj)
    {
        if (renderer.IsProvisional(obj) && FallbackRenderer(renderer, obj) is { } fallback)
        {
            fallback.Write(renderer, obj);
            return;
        }

        var source = ExtractSource(obj);

        var image = new Image
        {
            Stretch = Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        // Screen readers read the TeX source, as they do a formula's alt text on the web.
        AutomationProperties.SetName(image, source);

        var border = new Border { Child = image };
        border.Classes.Add("markdown-math-block");
        MarkdownSelection.SetCopyText(border, obj is BracketMathBlock ? $"\\[\n{source}\n\\]" : $"$$\n{source}\n$$");

        ThemeTracking.ReapplyOnThemeChange(border, ApplyTheme);

        ApplyTheme();
        renderer.WriteBlock(renderer.ShowCodeBlockCopyButton ? WithCopyButton(border, source) : border);

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

    /// <summary>
    /// Puts <paramref name="border"/> in a <c>markdown-math-container</c> grid, under a copy button for
    /// <paramref name="source"/> in its top right corner. The button is a <c>markdown-code-copy</c> like a
    /// code block's, so it looks and behaves the same; its <c>markdown-math-copy</c> class gives it its
    /// own tooltip and accessible name. Text selection skips it, as it does a code block's toolbar.
    /// </summary>
    private static Grid WithCopyButton(Border border, string source)
    {
        var button = CodeBlockToolbar.CreateCopyButton(source, MarkdownCopyOrigin.MathBlock);
        // Its place in the corner comes from the theme, like the rest of its look.
        button.Classes.Add("markdown-math-copy");

        var container = new Grid { Children = { border, button } };
        container.Classes.Add("markdown-math-container");
        return container;
    }

    /// <summary>
    /// Finds the renderer that would render <paramref name="block"/> as a fenced code block, whichever
    /// package registered it, so a half-streamed formula never reaches CSharpMath.
    /// </summary>
    /// <returns>The first registered renderer other than a <see cref="MathBlockRenderer"/> that accepts the block's type, or <c>null</c> when none does.</returns>
    private static IMarkdownObjectRenderer? FallbackRenderer(AvaloniaRenderer renderer, MathBlock block)
    {
        var type = block.GetType();
        return renderer.ObjectRenderers.FirstOrDefault(r => r is not MathBlockRenderer && r.Accept(renderer, type));
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
