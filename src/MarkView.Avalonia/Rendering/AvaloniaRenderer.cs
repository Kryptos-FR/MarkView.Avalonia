// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;

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
    /// Controls how images without an explicit "=WxH" size hint are scaled.
    /// Defaults to <see cref="ImageResizeMode.ScaleDownToFit"/>.
    /// </summary>
    public ImageResizeMode ImageResizeMode { get; set; } = ImageResizeMode.ScaleDownToFit;

    /// <summary>
    /// Whether code blocks show their language (the info string of a fenced code block) in a
    /// toolbar above the code. Defaults to <c>true</c>.
    /// </summary>
    public bool ShowCodeBlockLanguage { get; set; } = true;

    /// <summary>
    /// Whether code blocks show a button in a toolbar above the code that copies the code to the
    /// clipboard. Defaults to <c>true</c>.
    /// </summary>
    public bool ShowCodeBlockCopyButton { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, task-list items render a clickable checkbox that raises
    /// <see cref="TaskToggled"/> when toggled; otherwise a static check glyph.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool AreTaskListsInteractive { get; set; }

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

    private readonly List<(int Level, string Text, string Slug)> _headingEntries = [];

    /// <summary>
    /// Document-ordered list of headings encountered during the last render pass
    /// (level, plain text, anchor slug), used to build <see cref="MarkdownViewer.TableOfContents"/>.
    /// Reset on each render.
    /// </summary>
    public IReadOnlyList<(int Level, string Text, string Slug)> HeadingEntries => _headingEntries;

    /// <summary>
    /// Records a heading's level, plain text, and anchor slug in document order.
    /// </summary>
    public void RegisterHeading(int level, string text, string slug) => _headingEntries.Add((level, text, slug));

    /// <summary>
    /// Optional syntax highlighter used by <c>TextMateCodeBlockRenderer</c>.
    /// Set by <see cref="IMarkViewExtension.Register"/> implementations.
    /// </summary>
    public ICodeHighlighter? CodeHighlighter { get; set; }

    /// <summary>
    /// When <c>true</c>, the next <see cref="TaskListRenderer"/> call is skipped
    /// and this flag is reset to <c>false</c>.  Set by <see cref="ListRenderer"/>
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
    /// Factories of the controls that replace fenced code blocks, by language (case-insensitive).
    /// Filled by <see cref="FenceRendererExtension"/>.
    /// </summary>
    internal Dictionary<string, FenceRendererFactory> FenceRenderers { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Raised when a hyperlink is clicked.
    /// </summary>
    public event EventHandler<LinkClickedEventArgs>? LinkClicked;

    internal void OnLinkClicked(string url) =>
        LinkClicked?.Invoke(this, new LinkClickedEventArgs(url));

    /// <summary>
    /// Raised when the user toggles a task-list checkbox (see <see cref="AreTaskListsInteractive"/>).
    /// Positions refer to the text the rendered document was parsed from.
    /// </summary>
    public event EventHandler<TaskToggledEventArgs>? TaskToggled;

    /// <summary>
    /// Raises <see cref="TaskToggled"/> for the task item whose first paragraph is <paramref name="paragraph"/>.
    /// </summary>
    /// <remarks>
    /// Markdig only tracks an inline's source span with <c>UsePreciseSourceLocation()</c>, but always
    /// tracks a block's. A task marker always opens its item's first paragraph and is always three
    /// characters long (<c>[ ]</c>, <c>[x]</c> or <c>[X]</c>), so the paragraph locates it in any pipeline.
    /// </remarks>
    internal void OnTaskToggled(ParagraphBlock paragraph, bool isChecked) =>
        TaskToggled?.Invoke(this, new TaskToggledEventArgs(isChecked, paragraph.Span.Start, TaskMarkerLength, paragraph.Line));

    private const int TaskMarkerLength = 3;

    public AvaloniaRenderer()
    {
        RootPanel = new StackPanel { Spacing = 8 };
        _stack.Push(new BlockContainer(RootPanel));
        LoadRenderers();
        ObjectWriteBefore += (_, obj) => _writing.Push((
            ApplyAttributeClasses ? Markdig.Renderers.Html.HtmlAttributesExtensions.TryGetAttributes(obj)?.Classes : null,
            obj is Markdig.Syntax.Inlines.Inline));
        ObjectWriteAfter += (_, _) => _writing.Pop();
    }

    /// <summary>
    /// When <c>true</c>, the classes Markdig attaches to an element (<c>{.name}</c> with Markdig's
    /// <c>UseGenericAttributes()</c>) are added to the <see cref="StyledElement.Classes"/> of the
    /// control or inline rendered for it, so they can be targeted by styles.
    /// <see cref="MarkdownViewer"/> turns this on when its pipeline uses generic attributes.
    /// </summary>
    public bool ApplyAttributeClasses { get; set; }

    // The attribute classes of each object being written, innermost last, and whether that object
    // is an inline. An entry is cleared once given to the first control or inline its object writes:
    // a block's classes go to its first block write, so the raw text runs a code block writes into
    // its own TextBlock first don't take them.
    private readonly Stack<(List<string>? Classes, bool IsInline)> _writing = new();

    private void AddAttributeClasses(StyledElement element, bool isInline)
    {
        if (!_writing.TryPeek(out var entry) || entry.IsInline != isInline)
            return;
        var classes = entry.Classes;
        if (classes is null)
            return;
        _writing.Pop();
        _writing.Push((null, isInline));
        foreach (var name in classes)
        {
            // Avalonia reserves names starting with ':' for pseudo-classes.
            if (name.Length > 0 && name[0] != ':')
                element.Classes.Add(name);
        }
    }

    public override object Render(MarkdownObject markdownObject)
    {
        _anchors.Clear();
        _headingEntries.Clear();
        SlugGenerator.Reset();
        PlaceCustomFenceRenderer();
        Write(markdownObject);
        return RootPanel;
    }

    // Placed when rendering starts, after every extension and pipeline.Setup() have installed their
    // renderers, so a fence renderer wins over the other renderers of fenced code blocks (Mermaid,
    // syntax highlighting, the plain code block) whatever order they were registered in. A renderer
    // of a narrower type (MathBlock) that already precedes them keeps its place.
    private void PlaceCustomFenceRenderer()
    {
        if (FenceRenderers.Count == 0)
            return;

        var insertAt = ObjectRenderers.Count;
        for (var i = 0; i < ObjectRenderers.Count; i++)
        {
            if (ObjectRenderers[i].Accept(this, typeof(FencedCodeBlock)))
            {
                insertAt = i;
                break;
            }
        }
        ObjectRenderers.Insert(insertAt, new CustomFenceRenderer());
    }

    /// <summary>
    /// Registers all built-in object renderers. Override to customize.
    /// </summary>
    protected virtual void LoadRenderers()
    {
        // Block renderers
        ObjectRenderers.Add(new ParagraphRenderer());
        ObjectRenderers.Add(new HeadingRenderer());
        // YamlFrontMatterBlockRenderer must precede CodeBlockRenderer: YamlFrontMatterBlock extends
        // CodeBlock and Markdig dispatches to the first renderer whose Accept() matches.
        ObjectRenderers.Add(new YamlFrontMatterBlockRenderer());
        ObjectRenderers.Add(new CodeBlockRenderer());
        ObjectRenderers.Add(new ListRenderer());
        ObjectRenderers.Add(new ThematicBreakRenderer());
        // AlertBlockRenderer must precede QuoteBlockRenderer: AlertBlock extends QuoteBlock
        // and Markdig dispatches to the first renderer whose Accept() matches.
        ObjectRenderers.Add(new AlertBlockRenderer());
        ObjectRenderers.Add(new QuoteBlockRenderer());
        ObjectRenderers.Add(new HtmlBlockRenderer());
        ObjectRenderers.Add(new DetailsBlockRenderer());
        ObjectRenderers.Add(new TableRenderer());
        ObjectRenderers.Add(new FootnoteGroupRenderer());
        ObjectRenderers.Add(new FigureRenderer());
        ObjectRenderers.Add(new DefinitionListRenderer());
        ObjectRenderers.Add(new CustomContainerRenderer());
        ObjectRenderers.Add(new FooterBlockRenderer());

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
        ObjectRenderers.Add(new SmartyPantInlineRenderer());
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
        {
            AddAttributeClasses(control, isInline: false);
            block.Add(control);
        }
    }

    /// <summary>
    /// Adds an inline to the current inline container.
    /// </summary>
    public void WriteInline(AvaloniaInline inline)
    {
        if (_stack.Peek() is InlineContainer container)
        {
            AddAttributeClasses(inline, isInline: true);
            container.Add(inline);
        }
    }

    /// <summary>
    /// Adds a control wrapped in InlineUIContainer to the current inline container.
    /// </summary>
    public void WriteInline(Control control)
    {
        if (_stack.Peek() is InlineContainer container)
        {
            AddAttributeClasses(control, isInline: true);
            container.Add(control);
        }
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
            // Stryker disable once Statement : a group without line storage has Count 0, so the loop below is a no-op as well
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
    /// The fragment part (everything from <c>#</c> onward) is split off before combining
    /// with the base URI so that <c>#</c> is never percent-encoded to <c>%23</c>.
    /// </summary>
    public string ResolveUrl(string url)
    {
        if (url.StartsWith('#'))
            return url; // same-document anchor: never resolve against BaseUri

        if (BaseUri != null && Uri.TryCreate(url, UriKind.Relative, out _))
        {
            // Separate path from fragment before combining to prevent '#' → '%23' encoding.
            if (url.Contains('#'))
            {
                var hashIdx = url.IndexOf('#');
                var path = url[..hashIdx];
                var fragment = url[hashIdx..]; // includes the '#'
                return new Uri(BaseUri, path) + fragment;
            }
            return new Uri(BaseUri, url).ToString();
        }
        return url;
    }

}

/// <summary>
/// Event args for hyperlink click events.
/// </summary>
public sealed class LinkClickedEventArgs(string url) : RoutedEventArgs
{
    /// <summary>
    /// The URL of the clicked link.
    /// </summary>
    public string Url { get; } = url;
}
