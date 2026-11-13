// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Media;

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
    /// toolbar above the code. Defaults to <c>false</c>.
    /// </summary>
    public bool ShowCodeBlockLanguage { get; set; }

    /// <summary>
    /// Whether code blocks show a button in a toolbar above the code that copies the code to the
    /// clipboard. Defaults to <c>true</c>.
    /// </summary>
    public bool ShowCodeBlockCopyButton { get; set; } = true;

    /// <summary>
    /// Whether code blocks show the number of each line in a gutter next to the code. The numbers
    /// are not part of the code: text selection and the copy button never include them.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool ShowCodeBlockLineNumbers { get; set; }

    /// <summary>
    /// Whether code blocks wrap lines longer than the block's width instead of letting them run
    /// past it. Defaults to <c>false</c>.
    /// </summary>
    public bool WrapCodeBlockLines { get; set; }

    /// <summary>
    /// The maximum height of a code block (toolbar excluded); a taller block scrolls vertically
    /// inside itself. Defaults to <see cref="double.PositiveInfinity"/> (no limit).
    /// </summary>
    public double CodeBlockMaxHeight { get; set; } = double.PositiveInfinity;

    /// <summary>
    /// When <c>true</c>, task-list items render a clickable checkbox that raises
    /// <see cref="TaskToggled"/> when toggled; otherwise a static check glyph.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool AreTaskListsInteractive { get; set; }

    /// <summary>
    /// When <c>true</c>, each heading starts with a fold button that hides or shows its section: the blocks
    /// that follow it up to the next heading of the same or a higher level. Sections start expanded.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool AreHeadingsFoldable { get; set; }

    // The fold states the user chose for the document's foldable blocks (alerts, details elements,
    // headings), by block type and start in the source. A streaming session renders the block that is
    // still growing again on every update; the new controls take the state over from these.
    private readonly Dictionary<(Type BlockType, int Start), bool> _foldStates = [];

    /// <summary>
    /// Whether the foldable <paramref name="block"/> starts expanded: as the user last left it in this
    /// document when it was rendered before (see <see cref="FoldStateRecorder"/>), else
    /// <paramref name="defaultValue"/>.
    /// </summary>
    internal bool GetFoldState(Block block, bool defaultValue) =>
        _foldStates.TryGetValue((block.GetType(), block.Span.Start), out var expanded) ? expanded : defaultValue;

    /// <summary>A callback that records the fold state of <paramref name="block"/> for <see cref="GetFoldState"/>.</summary>
    internal Action<bool> FoldStateRecorder(Block block)
    {
        var key = (block.GetType(), block.Span.Start);
        return expanded => _foldStates[key] = expanded;
    }

    // When a streaming session first rendered each thinking block still in progress, and how long each one
    // it saw complete took, by the block's start in the source.
    private readonly Dictionary<int, long> _thinkingStarts = [];
    private readonly Dictionary<int, TimeSpan> _thinkingDurations = [];

    /// <summary>The clock thinking blocks are timed with. Tests replace it.</summary>
    internal TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>
    /// Times a language model's reasoning: notes when <paramref name="block"/> is first rendered while
    /// <paramref name="isThinking"/>, and returns how long it took once it is rendered complete after that, or
    /// <c>null</c> when this document never saw it in progress.
    /// </summary>
    internal TimeSpan? MeasureThinking(Block block, bool isThinking)
    {
        var start = block.Span.Start;
        if (_thinkingDurations.TryGetValue(start, out var duration))
            return duration;
        if (isThinking)
        {
            _thinkingStarts.TryAdd(start, TimeProvider.GetTimestamp());
            return null;
        }
        if (!_thinkingStarts.Remove(start, out var started))
            return null;
        duration = TimeProvider.GetElapsedTime(started);
        _thinkingDurations[start] = duration;
        return duration;
    }

    // Panels holding a heading rendered collapsed, whose section must be hidden once the panel is complete.
    private readonly List<Panel> _pendingHeadingFolds = [];

    /// <summary>Asks <see cref="ApplyHeadingFolds"/> to hide the section of the collapsed heading in <paramref name="panel"/>.</summary>
    internal void QueueHeadingFold(Panel panel)
    {
        if (!_pendingHeadingFolds.Contains(panel))
            _pendingHeadingFolds.Add(panel);
    }

    /// <summary>Hides the sections of the headings rendered collapsed since the last call.</summary>
    internal void ApplyHeadingFolds()
    {
        foreach (var panel in _pendingHeadingFolds)
            HeadingFold.Apply(panel);
        _pendingHeadingFolds.Clear();
    }

    /// <summary>
    /// When <c>true</c>, clicking a loaded image that is not inside a link (or pressing Enter or Space
    /// while it has focus) raises <see cref="ImageZoomRequested"/>, and the image shows the zoom-in
    /// cursor. <see cref="MarkdownViewer"/> turns this on from its <see cref="MarkdownViewer.IsImageZoomEnabled"/>
    /// and shows the image in its lightbox. Defaults to <c>false</c>.
    /// </summary>
    internal bool IsImageZoomEnabled { get; set; }

    /// <summary>
    /// When <c>true</c>, each block takes the <see cref="Visual.FlowDirection"/> of its own text, as HTML's
    /// <c>dir="auto"</c> does: a paragraph, heading, list, quote or table whose first letter is Hebrew or Arabic
    /// is laid out right to left, one whose first letter is Latin left to right. A block without a letter
    /// (only digits or symbols) keeps the direction around it. Code blocks are always left to right, and an
    /// HTML <c>dir</c> attribute (<c>&lt;div dir="rtl"&gt;</c>, <c>&lt;p dir="rtl"&gt;</c>) wins over the text.
    /// When <c>false</c>, blocks keep the direction of the control that shows the document. Defaults to <c>true</c>.
    /// </summary>
    public bool DetectTextDirection { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, costly content waits until its element first comes within one viewport height
    /// of the visible area (see <see cref="ViewportTracking.RunWhenNearViewport"/>): images start
    /// loading (unless already cached), formulas and Mermaid diagrams render, and code blocks, shown as
    /// plain text until then, get their colours. When <c>false</c>, formulas and colours are built during
    /// <see cref="RendererBase.Render"/>, and images and diagrams start as soon as their element is
    /// attached to a visual tree. Defaults to <c>false</c>, since the output of a renderer may never be
    /// laid out in a viewport; <see cref="MarkdownViewer"/> turns it on from its
    /// <see cref="MarkdownViewer.IsLazyRenderingEnabled"/>.
    /// </summary>
    public bool IsLazyRenderingEnabled { get; set; }

    /// <summary>Raised with the clicked image when <see cref="IsImageZoomEnabled"/> is set.</summary>
    internal event Action<Image>? ImageZoomRequested;

    internal void OnImageZoomRequested(Image image) => ImageZoomRequested?.Invoke(image);

    /// <summary>
    /// Generates GitHub-style anchor IDs for headings. Reset on each render pass.
    /// </summary>
    public SlugGenerator SlugGenerator { get; } = new();

    private readonly Dictionary<string, Control> _anchors = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Maps anchor IDs to their heading controls for fragment link navigation.
    /// </summary>
    public IReadOnlyDictionary<string, Control> Anchors => _anchors;

    // Registrations in order, beside the dictionary: a duplicate id overwrites its entry there, so
    // truncating to an earlier registration needs the history to restore it.
    private readonly List<(string Id, Control Control)> _anchorOrder = [];

    /// <summary>
    /// Registers a heading control under the given anchor ID.
    /// </summary>
    public void RegisterAnchor(string id, Control control)
    {
        _anchors[id] = control;
        _anchorOrder.Add((id, control));
    }

    /// <summary>The number of <see cref="RegisterAnchor"/> calls since the document began, less those dropped by <see cref="TruncateAnchors"/>.</summary>
    internal int AnchorCount => _anchorOrder.Count;

    /// <summary>
    /// Forgets every anchor registered after the first <paramref name="count"/> registrations, so a
    /// duplicate id points at its earlier registration again.
    /// </summary>
    internal void TruncateAnchors(int count)
    {
        _anchorOrder.RemoveRange(count, _anchorOrder.Count - count);
        _anchors.Clear();
        foreach (var (id, control) in _anchorOrder)
            _anchors[id] = control;
    }

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

    /// <summary>The number of headings recorded by <see cref="RegisterHeading"/> since the document began, less those dropped by <see cref="TruncateHeadings"/>.</summary>
    internal int HeadingCount => _headingEntries.Count;

    /// <summary>Forgets every heading recorded after the first <paramref name="count"/>.</summary>
    internal void TruncateHeadings(int count) => _headingEntries.RemoveRange(count, _headingEntries.Count - count);

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
        ObjectWriteBefore += (_, obj) =>
        {
            if (obj is Block { Parent: MarkdownDocument })
                _topLevelChildStart = RootPanel.Children.Count;
        };
        ObjectWriteAfter += (_, obj) =>
        {
            if (obj is Block { Parent: MarkdownDocument } block)
            {
                for (var i = _topLevelChildStart; i < RootPanel.Children.Count; i++)
                    TopLevelBlocks.AddOrUpdate(RootPanel.Children[i], block);
            }
        };
        ObjectWriteBefore += (_, obj) => _blockStarts.Push(
            obj is Block and not MarkdownDocument && _stack.Peek() is BlockContainer container
                ? (container.Panel, container.Panel.Children.Count)
                : (null, 0));
        ObjectWriteAfter += (_, obj) => ApplyFlowDirection(obj, _blockStarts.Pop());
    }

    // For each object being written, innermost last: the panel its block controls are added to and how
    // many children that panel had before, or no panel for an inline or the document.
    private readonly Stack<(Panel? Panel, int Count)> _blockStarts = new();

    /// <summary>
    /// Gives the controls <paramref name="obj"/> added to its panel the direction of the block (see
    /// <see cref="DetectTextDirection"/>), unless its renderer chose one. The text of a paragraph that reads the
    /// other way from the list item or table column it is in is aligned to the side of the item's marker or the
    /// column's other cells.
    /// </summary>
    private void ApplyFlowDirection(MarkdownObject obj, (Panel? Panel, int Count) start)
    {
        if (start.Panel is not { } panel || panel.Children.Count == start.Count
            || TextDirection.Of((Block)obj, DetectTextDirection) is not { } direction)
            return;

        var isAgainstLayout = obj is LeafBlock leaf && TextDirection.OfLayout(leaf, DetectTextDirection) is { } layout && layout != direction;
        for (var i = start.Count; i < panel.Children.Count; i++)
        {
            var child = panel.Children[i];
            if (!child.IsSet(Visual.FlowDirectionProperty))
                child.FlowDirection = direction;
            if (isAgainstLayout && child is TextBlock textBlock && !textBlock.IsSet(TextBlock.TextAlignmentProperty))
                textBlock.TextAlignment = TextAlignment.End;
        }
    }

    /// <summary>
    /// The direction the text of <paramref name="block"/> is laid out in (see <see cref="DetectTextDirection"/>),
    /// or <c>null</c> when the block takes the direction of the control that shows the document.
    /// </summary>
    internal FlowDirection? ResolveFlowDirection(Block block) => TextDirection.Resolve(block, DetectTextDirection);

    /// <summary>
    /// <c>true</c> when the text <paramref name="inline"/> belongs to is laid out right to left (see
    /// <see cref="ResolveFlowDirection"/>).
    /// </summary>
    internal bool IsRightToLeft(Markdig.Syntax.Inlines.Inline inline)
    {
        var container = inline as Markdig.Syntax.Inlines.ContainerInline ?? inline.Parent;
        while (container?.Parent is { } parent)
            container = parent;
        return container?.ParentBlock is { } leaf && ResolveFlowDirection(leaf) == FlowDirection.RightToLeft;
    }

    // The top-level block each control of a root panel was rendered for.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, Block> TopLevelBlocks = new();

    // How many controls the root panel held when the top-level block being written started.
    private int _topLevelChildStart;

    /// <summary>
    /// The top-level block <paramref name="control"/>, a child of a renderer's <see cref="RootPanel"/>, was
    /// rendered for; <c>null</c> for any other control.
    /// </summary>
    internal static Block? GetTopLevelBlock(Control control) =>
        TopLevelBlocks.TryGetValue(control, out var block) ? block : null;

    /// <summary>
    /// Records <paramref name="block"/> as the top-level block <paramref name="control"/> was rendered for, for a
    /// control a streaming session kept while its block grew (see <see cref="GrowingCodeBlock"/>).
    /// </summary>
    internal static void SetTopLevelBlock(Control control, Block block) => TopLevelBlocks.AddOrUpdate(control, block);

    /// <summary>
    /// The code block a streaming session wants written as a <see cref="GrowingCodeBlock"/>, the one that
    /// ends the text so far, or <c>null</c>. Set by the session around writing that block only.
    /// </summary>
    internal CodeBlock? GrowingCodeTarget { get; set; }

    /// <summary>
    /// The <see cref="GrowingCodeBlock"/> written for <see cref="GrowingCodeTarget"/>, or <c>null</c> when the
    /// renderer that wrote the block does not write growing code blocks. Taken by the session.
    /// </summary>
    internal GrowingCodeBlock? WrittenGrowingCode { get; set; }

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

    /// <inheritdoc/>
    public override object Render(MarkdownObject markdownObject)
    {
        BeginDocument();
        Write(markdownObject);
        ApplyHeadingFolds();
        return RootPanel;
    }

    /// <summary>
    /// What <see cref="Render"/> does before writing: resets the anchors, headings, slugs, fold states and thinking times, and places
    /// the custom fence renderer (once, however many times a document begins).
    /// </summary>
    internal void BeginDocument()
    {
        _anchors.Clear();
        _anchorOrder.Clear();
        _headingEntries.Clear();
        _foldStates.Clear();
        _pendingHeadingFolds.Clear();
        _thinkingStarts.Clear();
        _thinkingDurations.Clear();
        SlugGenerator.Reset();
        PlaceCustomFenceRenderer();
    }

    /// <summary>
    /// Writes a top-level block into the root panel, without resetting anything. The bottom of the
    /// container stack is the <see cref="RootPanel"/>, pushed by the constructor.
    /// </summary>
    internal void WriteTopLevel(Block block) => Write(block);

    /// <summary>
    /// <c>true</c> while a streaming session renders the document, which may end in the middle of a
    /// block. Renderers can consult <see cref="IsProvisional"/> to render the blocks that contain the end
    /// of the text so far differently.
    /// </summary>
    public bool IsStreaming { get; internal set; }

    /// <summary>
    /// The source position where the block that contains the end of the text starts (the deepest last
    /// block of the document), or <see cref="int.MaxValue"/> when no block does: outside a session, on
    /// the final update, or when a blank line follows the last block. Every block whose span ends at or
    /// after it contains the end of the text. Read by <see cref="IsProvisional"/> while
    /// <see cref="IsStreaming"/> is <c>true</c>.
    /// </summary>
    public int ProvisionalStart { get; internal set; } = int.MaxValue;

    /// <summary>
    /// True during a session for an object that contains the end of the text so far: the last leaf block
    /// and every block around it, those whose span ends at or after <see cref="ProvisionalStart"/>; an
    /// inline is judged by its leaf block (ContainerInline.ParentBlock), and is never provisional outside one.
    /// Always <c>false</c> when <see cref="IsStreaming"/> is <c>false</c>, so renderers can call it unconditionally.
    /// </summary>
    public bool IsProvisional(MarkdownObject obj)
    {
        if (!IsStreaming)
            return false;

        switch (obj)
        {
            case Block block:
                return ContainsTheEnd(block);
            case Markdig.Syntax.Inlines.Inline inline:
                var container = inline as Markdig.Syntax.Inlines.ContainerInline ?? inline.Parent;
                while (container?.Parent is { } parent)
                    container = parent;
                return container?.ParentBlock is { } leaf && ContainsTheEnd(leaf);
            default:
                return false;
        }
    }

    private bool ContainsTheEnd(Block block) => block.Span.End >= ProvisionalStart;

    // Placed when rendering starts, after every extension and pipeline.Setup() have installed their
    // renderers, so a fence renderer wins over the other renderers of fenced code blocks (Mermaid,
    // syntax highlighting, the plain code block) whatever order they were registered in. A renderer
    // of a narrower type (MathBlock) that already precedes them keeps its place. Placed once: a
    // second document on the same renderer must not stack another one.
    private bool _fenceRendererPlaced;

    private void PlaceCustomFenceRenderer()
    {
        if (_fenceRendererPlaced || FenceRenderers.Count == 0)
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
        _fenceRendererPlaced = true;
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
        ObjectRenderers.Add(new HtmlAlignedBlockRenderer());
        ObjectRenderers.Add(new TableRenderer());
        ObjectRenderers.Add(new FootnoteGroupRenderer());
        ObjectRenderers.Add(new FigureRenderer());
        ObjectRenderers.Add(new DefinitionListRenderer());
        ObjectRenderers.Add(new CustomContainerRenderer());
        ObjectRenderers.Add(new ThinkingBlockRenderer());
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
        ObjectRenderers.Add(new HtmlElementInlineRenderer());
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

    /// <summary>The control themes this renderer's controls get, found from its viewer or the application.</summary>
    internal ThemeLookup Themes { get; set; } = ThemeLookup.ForApplication();

    /// <summary>
    /// Gives <paramref name="element"/> the control theme with the resource key <paramref name="resourceKey"/>,
    /// such as <c>MarkdownCodeBlockTheme</c>, so that a renderer's own control looks like the built-in one
    /// it stands for. The built-in look of the document's controls comes from these themes, not from
    /// styles on their <c>markdown-*</c> classes. A key that no theme defines yet gives an empty theme,
    /// which the viewer replaces once one does. Replaces the theme the control's type would otherwise
    /// get, so it is meant for plain panels, borders, text blocks and inlines.
    /// </summary>
    public void ApplyTheme(StyledElement element, string resourceKey) => element.Theme = Themes.Get(resourceKey);

    /// <summary>
    /// Whether the inlines being written belong to a paragraph (<c>markdown-paragraph</c>), as opposed to
    /// a heading, a title or a caption: inline code then takes the paragraph's look.
    /// </summary>
    internal bool IsWritingParagraph { get; set; }

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
    /// Adds plain text to the current inline container. Text written right after other plain text goes into
    /// the same <see cref="Run"/>: Markdig splits prose into several literals (at each line break, escape,
    /// entity or unmatched delimiter), and a Run each would multiply what every paragraph costs to attach,
    /// style and lay out. Text that takes attribute classes gets a Run of its own.
    /// </summary>
    internal void WriteText(ReadOnlySpan<char> text)
    {
        if (_stack.Peek() is not InlineContainer container)
            return;
        if (_writing.TryPeek(out var entry) && entry.IsInline && entry.Classes is not null)
        {
            var run = new Run(text.ToString());
            AddAttributeClasses(run, isInline: true);
            container.Add(run);
            return;
        }
        container.AddText(text);
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
    /// Writes the raw text lines of a leaf block (code blocks), as one Run.
    /// </summary>
    public void WriteLeafRawLines(LeafBlock leafBlock)
    {
        if (leafBlock.Lines.Lines == null)
            // Stryker disable once Statement : a group without line storage has Count 0, so the loop below is a no-op as well
            return;

        // One Run for the whole block, each line end a '\n' in its text: a Run and a LineBreak per line
        // cost far more to create and lay out.
        var lines = leafBlock.Lines;
        for (int i = 0; i < lines.Count; i++)
        {
            if (i > 0)
                WriteText("\n");
            WriteText(lines.Lines[i].Slice.AsSpan());
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
