// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using LiveMarkdown.Avalonia;
using Markdig;
using MarkView.Benchmarks.Shared;

namespace LiveMarkdown.Benchmarks;

/// <summary>
/// LiveMarkdown.Avalonia as its README sets it up. It parses on a background thread and applies the result on
/// the UI thread, so a document is settled once the renderer's projection reports the builder's version.
/// Its Math and Mermaid nodes are registered process-wide, so a process measures one configuration only
/// (BenchmarkDotNet and the probe both run each case in its own process).
/// </summary>
public sealed class LiveMarkdownAdapter : IViewerAdapter
{
    private static string? _configured;
    private static MarkdownPipeline? _plainPipeline;

    public string Library => LibraryVersion.Of(typeof(MarkdownRenderer), "LiveMarkdown.Avalonia");

    /// <summary>
    /// "Plain" swaps in a Markdig pipeline with no extension (as MarkView's "Plain"); "Default" is the library's
    /// own pipeline, which always highlights code with TextMate; Math and Mermaid are its optional packages.
    /// </summary>
    public IReadOnlyList<string> Configurations { get; } = ["Plain", "Default", "Default+Mermaid", "Default+Math", "Default+All"];

    public void AddStyles(Styles styles)
    {
        var baseUri = new Uri("avares://LiveMarkdown.Benchmarks/");
        styles.Add(new StyleInclude(baseUri) { Source = new Uri("avares://LiveMarkdown.Avalonia/Styles.axaml") });
        styles.Add(new StyleInclude(baseUri) { Source = new Uri("avares://LiveMarkdown.Avalonia.Mermaid/Styles.axaml") });
        // The README merges Defaults.axaml into Application.Resources; StyleInclude-ing a ResourceDictionary is
        // not possible, so the harness adds it through a Styles' resources.
        var defaults = new Styles();
        defaults.Resources.MergedDictionaries.Add(new ResourceInclude(baseUri) { Source = new Uri("avares://LiveMarkdown.Avalonia/Defaults.axaml") });
        styles.Add(defaults);
    }

    public Control CreateViewer(string configuration)
    {
        ConfigureProcess(configuration);
        var renderer = new MarkdownRenderer();
        // The builder belongs to the update producer, so a replaced producer comes first.
        if (configuration == "Plain")
            renderer.UpdateProducer = new MarkdownUpdateProducer { Pipeline = _plainPipeline ??= new MarkdownPipelineBuilder().Build() };
        renderer.MarkdownBuilder = new ObservableStringBuilder();
        // The renderer does not scroll by itself; an application hosts it in a ScrollViewer.
        return new ScrollViewer { Content = renderer };
    }

    public void SetMarkdown(Control viewer, string markdown)
    {
        var builder = Renderer(viewer).MarkdownBuilder!;
        builder.Clear();
        builder.Append(markdown);
    }

    public bool IsSettled(Control viewer)
    {
        var renderer = Renderer(viewer);
        return renderer.RenderedTextProjection is { } projection && projection.SourceVersion == renderer.MarkdownBuilder!.Version;
    }

    private static MarkdownRenderer Renderer(Control viewer) => (MarkdownRenderer)((ScrollViewer)viewer).Content!;

    public static int PrintDefaultPipeline()
    {
        foreach (var extension in MarkdownUpdateProducer.DefaultPipeline.Extensions)
            Console.WriteLine(extension.GetType().FullName);
        return 0;
    }

    private static void ConfigureProcess(string configuration)
    {
        if (_configured is not null)
        {
            if (_configured != configuration)
                throw new InvalidOperationException($"This process is set up for {_configured}; LiveMarkdown registers its extensions process-wide.");
            return;
        }
        _configured = configuration;
        var mermaid = configuration is "Default+Mermaid" or "Default+All";
        var math = configuration is "Default+Math" or "Default+All";
        if (mermaid)
        {
            MarkdownRenderer.ConfigurePipeline += x => x.UseMermaid();
            MarkdownNode.Register<MermaidBlockNode>();
        }
        if (math)
        {
            MarkdownNode.Register<MathInlineNode>();
            MarkdownNode.Register<MathBlockNode>();
        }
    }
}
