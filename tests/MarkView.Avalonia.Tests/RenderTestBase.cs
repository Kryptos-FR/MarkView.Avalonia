// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Markdig;
using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Tests;

public abstract class RenderTestBase
{
    /// <summary>
    /// Renders <paramref name="markdown"/> outside a viewer. With <paramref name="themed"/>, its controls get the
    /// control themes of <c>MarkdownTheme.axaml</c>, for a test that shows them in a window that includes it.
    /// </summary>
    protected static StackPanel Render(string markdown, MarkdownPipeline? pipeline = null, bool themed = false)
    {
        pipeline ??= new MarkdownPipelineBuilder().Build();
        var document = Markdown.Parse(markdown, pipeline);
        var renderer = new AvaloniaRenderer();
        if (themed)
            renderer.Themes = MarkdownThemes();
        pipeline.Setup(renderer);
        renderer.Render(document);
        return renderer.RootPanel;
    }

    /// <summary>
    /// The control themes of <c>MarkdownTheme.axaml</c>, for a renderer used outside a viewer, which otherwise
    /// finds them in the application's resources only.
    /// </summary>
    internal static ThemeLookup MarkdownThemes()
    {
        var theme = new StyleInclude((Uri?)null) { Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml") };
        return new ThemeLookup(key => theme.TryGetResource(key, null, out var value) ? value : null);
    }
}
