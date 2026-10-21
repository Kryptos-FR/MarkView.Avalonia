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

/// <summary>
/// MarkView, compiled twice: against <c>src/</c> (MarkView.Benchmarks) and against the 12.3.0 packages from NuGet
/// (MarkView123.Benchmarks, <c>MARKVIEW_12_3</c>). The configurations both runners share have the same name and
/// mean the same thing for a user: "Default" is the viewer with no pipeline set, which was
/// <c>UseSupportedExtensions()</c> in 12.3.0 and is <c>UseGitHubExtensions()</c> since.
/// MarkViewBaseline.Benchmarks compiles it a third time, against the sources of an older commit, with the 12.3
/// configurations and the streaming session (<c>MARKVIEW_STREAMING</c>).
/// </summary>
public sealed class MarkViewAdapter : IViewerAdapter
{
    // Built once, as an application would; building a pipeline is not part of showing a document.
    private static readonly MarkdownPipeline PlainPipeline = new MarkdownPipelineBuilder().Build();
    private static readonly MarkdownPipeline SupportedPipeline = new MarkdownPipelineBuilder().UseSupportedExtensions().Build();

    private static readonly string[] Shared =
        ["Plain", "Supported", "Default", "Default+TextMate", "Default+Mermaid", "Default+Math", "Default+All"];

#if MARKVIEW_12_3
    public IReadOnlyList<string> Configurations { get; } = Shared;
#else
    // The default pipeline without the HTML subset and <details> (UseGitHubExtensions minus its two HTML extensions).
    private static readonly MarkdownPipeline GitHubNoHtmlPipeline = BuildGitHubNoHtml();

    private static readonly MarkdownPipeline ExtendedAlertsPipeline =
        new MarkdownPipelineBuilder().UseGitHubExtensions().UseExtendedAlertBlocks().Build();

    private static readonly MarkdownPipeline ThinkingPipeline =
        new MarkdownPipelineBuilder().UseGitHubExtensions().UseThinkingBlocks().Build();

    /// <summary>
    /// The shared configurations, then the default with each opt-out feature turned off ("Default-X"), all of them
    /// off ("Lean"), and the default with each opt-in feature turned on ("Default+X").
    /// </summary>
    public IReadOnlyList<string> Configurations { get; } =
    [
        .. Shared,
        "Default-Html", "Default-CodeToolbar", "Default-Rtl", "Default-ImageZoom", "Lean",
        "Default+ExtAlerts", "Default+FoldHeadings", "Default+Tasks", "Default+LineNumbers", "Default+Thinking",
        "Default+SmoothScroll",
        "Plain-CodeToolbar", "Plain-Rtl", "Plain-ContextMenu", "Plain-Lean",
    ];

    private static MarkdownPipeline BuildGitHubNoHtml()
    {
        var builder = new MarkdownPipelineBuilder();
        var strikethrough = typeof(MarkdownViewer).Assembly.GetType("MarkView.Avalonia.Parsing.GitHubStrikethroughExtension", throwOnError: true)!;
        builder.Extensions.Add((Markdig.IMarkdownExtension)Activator.CreateInstance(strikethrough)!);
        return builder
            .UseAlertBlocks()
            .UseAutoLinks()
            .UseCjkFriendlyEmphasis()
            .UseEmojiAndSmiley(enableSmileys: false)
            .UseFootnotes()
            .UsePipeTables()
            .UseTaskLists()
            .UseYamlFrontMatter()
            .Build();
    }
#endif

    public string Library => LibraryVersion.Of(typeof(MarkdownViewer), "MarkView.Avalonia");

    public void AddStyles(Styles styles)
    {
        var baseUri = new Uri("avares://MarkView.Benchmarks/");
        styles.Add(new StyleInclude(baseUri) { Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml") });
        styles.Add(new StyleInclude(baseUri) { Source = new Uri("avares://MarkView.Avalonia.Mermaid/Themes/MermaidTheme.axaml") });
        // BENCH_PRESET=GitHub|Chat|Docs adds a theme preset after the default theme, as an application would.
        if (Environment.GetEnvironmentVariable("BENCH_PRESET") is { Length: > 0 } preset)
            styles.Add(new StyleInclude(baseUri) { Source = new Uri($"avares://MarkView.Avalonia/Themes/{preset}Theme.axaml") });
    }

    public Control CreateViewer(string configuration)
    {
        var viewer = new MarkdownViewer();
        switch (configuration)
        {
            case "Plain": viewer.Pipeline = PlainPipeline; break;
            case "Supported": viewer.Pipeline = SupportedPipeline; break;
            case "Default": break;
            case "Default+TextMate": viewer.UseTextMateHighlighting(); break;
            case "Default+Mermaid": viewer.UseMermaid(); break;
            case "Default+Math": viewer.UseMath(); break;
            case "Default+All": viewer.UseTextMateHighlighting().UseMermaid().UseSvg().UseMath(); break;
#if !MARKVIEW_12_3
            case "Default-Html": viewer.Pipeline = GitHubNoHtmlPipeline; break;
            case "Default-CodeToolbar": viewer.ShowCodeBlockLanguage = false; viewer.ShowCodeBlockCopyButton = false; break;
            case "Default-Rtl": viewer.DetectTextDirection = false; break;
            case "Default-ImageZoom": viewer.IsImageZoomEnabled = false; break;
            case "Lean":
                viewer.Pipeline = GitHubNoHtmlPipeline;
                viewer.ShowCodeBlockLanguage = false;
                viewer.ShowCodeBlockCopyButton = false;
                viewer.DetectTextDirection = false;
                viewer.IsImageZoomEnabled = false;
                break;
            case "Default+ExtAlerts": viewer.Pipeline = ExtendedAlertsPipeline; break;
            case "Default+FoldHeadings": viewer.AreHeadingsFoldable = true; break;
            case "Default+Tasks": viewer.AreTaskListsInteractive = true; break;
            case "Default+LineNumbers": viewer.ShowCodeBlockLineNumbers = true; break;
            case "Default+Thinking": viewer.Pipeline = ThinkingPipeline; break;
            case "Default+SmoothScroll": viewer.IsSmoothScrollingEnabled = true; break;
            // The plain pipeline with the switches that have no 12.3 counterpart turned off: what remains of the
            // difference with 12.3's Plain is what costs without a switch.
            case "Plain-CodeToolbar": viewer.Pipeline = PlainPipeline; viewer.ShowCodeBlockLanguage = false; viewer.ShowCodeBlockCopyButton = false; break;
            case "Plain-Rtl": viewer.Pipeline = PlainPipeline; viewer.DetectTextDirection = false; break;
            case "Plain-ContextMenu": viewer.Pipeline = PlainPipeline; viewer.ContextFlyout = null; break;
            case "Plain-Lean":
                viewer.Pipeline = PlainPipeline;
                viewer.ShowCodeBlockLanguage = false;
                viewer.ShowCodeBlockCopyButton = false;
                viewer.DetectTextDirection = false;
                viewer.IsImageZoomEnabled = false;
                viewer.ContextFlyout = null;
                break;
#endif
            default: throw new ArgumentOutOfRangeException(nameof(configuration), configuration, null);
        }
        return viewer;
    }

    public void SetMarkdown(Control viewer, string markdown) => ((MarkdownViewer)viewer).Markdown = markdown;

#if !MARKVIEW_12_3 || MARKVIEW_STREAMING
    public IStreamWriter BeginStreaming(Control viewer) => new SessionWriter((MarkdownViewer)viewer);

    /// <summary>
    /// The 12.4 streaming API: appends go through a session, which re-renders only the open blocks. Each append is
    /// applied at once, as the session's timer tick would (headless timers don't run on wall-clock time), so that
    /// every chunk is shown before the next, as with 12.3's full re-render.
    /// </summary>
    private sealed class SessionWriter(MarkdownViewer viewer) : IStreamWriter
    {
        private static readonly System.Reflection.MethodInfo ApplyPendingUpdate =
            typeof(MarkdownStreamingSession).GetMethod("ApplyPendingUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

        private readonly MarkdownStreamingSession _session = viewer.BeginStreaming();

        public void Append(string chunk)
        {
            _session.Append(chunk);
            ApplyPendingUpdate.Invoke(_session, null);
        }

        public bool IsSettled => true;

        public void Complete() => _session.Complete();
    }
#endif
}
