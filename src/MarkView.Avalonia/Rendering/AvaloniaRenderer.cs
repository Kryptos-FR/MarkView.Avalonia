// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;

using Markdig.Renderers;
using Markdig.Syntax;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering.Blocks;
using MarkView.Avalonia.Rendering.Containers;
using MarkView.Avalonia.Rendering.Inlines;

using AvaloniaInline = Avalonia.Controls.Documents.Inline;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Renders a Markdig <see cref="MarkdownDocument"/> into an Avalonia control tree.
/// </summary>
public class AvaloniaRenderer : RendererBase
{
    private readonly Stack<IContainer> _stack = new();

    /// <summary>
    /// The root panel that contains all rendered block-level elements.
    /// </summary>
    public StackPanel RootPanel { get; }

    /// <summary>
    /// Optional base URI for resolving relative image and link paths.
    /// </summary>
    public Uri? BaseUri { get; set; }

    /// <summary>
    /// Generates GitHub-style anchor IDs for headings. Reset on each render pass.
    /// </summary>
    public SlugGenerator SlugGenerator { get; } = new();

    private readonly Dictionary<string, Control> _anchors = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Maps anchor IDs to their heading controls for fragment link navigation.
    /// </summary>
    public IReadOnlyDictionary<string, Control> Anchors => _anchors;

    /// <summary>
    /// Registers a heading control under the given anchor ID.
    /// </summary>
    public void RegisterAnchor(string id, Control control) => _anchors[id] = control;

    /// <summary>
    /// Optional syntax highlighter used by <c>TextMateCodeBlockRenderer</c>.
    /// Set by <see cref="Extensions.IMarkViewExtension.Register"/> implementations.
    /// </summary>
    public ICodeHighlighter? CodeHighlighter { get; set; }

    /// <summary>
    /// When <c>true</c>, the next <see cref="Inlines.TaskListRenderer"/> call is skipped
    /// and this flag is reset to <c>false</c>.  Set by <see cref="Blocks.ListRenderer"/>
    /// when it has already placed the checkbox in the list-item grid's column 0.
    /// </summary>
    internal bool SkipNextTaskList { get; set; }

    /// <summary>
    /// Ordered list of image loaders. The last entry is always the built-in
    /// <see cref="BitmapImageLoader"/> which handles avares://, http/https, and
    /// data URIs for bitmap formats. Extensions insert at index 0 to take priority.
    /// </summary>
    public IList<IImageLoader> ImageLoaders { get; } = [new BitmapImageLoader()];

    /// <summary>
    /// Removes the first registered renderer of type <typeparamref name="TRenderer"/>
    /// and adds <paramref name="replacement"/> at the end of the list.
    /// If none exists, simply adds <paramref name="replacement"/>.
    /// </summary>
    public void ReplaceOrAdd<TRenderer>(IMarkdownObjectRenderer replacement)
        where TRenderer : IMarkdownObjectRenderer
    {
        var existing = ObjectRenderers.Find<TRenderer>();
        if (existing != null)
            ObjectRenderers.Remove(existing);
        ObjectRenderers.Add(replacement);
    }

    /// <summary>
    /// Raised when a hyperlink is clicked.
    /// </summary>
    public event EventHandler<LinkClickedEventArgs>? LinkClicked;

    public AvaloniaRenderer()
    {
        RootPanel = new StackPanel { Spacing = 8 };
        _stack.Push(new BlockContainer(RootPanel));
        LoadRenderers();
    }

    public override object Render(MarkdownObject markdownObject)
    {
        _anchors.Clear();
        SlugGenerator.Reset();
        Write(markdownObject);
        return RootPanel;
    }

    /// <summary>
    /// Registers all built-in object renderers. Override to customize.
    /// </summary>
    protected virtual void LoadRenderers()
    {
        // Block renderers
        ObjectRenderers.Add(new ParagraphRenderer());
        ObjectRenderers.Add(new HeadingRenderer());
        ObjectRenderers.Add(new CodeBlockRenderer());
        ObjectRenderers.Add(new ListRenderer());
        ObjectRenderers.Add(new ThematicBreakRenderer());
        // AlertBlockRenderer must precede QuoteBlockRenderer: AlertBlock extends QuoteBlock
        // and Markdig dispatches to the first renderer whose Accept() matches.
        ObjectRenderers.Add(new AlertBlockRenderer());
        ObjectRenderers.Add(new QuoteBlockRenderer());
        ObjectRenderers.Add(new HtmlBlockRenderer());
        ObjectRenderers.Add(new TableRenderer());
        ObjectRenderers.Add(new FootnoteGroupRenderer());
        ObjectRenderers.Add(new FigureRenderer());

        // Inline renderers
        ObjectRenderers.Add(new LiteralInlineRenderer());
        ObjectRenderers.Add(new EmphasisInlineRenderer());
        ObjectRenderers.Add(new CodeInlineRenderer());
        ObjectRenderers.Add(new LinkInlineRenderer());
        ObjectRenderers.Add(new AutolinkInlineRenderer());
        ObjectRenderers.Add(new LineBreakInlineRenderer());
        ObjectRenderers.Add(new DelimiterInlineRenderer());
        ObjectRenderers.Add(new HtmlEntityInlineRenderer());
        ObjectRenderers.Add(new HtmlInlineRenderer());
        ObjectRenderers.Add(new TaskListRenderer());
        ObjectRenderers.Add(new FootnoteLinkRenderer());
        ObjectRenderers.Add(new AbbreviationInlineRenderer());
    }

    /// <summary>
    /// Pushes a panel as a block container onto the stack.
    /// </summary>
    public void Push(Panel panel)
    {
        _stack.Push(new BlockContainer(panel));
    }

    /// <summary>
    /// Pushes an inline collection as an inline container onto the stack.
    /// </summary>
    public void Push(InlineCollection inlines)
    {
        _stack.Push(new InlineContainer(inlines));
    }

    /// <summary>
    /// Pops the top container from the stack.
    /// </summary>
    public void Pop()
    {
        _stack.Pop();
    }

    /// <summary>
    /// Adds a block-level control to the current block container.
    /// </summary>
    public void WriteBlock(Control control)
    {
        if (_stack.Peek() is BlockContainer block)
            block.Add(control);
    }

    /// <summary>
    /// Adds an inline to the current inline container.
    /// </summary>
    public void WriteInline(AvaloniaInline inline)
    {
        if (_stack.Peek() is InlineContainer container)
            container.Add(inline);
    }

    /// <summary>
    /// Adds a control wrapped in InlineUIContainer to the current inline container.
    /// </summary>
    public void WriteInline(Control control)
    {
        if (_stack.Peek() is InlineContainer container)
            container.Add(control);
    }

    /// <summary>
    /// Writes all inlines of a leaf block (paragraph, heading, etc.) into the current inline container.
    /// </summary>
    public void WriteLeafInline(LeafBlock leafBlock)
    {
        Markdig.Syntax.Inlines.Inline? inline = leafBlock.Inline;
        while (inline != null)
        {
            Write(inline);
            inline = inline.NextSibling;
        }
    }

    /// <summary>
    /// Writes the raw text lines of a leaf block (code blocks).
    /// </summary>
    public void WriteLeafRawLines(LeafBlock leafBlock)
    {
        if (leafBlock.Lines.Lines == null)
            return;

        var lines = leafBlock.Lines;
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines.Lines[i];
            if (i > 0)
                WriteInline(new LineBreak());
            WriteInline(new Run(line.Slice.ToString()));
        }
    }

    /// <summary>
    /// Resolves a URL against the <see cref="BaseUri"/> if set and the URL is relative.
    /// </summary>
    public string ResolveUrl(string url)
    {
        if (BaseUri != null && Uri.TryCreate(url, UriKind.Relative, out _))
        {
            return new Uri(BaseUri, url).ToString();
        }
        return url;
    }

    internal void OnLinkClicked(string url)
    {
        LinkClicked?.Invoke(this, new LinkClickedEventArgs(url));
    }
}

/// <summary>
/// Event args for hyperlink click events.
/// </summary>
public class LinkClickedEventArgs(string url) : EventArgs
{
    /// <summary>
    /// The URL of the clicked link.
    /// </summary>
    public string Url { get; } = url;
}
