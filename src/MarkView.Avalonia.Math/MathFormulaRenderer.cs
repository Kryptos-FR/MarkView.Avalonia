// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

using CSharpMath.SkiaSharp;

using SkiaSharp;

namespace MarkView.Avalonia.Math;

/// <summary>
/// Renders a LaTeX formula to a bitmap via CSharpMath.SkiaSharp. Shared by
/// <see cref="MathBlockRenderer"/> and <see cref="MathInlineRenderer"/>.
/// </summary>
internal static class MathFormulaRenderer
{
    private const int MaxLatexLength = 10_000;
    private const int MaxBraceNestingDepth = 50;

    public static Bitmap Render(string latex, SKColor textColor, float fontSize = 16f)
    {
        EnsureSafeToRender(latex);

        var painter = new MathPainter
        {
            LaTeX = latex,
            FontSize = fontSize,
            TextColor = textColor,
            DisplayErrorInline = true, // bad LaTeX renders its own error text instead of throwing
        };

        // NOTE: DrawAsStream() has never returned null in empirical testing (even for "" or
        // whitespace-only input) — this is defensive against a future CSharpMath prerelease
        // behavior change, not a currently-reachable path.
        using var stream = painter.DrawAsStream()
            ?? throw new InvalidOperationException("CSharpMath failed to render the formula to a stream.");
        return new Bitmap(stream);
    }

    /// <summary>
    /// CSharpMath.SkiaSharp's typesetter recurses per nesting level (confirmed: deeply nested
    /// \frac/\sqrt overflow the stack). A StackOverflowException there is uncatchable and kills
    /// the whole host process, not just this control — and MarkdownViewer.Source accepts http(s)
    /// URLs, so rendering untrusted/remote markdown is a supported scenario. Reject pathological
    /// input before it ever reaches CSharpMath, so the existing (catchable) fallback path handles
    /// it instead.
    /// </summary>
    private static void EnsureSafeToRender(string latex)
    {
        if (latex.Length > MaxLatexLength)
            throw new InvalidOperationException($"LaTeX source exceeds the {MaxLatexLength}-character safety limit.");

        int depth = 0, maxDepth = 0;
        foreach (var c in latex)
        {
            if (c == '{') { depth++; if (depth > maxDepth) maxDepth = depth; }
            else if (c == '}') { depth--; }
        }
        if (maxDepth > MaxBraceNestingDepth)
            throw new InvalidOperationException($"LaTeX source exceeds the {MaxBraceNestingDepth}-level brace-nesting safety limit.");
    }

    /// <summary>
    /// Matches MarkView.Avalonia.Mermaid's exact two-hardcoded-hex-per-theme convention.
    /// </summary>
    public static SKColor GetThemeTextColor() =>
        Application.Current?.ActualThemeVariant == ThemeVariant.Dark
            ? SKColor.Parse("#FAFAFA")
            : SKColor.Parse("#27272A");
}
