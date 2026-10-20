// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia;
using MarkView.Avalonia.Math;
using MarkView.Avalonia.Mermaid;
using MarkView.Avalonia.Svg;
using MarkView.Avalonia.SyntaxHighlighting;
using MarkView.Avalonia;
using MarkView.Benchmarks.Shared;
using Markdig;

namespace MarkView.Benchmarks;

public sealed class MarkViewAdapter : IViewerAdapter
{
    // Built once, as an application would; building a pipeline is not part of showing a document.
    private static readonly MarkdownPipeline PlainPipeline = new MarkdownPipelineBuilder().Build();
    private static readonly MarkdownPipeline SupportedPipeline = new MarkdownPipelineBuilder().UseSupportedExtensions().Build();
    private static readonly MarkdownPipeline GitHubPipeline = new MarkdownPipelineBuilder().UseGitHubExtensions().Build();

    // One extension instance for every viewer, as MarkdownViewerDefaults.Extensions.AddTextMateHighlighting() does:
    // its highlighter (themes and grammars) is then built once per application instead of once per viewer.
    private static readonly TextMateExtension SharedTextMate = new();

    public string Library => LibraryVersion.Of(typeof(MarkdownViewer), "MarkView.Avalonia");

    /// <summary>
    /// The three Markdig presets on the core package, then each extension package on top of the default
    /// (GitHub) preset, then all of them. TextMate comes twice: set up per viewer (<c>viewer.UseTextMateHighlighting()</c>)
    /// and shared by all viewers.
    /// </summary>
    public IReadOnlyList<string> Configurations { get; } =
        ["Plain", "Supported", "GitHub", "GitHub+TextMate", "GitHub+SharedTextMate", "GitHub+Mermaid", "GitHub+Math", "GitHub+Svg", "GitHub+All"];

    public void AddStyles(Styles styles)
    {
        var baseUri = new Uri("avares://MarkView.Benchmarks/");
        styles.Add(new StyleInclude(baseUri) { Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml") });
        styles.Add(new StyleInclude(baseUri) { Source = new Uri("avares://MarkView.Avalonia.Mermaid/Themes/MermaidTheme.axaml") });
    }

    public Control CreateViewer(string configuration)
    {
        var viewer = new MarkdownViewer();
        switch (configuration)
        {
            case "Plain": viewer.Pipeline = PlainPipeline; break;
            case "Supported": viewer.Pipeline = SupportedPipeline; break;
            case "GitHub": viewer.Pipeline = GitHubPipeline; break;
            case "GitHub+TextMate": viewer.Pipeline = GitHubPipeline; viewer.UseTextMateHighlighting(); break;
            case "GitHub+SharedTextMate": viewer.Pipeline = GitHubPipeline; viewer.Extensions.Add(SharedTextMate); break;
            case "GitHub+Mermaid": viewer.Pipeline = GitHubPipeline; viewer.UseMermaid(); break;
            case "GitHub+Math": viewer.UseMath(); break;
            case "GitHub+Svg": viewer.Pipeline = GitHubPipeline; viewer.UseSvg(); break;
            case "GitHub+All": viewer.UseTextMateHighlighting().UseMermaid().UseSvg().UseMath(); break;
            default: throw new ArgumentOutOfRangeException(nameof(configuration), configuration, null);
        }
        return viewer;
    }

    public void SetMarkdown(Control viewer, string markdown) => ((MarkdownViewer)viewer).Markdown = markdown;
}
