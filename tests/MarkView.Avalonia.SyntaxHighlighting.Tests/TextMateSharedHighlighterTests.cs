// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Runtime.CompilerServices;

using Avalonia.Headless.XUnit;
using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using TextMateSharp.Grammars;
using Xunit;

namespace MarkView.Avalonia.SyntaxHighlighting.Tests;

/// <summary>One highlighter per pair of themes, shared by every <see cref="TextMateExtension"/>.</summary>
public class TextMateSharedHighlighterTests
{
    private static ICodeHighlighter? Registered(TextMateExtension extension)
    {
        var renderer = new AvaloniaRenderer();
        extension.Register(renderer);
        return renderer.CodeHighlighter;
    }

    [AvaloniaFact]
    public void Extensions_with_the_same_themes_share_one_highlighter()
    {
        var first = new TextMateExtension(ThemeName.Monokai, ThemeName.QuietLight);
        var second = new TextMateExtension(ThemeName.Monokai, ThemeName.QuietLight);

        var highlighter = Registered(first);

        Assert.NotNull(highlighter);
        Assert.Same(highlighter, Registered(second));
        Assert.Same(highlighter, Registered(first));
        GC.KeepAlive(first);
    }

    [AvaloniaFact]
    public void Viewers_using_the_same_themes_share_one_highlighter()
    {
        var first = new MarkdownViewer().UseTextMateHighlighting();
        var second = new MarkdownViewer().UseTextMateHighlighting();

        Assert.Same(Registered((TextMateExtension)first.Extensions[0]), Registered((TextMateExtension)second.Extensions[0]));
    }

    [Fact]
    public void Each_pair_of_themes_has_its_own_highlighter()
    {
        var plus = TextMateExtension.GetSharedHighlighter(ThemeName.DarkPlus, ThemeName.LightPlus);
        var monokai = TextMateExtension.GetSharedHighlighter(ThemeName.Monokai, ThemeName.LightPlus);
        var swapped = TextMateExtension.GetSharedHighlighter(ThemeName.LightPlus, ThemeName.DarkPlus);

        Assert.NotSame(plus, monokai);
        Assert.NotSame(plus, swapped);
        Assert.NotSame(monokai, swapped);
        Assert.Same(plus, TextMateExtension.GetSharedHighlighter(ThemeName.DarkPlus, ThemeName.LightPlus));
    }

    [Fact]
    public void A_highlighter_no_extension_uses_is_released()
    {
        var released = Unused();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(released.IsAlive);
        Assert.NotNull(TextMateExtension.GetSharedHighlighter(ThemeName.KimbieDark, ThemeName.SolarizedLight));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference Unused() =>
        new(TextMateExtension.GetSharedHighlighter(ThemeName.KimbieDark, ThemeName.SolarizedLight));
}
