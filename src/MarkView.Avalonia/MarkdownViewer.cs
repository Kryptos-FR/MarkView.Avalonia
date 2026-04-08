using Avalonia;
using Avalonia.Controls;
using Markdig;
using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia;

public class MarkdownViewer : ContentControl
{
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownViewer, string?>(nameof(Markdown));

    public static readonly StyledProperty<MarkdownPipeline?> PipelineProperty =
        AvaloniaProperty.Register<MarkdownViewer, MarkdownPipeline?>(nameof(Pipeline));

    public static readonly StyledProperty<Uri?> BaseUriProperty =
        AvaloniaProperty.Register<MarkdownViewer, Uri?>(nameof(BaseUri));

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public MarkdownPipeline? Pipeline
    {
        get => GetValue(PipelineProperty);
        set => SetValue(PipelineProperty, value);
    }

    public Uri? BaseUri
    {
        get => GetValue(BaseUriProperty);
        set => SetValue(BaseUriProperty, value);
    }

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
            return;
        }

        var pipeline = Pipeline ?? new MarkdownPipelineBuilder().UseSupportedExtensions().Build();
        var document = Markdig.Markdown.Parse(Markdown, pipeline);

        var renderer = new AvaloniaRenderer { BaseUri = BaseUri };
        renderer.LinkClicked += (_, e) => LinkClicked?.Invoke(this, e);
        pipeline.Setup(renderer);
        renderer.Render(document);

        Content = new ScrollViewer
        {
            Content = renderer.RootPanel,
        };
    }
}
