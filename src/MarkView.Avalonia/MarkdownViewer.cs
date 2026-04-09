using Avalonia;
using Avalonia.Controls;
using Markdig;
using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia;

/// <summary>
/// A control that renders Markdown text into Avalonia visual elements.
/// </summary>
public class MarkdownViewer : ContentControl
{
    private Dictionary<string, Control> _anchors = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Defines the <see cref="Markdown"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownViewer, string?>(nameof(Markdown));

    /// <summary>
    /// Defines the <see cref="Pipeline"/> property.
    /// </summary>
    public static readonly StyledProperty<MarkdownPipeline?> PipelineProperty =
        AvaloniaProperty.Register<MarkdownViewer, MarkdownPipeline?>(nameof(Pipeline));

    /// <summary>
    /// Defines the <see cref="BaseUri"/> property.
    /// </summary>
    public static readonly StyledProperty<Uri?> BaseUriProperty =
        AvaloniaProperty.Register<MarkdownViewer, Uri?>(nameof(BaseUri));

    /// <summary>
    /// Gets or sets the Markdown text to render.
    /// </summary>
    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    /// <summary>
    /// Gets or sets the Markdig pipeline. If null, a default pipeline with supported extensions is used.
    /// </summary>
    public MarkdownPipeline? Pipeline
    {
        get => GetValue(PipelineProperty);
        set => SetValue(PipelineProperty, value);
    }

    /// <summary>
    /// Gets or sets the base URI for resolving relative URLs in images and links.
    /// </summary>
    public Uri? BaseUri
    {
        get => GetValue(BaseUriProperty);
        set => SetValue(BaseUriProperty, value);
    }

    /// <summary>
    /// Raised when a hyperlink in the rendered markdown is clicked.
    /// </summary>
    public event EventHandler<LinkClickedEventArgs>? LinkClicked;

    static MarkdownViewer()
    {
        MarkdownProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.RenderMarkdown());
        PipelineProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.RenderMarkdown());
        BaseUriProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.RenderMarkdown());
    }

    private void RenderMarkdown()
    {
        if (string.IsNullOrEmpty(Markdown))
        {
            Content = null;
            _anchors = new(StringComparer.OrdinalIgnoreCase);
            return;
        }

        var pipeline = Pipeline ?? new MarkdownPipelineBuilder().UseSupportedExtensions().Build();
        var document = Markdig.Markdown.Parse(Markdown, pipeline);

        var renderer = new AvaloniaRenderer { BaseUri = BaseUri };
        renderer.LinkClicked += OnLinkClicked;
        pipeline.Setup(renderer);
        renderer.Render(document);

        _anchors = new Dictionary<string, Control>(renderer.Anchors, StringComparer.OrdinalIgnoreCase);

        Content = new ScrollViewer
        {
            Content = renderer.RootPanel,
        };
    }

    private void OnLinkClicked(object? sender, LinkClickedEventArgs e)
    {
        if (e.Url.StartsWith('#'))
        {
            ScrollToAnchor(e.Url[1..]);
            return;
            // Fragment links are handled internally — do not fire public LinkClicked.
        }
        LinkClicked?.Invoke(this, e);
    }

    private void ScrollToAnchor(string anchorId)
    {
        if (_anchors.TryGetValue(anchorId, out var control))
            control.BringIntoView();

        // TODO: expose AnchorNavigationRequested event so consumers can override
        // or cancel scroll behaviour (e.g. custom scroll animation, external router).
    }
}
