// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace MarkView.Avalonia.Math;

/// <summary>
/// Safety checks and theme colour lookup shared by <see cref="MathBlockRenderer"/> and
/// <see cref="MathInlineRenderer"/>.
/// </summary>
internal static class MathFormulaRenderer
{
    private const int MaxLatexLength = 10_000;
    private const int MaxBraceNestingDepth = 50;

    /// <summary>
    /// CSharpMath's typesetter (shared by every front end, including CSharpMath.Avalonia)
    /// recurses per nesting level (confirmed: deeply nested \frac/\sqrt overflow the stack). A
    /// StackOverflowException there is uncatchable and kills the whole host process, not just
    /// this control — and MarkdownViewer.Source accepts http(s) URLs, so rendering
    /// untrusted/remote markdown is a supported scenario. Reject pathological input before it
    /// ever reaches CSharpMath, so the existing (catchable) fallback path handles it instead.
    /// </summary>
    public static void EnsureSafeToRender(string latex)
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
    public static Color GetThemeTextColor() =>
        Application.Current?.ActualThemeVariant == ThemeVariant.Dark
            ? Color.Parse("#FAFAFA")
            : Color.Parse("#27272A");
}
