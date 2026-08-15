// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls.Documents;

using CSharpMath.Avalonia;

using Markdig.Extensions.Mathematics;

using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Math;

/// <summary>
/// Renders Markdig <see cref="MathInline"/> nodes (<c>$...$</c>) as an inline
/// <see cref="MathView"/> produced by CSharpMath.Avalonia.
/// </summary>
public sealed class MathInlineRenderer : AvaloniaObjectRenderer<MathInline>
{
    protected override void Write(AvaloniaRenderer renderer, MathInline obj)
    {
        var source = obj.Content.ToString();

        MathView mathView;
        try
        {
            MathFormulaRenderer.EnsureSafeToRender(source);

            mathView = new MathView
            {
                FontSize = 16f,
                DisplayErrorInline = true,
                TextColor = MathFormulaRenderer.GetThemeTextColor(),
                LaTeX = source,
            };
        }
        catch (Exception)
        {
            renderer.WriteInline(new Run(source));
            return;
        }
        mathView.Classes.Add("markdown-math-inline");

        void OnThemeChanged(object? s, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name != nameof(Application.ActualThemeVariant)) return;
            mathView.TextColor = MathFormulaRenderer.GetThemeTextColor();
        }
        Application.Current!.PropertyChanged += OnThemeChanged;
        mathView.DetachedFromLogicalTree += (_, _) =>
            Application.Current?.PropertyChanged -= OnThemeChanged;

        renderer.WriteInline(mathView);
    }
}
