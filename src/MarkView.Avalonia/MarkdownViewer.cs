// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text;
using System.Text.RegularExpressions;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Markdig;
using Markdig.Extensions.GenericAttributes;
using Markdig.Extensions.SmartyPants;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;
using MarkView.Avalonia.Rendering.Inlines;

namespace MarkView.Avalonia;

/// <summary>
/// A control that renders Markdown text into Avalonia visual elements.
/// </summary>
public partial class MarkdownViewer : ContentControl
{
    // Matches a backtick code span (group 1) or the non-standard "![alt](url =WxH)" size hint
    // (groups 2-5). Code spans are passed through unchanged so literal syntax examples in
    // documentation are not mangled.
    [GeneratedRegex(@"(`+)[\s\S]*?\1|(\!\[[^\]]*\]\()([^\s\)]+)\s+=(\d+x\d+)(\))")]
    private static partial Regex ImageSizePreprocessorRegex();

    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

    private Dictionary<string, Control> _anchors = new(StringComparer.OrdinalIgnoreCase);
    private List<(int Level, string Text, string Slug)> _headingEntries = [];
    private DocumentSelectionLayer? _selectionLayer;
    private LinkFocusLayer? _linkFocusLayer;
    private ScrollViewer? _scrollViewer;
    private bool _isDragging;
    private Point _dragStart;
    private const double DragThreshold = 3.0;

    private string? _sourceMarkdown;

    // The active search, run again on each new render; null when there is none.
    private string? _findText;
    private MarkdownFindOptions _findOptions;
    private ImageLightbox? _lightbox;

    // The markdown last rendered, with the task toggles made since applied to it; assigning the same
    // text to Markdown (as a host writing a toggle back does) then needs no re-render.
    private string? _renderedMarkdown;
    // Where the image size hints of the last render were rewritten, for mapping a task toggle back to its source position.
    private List<SourceShift> _sourceShifts = [];
    private CancellationTokenSource? _sourceLoadCts;

    private static readonly MarkdownPipeline DefaultPipeline =
        new MarkdownPipelineBuilder().UseGitHubExtensions().Build();

    /// <summary>The pipeline in effect: the viewer's, else the global default, else the built-in one.</summary>
    private MarkdownPipeline EffectivePipeline => Pipeline ?? MarkdownViewerDefaults.Pipeline ?? DefaultPipeline;

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
    /// Defines the <see cref="Source"/> property.
    /// </summary>
    public static readonly StyledProperty<Uri?> SourceProperty =
        AvaloniaProperty.Register<MarkdownViewer, Uri?>(nameof(Source));

    /// <summary>
    /// Defines the <see cref="ImageResizeMode"/> property.
    /// </summary>
    public static readonly StyledProperty<ImageResizeMode> ImageResizeModeProperty =
        AvaloniaProperty.Register<MarkdownViewer, ImageResizeMode>(nameof(ImageResizeMode), ImageResizeMode.ScaleDownToFit);

    /// <summary>
    /// Defines the <see cref="ShowCodeBlockLanguage"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> ShowCodeBlockLanguageProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(ShowCodeBlockLanguage), true);

    /// <summary>
    /// Defines the <see cref="ShowCodeBlockCopyButton"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> ShowCodeBlockCopyButtonProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(ShowCodeBlockCopyButton), true);

    /// <summary>
    /// Defines the <see cref="ShowCodeBlockCopyButtonOnHover"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> ShowCodeBlockCopyButtonOnHoverProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(ShowCodeBlockCopyButtonOnHover));

    /// <summary>
    /// Defines the <see cref="ShowCodeBlockLineNumbers"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> ShowCodeBlockLineNumbersProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(ShowCodeBlockLineNumbers));

    /// <summary>
    /// Defines the <see cref="WrapCodeBlockLines"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> WrapCodeBlockLinesProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(WrapCodeBlockLines));

    /// <summary>
    /// Defines the <see cref="CodeBlockMaxHeight"/> property.
    /// </summary>
    public static readonly StyledProperty<double> CodeBlockMaxHeightProperty =
        AvaloniaProperty.Register<MarkdownViewer, double>(nameof(CodeBlockMaxHeight), double.PositiveInfinity,
            validate: value => value >= 0);

    /// <summary>
    /// Defines the <see cref="AreTaskListsInteractive"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> AreTaskListsInteractiveProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(AreTaskListsInteractive));

    /// <summary>
    /// Defines the <see cref="AreHeadingsFoldable"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> AreHeadingsFoldableProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(AreHeadingsFoldable));

    /// <summary>
    /// Defines the <see cref="IsImageZoomEnabled"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsImageZoomEnabledProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(IsImageZoomEnabled), true);

    /// <summary>
    /// Defines the <see cref="DetectTextDirection"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> DetectTextDirectionProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(DetectTextDirection), true);

    /// <summary>
    /// Defines the <see cref="TableOfContentsMaxDepth"/> property.
    /// </summary>
    public static readonly StyledProperty<int> TableOfContentsMaxDepthProperty =
        AvaloniaProperty.Register<MarkdownViewer, int>(nameof(TableOfContentsMaxDepth), 6);

    /// <summary>
    /// Defines the <see cref="TableOfContents"/> property.
    /// </summary>
    public static readonly DirectProperty<MarkdownViewer, IReadOnlyList<TocEntry>> TableOfContentsProperty =
        AvaloniaProperty.RegisterDirect<MarkdownViewer, IReadOnlyList<TocEntry>>(
            nameof(TableOfContents), o => o.TableOfContents);

    /// <summary>
    /// Defines the <see cref="FindMatchCount"/> property.
    /// </summary>
    public static readonly DirectProperty<MarkdownViewer, int> FindMatchCountProperty =
        AvaloniaProperty.RegisterDirect<MarkdownViewer, int>(nameof(FindMatchCount), o => o.FindMatchCount);

    /// <summary>
    /// Defines the <see cref="CurrentFindMatchIndex"/> property.
    /// </summary>
    public static readonly DirectProperty<MarkdownViewer, int> CurrentFindMatchIndexProperty =
        AvaloniaProperty.RegisterDirect<MarkdownViewer, int>(nameof(CurrentFindMatchIndex), o => o.CurrentFindMatchIndex);

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
    /// Gets or sets a URI that points to a Markdown document to load and render.
    /// Supports <c>avares://</c> embedded resources, <c>file://</c>, and <c>http/https</c> schemes.
    /// A <c>#fragment</c> scrolls to the matching anchor once the document has rendered; for
    /// <c>avares://</c> the fragment starts at the first <c>#</c>, so asset paths containing
    /// <c>#</c> are not supported.
    /// When both <see cref="Source"/> and <see cref="Markdown"/> are set,
    /// <see cref="Source"/> takes precedence.
    /// </summary>
    public Uri? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>
    /// Gets or sets how images without an explicit "=WxH" size hint are scaled
    /// relative to their container. Defaults to <see cref="ImageResizeMode.ScaleDownToFit"/>.
    /// </summary>
    public ImageResizeMode ImageResizeMode
    {
        get => GetValue(ImageResizeModeProperty);
        set => SetValue(ImageResizeModeProperty, value);
    }

    /// <summary>
    /// Gets or sets whether code blocks show their language (the info string of a fenced code
    /// block, e.g. <c>csharp</c>) in a toolbar above the code. Defaults to <c>true</c>.
    /// Can be set from a style, e.g. for every viewer of an application.
    /// </summary>
    public bool ShowCodeBlockLanguage
    {
        get => GetValue(ShowCodeBlockLanguageProperty);
        set => SetValue(ShowCodeBlockLanguageProperty, value);
    }

    /// <summary>
    /// Gets or sets whether code blocks show a button in a toolbar above the code that copies
    /// the code to the clipboard. Display formulas of the <c>MarkView.Avalonia.Math</c> package
    /// get one in their top right corner, which copies their TeX source. Defaults to <c>true</c>.
    /// Can be set from a style, e.g. for every viewer of an application.
    /// </summary>
    public bool ShowCodeBlockCopyButton
    {
        get => GetValue(ShowCodeBlockCopyButtonProperty);
        set => SetValue(ShowCodeBlockCopyButtonProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the copy buttons of <see cref="ShowCodeBlockCopyButton"/> only show while
    /// the pointer is over their block, while they have keyboard focus, and while they confirm a copy.
    /// Hidden, a button keeps its place, its Tab stop and its accessible name. Defaults to
    /// <c>false</c>. Switching it restyles the buttons without re-rendering the document; the viewer
    /// has the <c>:copy-on-hover</c> pseudo-class while it is on.
    /// Can be set from a style, e.g. for every viewer of an application.
    /// </summary>
    public bool ShowCodeBlockCopyButtonOnHover
    {
        get => GetValue(ShowCodeBlockCopyButtonOnHoverProperty);
        set => SetValue(ShowCodeBlockCopyButtonOnHoverProperty, value);
    }

    /// <summary>
    /// Gets or sets whether code blocks show the number of each line in a gutter next to the code.
    /// The numbers are not part of the code: text selection and the copy button never include them.
    /// Defaults to <c>false</c>. Can be set from a style, e.g. for every viewer of an application.
    /// </summary>
    public bool ShowCodeBlockLineNumbers
    {
        get => GetValue(ShowCodeBlockLineNumbersProperty);
        set => SetValue(ShowCodeBlockLineNumbersProperty, value);
    }

    /// <summary>
    /// Gets or sets whether code blocks wrap lines longer than the block's width. When <c>false</c>
    /// (the default) code lines never wrap and run past the block's right edge, so they can only be
    /// read in full when the viewer scrolls horizontally.
    /// Can be set from a style, e.g. for every viewer of an application.
    /// </summary>
    public bool WrapCodeBlockLines
    {
        get => GetValue(WrapCodeBlockLinesProperty);
        set => SetValue(WrapCodeBlockLinesProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum height of a code block, its toolbar excluded. A taller block keeps
    /// this height and scrolls vertically inside itself; once it reaches either end, the mouse wheel
    /// scrolls the document again. Defaults to <see cref="double.PositiveInfinity"/> (no limit).
    /// Can be set from a style, e.g. for every viewer of an application.
    /// </summary>
    public double CodeBlockMaxHeight
    {
        get => GetValue(CodeBlockMaxHeightProperty);
        set => SetValue(CodeBlockMaxHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets whether task-list items (<c>- [ ]</c> / <c>- [x]</c>) render a checkbox the user can
    /// toggle, raising <see cref="TaskToggled"/>. When <c>false</c> (the default) they render a static
    /// check glyph. The viewer never rewrites <see cref="Markdown"/> itself: the host writes the change
    /// back to its source from the event.
    /// </summary>
    public bool AreTaskListsInteractive
    {
        get => GetValue(AreTaskListsInteractiveProperty);
        set => SetValue(AreTaskListsInteractiveProperty, value);
    }

    /// <summary>
    /// Gets or sets whether each heading starts with a fold button that hides or shows its section: the
    /// blocks that follow it, up to the next heading of the same or a higher level. Sections start
    /// expanded; the button also answers Enter and Space, and is a Tab stop. Navigating to a heading
    /// (<see cref="ScrollToAnchor"/>) or a find match in a collapsed section expands it, and hidden
    /// sections are left out of selection and copying. Defaults to <c>false</c>. Can be set from a style,
    /// e.g. for every viewer of an application.
    /// </summary>
    public bool AreHeadingsFoldable
    {
        get => GetValue(AreHeadingsFoldableProperty);
        set => SetValue(AreHeadingsFoldableProperty, value);
    }

    /// <summary>
    /// Gets or sets whether clicking an image opens it in a lightbox over the viewer: the image as large
    /// as fits, never enlarged past its natural size, on a dimmed backdrop. Clicking anywhere, the close
    /// button or Escape closes it, and focus returns to where it was. A zoomable image shows the zoom-in
    /// cursor and also opens with Enter or Space while it has focus; an image inside a link opens the
    /// link instead. Defaults to <c>true</c>. Can be set from a style, e.g. for every viewer of an application.
    /// </summary>
    public bool IsImageZoomEnabled
    {
        get => GetValue(IsImageZoomEnabledProperty);
        set => SetValue(IsImageZoomEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets whether each block takes the direction of its own text, as HTML's <c>dir="auto"</c> does:
    /// a paragraph, heading, list, quote or table whose first letter is Hebrew or Arabic is laid out right to
    /// left (lists and quotes mirrored, the first column of a table on the right), one whose first letter is
    /// Latin left to right. A block without a letter keeps the viewer's <see cref="Visual.FlowDirection"/>,
    /// code blocks are always left to right, and an HTML <c>dir</c> attribute wins over the text.
    /// When <c>false</c>, every block takes the viewer's <see cref="Visual.FlowDirection"/>.
    /// Defaults to <c>true</c>. Can be set from a style, e.g. for every viewer of an application.
    /// </summary>
    public bool DetectTextDirection
    {
        get => GetValue(DetectTextDirectionProperty);
        set => SetValue(DetectTextDirectionProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum heading level included in <see cref="TableOfContents"/>.
    /// Headings deeper than this level, and their descendants, are excluded. Defaults to 6 (no limit).
    /// </summary>
    public int TableOfContentsMaxDepth
    {
        get => GetValue(TableOfContentsMaxDepthProperty);
        set => SetValue(TableOfContentsMaxDepthProperty, value);
    }

    /// <summary>
    /// Gets the nested heading tree for the current document, filtered by
    /// <see cref="TableOfContentsMaxDepth"/>. Recomputed after every render. Pass an
    /// entry's <see cref="TocEntry.Slug"/> to <see cref="ScrollToAnchor"/> to navigate to it.
    /// </summary>
    public IReadOnlyList<TocEntry> TableOfContents
    {
        get;
        private set => SetAndRaise(TableOfContentsProperty, ref field, value);
    } = [];

    /// <summary>
    /// Gets the number of matches of the active search (see <see cref="Find"/>) in the rendered
    /// document, including those in collapsed content; 0 when there is no active search.
    /// </summary>
    public int FindMatchCount
    {
        get;
        private set => SetAndRaise(FindMatchCountProperty, ref field, value);
    }

    /// <summary>
    /// Gets the zero-based index of the current match of the active search, the one
    /// <see cref="FindNext"/> and <see cref="FindPrevious"/> moved to, or -1 when no match is current.
    /// </summary>
    public int CurrentFindMatchIndex
    {
        get;
        private set => SetAndRaise(CurrentFindMatchIndexProperty, ref field, value);
    } = -1;

    /// <summary>
    /// Extensions that customise the renderer before each render pass.
    /// Add entries before setting <see cref="Markdown"/> or assigning
    /// a new <see cref="Pipeline"/>; each extension's
    /// <see cref="IMarkViewExtension.Register"/> is called once per render.
    /// </summary>
    public IList<IMarkViewExtension> Extensions { get; } = [];

    /// <summary>
    /// Identifies the <see cref="LinkClicked"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<LinkClickedEventArgs> LinkClickedEvent =
        RoutedEvent.Register<MarkdownViewer, LinkClickedEventArgs>(
            nameof(LinkClicked), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised when a hyperlink in the rendered markdown is clicked.
    /// Bubbles up the visual tree; subscribe globally with
    /// <c>MarkdownViewer.LinkClickedEvent.AddClassHandler&lt;MarkdownViewer&gt;(...)</c>.
    /// </summary>
    public event EventHandler<LinkClickedEventArgs>? LinkClicked
    {
        add => AddHandler(LinkClickedEvent, value);
        remove => RemoveHandler(LinkClickedEvent, value);
    }

    /// <summary>
    /// Identifies the <see cref="TaskToggled"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<TaskToggledEventArgs> TaskToggledEvent =
        RoutedEvent.Register<MarkdownViewer, TaskToggledEventArgs>(
            nameof(TaskToggled), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised when the user toggles a task-list checkbox while <see cref="AreTaskListsInteractive"/>
    /// is <c>true</c>. The checkbox already shows the new state; the viewer does not change
    /// <see cref="Markdown"/>. <see cref="TaskToggledEventArgs.SourceStart"/> and
    /// <see cref="TaskToggledEventArgs.SourceLength"/> locate the item's <c>[ ]</c> / <c>[x]</c> marker
    /// in the rendered markdown (the <see cref="Markdown"/> string as set, or the text loaded from
    /// <see cref="Source"/>). Bubbles up the visual tree.
    /// </summary>
    public event EventHandler<TaskToggledEventArgs>? TaskToggled
    {
        add => AddHandler(TaskToggledEvent, value);
        remove => RemoveHandler(TaskToggledEvent, value);
    }

    static MarkdownViewer()
    {
        MarkdownProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.OnMarkdownChanged());
        PipelineProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.RenderMarkdown());
        BaseUriProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.RenderMarkdown());
        SourceProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.OnSourceChanged());
        ImageResizeModeProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.RenderMarkdown());
        ShowCodeBlockLanguageProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.RenderMarkdown());
        ShowCodeBlockCopyButtonProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.RenderMarkdown());
        ShowCodeBlockCopyButtonOnHoverProperty.Changed.AddClassHandler<MarkdownViewer>(
            (x, e) => x.PseudoClasses.Set(":copy-on-hover", e.GetNewValue<bool>()));
        ShowCodeBlockLineNumbersProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.RenderMarkdown());
        WrapCodeBlockLinesProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.RenderMarkdown());
        CodeBlockMaxHeightProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.RenderMarkdown());
        AreTaskListsInteractiveProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.RenderMarkdown());
        AreHeadingsFoldableProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.RenderMarkdown());
        IsImageZoomEnabledProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.RenderMarkdown());
        DetectTextDirectionProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.RenderMarkdown());
        TableOfContentsMaxDepthProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.RebuildTableOfContents());
        FocusableProperty.OverrideDefaultValue<MarkdownViewer>(true);
    }

    // ── Source loading ────────────────────────────────────────────────────────

    private void OnSourceChanged()
    {
        // The session appends to Markdown, which Source replaces: it ends now, not when the load renders.
        EndStreamingSession();
        _sourceLoadCts?.Cancel();
        _sourceLoadCts = null;
        _sourceMarkdown = null;

        var source = Source;
        if (source is null)
        {
            RenderMarkdown();
            return;
        }

        var fragment = source.Fragment.TrimStart('#');

        switch (source.Scheme)
        {
            // avares:// is a ManifestResourceStream — memory-mapped into the loaded assembly,
            // effectively zero I/O cost. No async API exists; synchronous read is appropriate.
            case "avares":
            {
                // Avalonia parses avares:// with GenericUriParserOptions.NoFragment, which keeps
                // "#fragment" in the resource path; split it off the original string instead.
                var hashIndex = source.OriginalString.IndexOf('#');
                if (hashIndex >= 0)
                {
                    fragment = source.OriginalString[(hashIndex + 1)..];
                    source = new Uri(source.OriginalString[..hashIndex]);
                }

                using var stream = AssetLoader.Open(source);
                using var reader = new StreamReader(stream);
                _sourceMarkdown = reader.ReadToEnd();
                RenderMarkdown();
                ScrollToAnchorAfterRender(fragment);
                break;
            }
            case "file":
            case "http":
            case "https":
                _ = LoadFromUriAsync(source, fragment);
                break;
        }
    }

    private async Task LoadFromUriAsync(Uri uri, string fragment)
    {
        var cts = new CancellationTokenSource();
        _sourceLoadCts = cts;
        try
        {
            string text;
            if (uri.Scheme == "file")
            {
                using var stream = File.OpenRead(uri.LocalPath);
                using var reader = new StreamReader(stream);
                text = await reader.ReadToEndAsync(cts.Token);
            }
            else
            {
                using var stream = await SharedHttpClient.Instance.GetStreamAsync(uri, cts.Token);
                using var reader = new StreamReader(stream);
                text = await reader.ReadToEndAsync(cts.Token);
            }

            if (!cts.IsCancellationRequested)
            {
                _sourceMarkdown = text;
                RenderMarkdown();
                ScrollToAnchorAfterRender(fragment);
            }
        }
        catch (OperationCanceledException) { }
        catch (HttpRequestException) { }
        catch (IOException) { }
    }

    // Anchor targets are only positioned after the next layout pass, so scrolling
    // must be deferred past the render that just set Content.
    private void ScrollToAnchorAfterRender(string fragment)
    {
        if (string.IsNullOrEmpty(fragment)) return;
        Dispatcher.UIThread.Post(() => ScrollToAnchor(fragment, animate: false), DispatcherPriority.Loaded);
    }

    private static Uri? InferBaseUri(Uri? source)
    {
        if (source is null) return null;
        var abs = source.AbsoluteUri;
        var lastSlash = abs.LastIndexOf('/');
        return lastSlash > 0 ? new Uri(abs[..(lastSlash + 1)]) : null;
    }

    private void OnMarkdownChanged()
    {
        if (_sourceMarkdown is null && string.Equals(Markdown, _renderedMarkdown, StringComparison.Ordinal))
            return;
        RenderMarkdown();
    }

    internal void RenderMarkdown()
    {
        // Whatever is rendered now replaces what an active streaming session renders, so the session ends.
        EndStreamingSession();
        var markdown = _sourceMarkdown ?? Markdown;
        _renderedMarkdown = markdown;
        // A focused link is about to be replaced; keep focus in the viewer rather than losing it.
        var focusWasInDocument = IsKeyboardFocusWithin && !IsFocused;
        if (string.IsNullOrEmpty(markdown))
        {
            Content = null;
            CloseLightbox();
            _anchors = new(StringComparer.OrdinalIgnoreCase);
            _headingEntries = [];
            RebuildTableOfContents();
            SetSelectionLayer(null);
            _linkFocusLayer = null;
            UpdateFind(CurrentFindMatchIndex);
            if (focusWasInDocument) Focus();
            return;
        }

        var markdownText = PreprocessImageSizes(markdown, out _sourceShifts);
        var document = Markdig.Markdown.Parse(markdownText, EffectivePipeline);

        var renderer = CreateRenderer();
        renderer.Render(document);

        _anchors = new Dictionary<string, Control>(renderer.Anchors, StringComparer.OrdinalIgnoreCase);
        _headingEntries = [.. renderer.HeadingEntries];
        RebuildTableOfContents();

        // Set up document-wide selection layer
        var layer = new DocumentSelectionLayer();
        RegisterBlocks(layer, renderer.RootPanel);
        SetSelectionLayer(layer);

        // Keyboard stand-ins for the document's links
        _linkFocusLayer = LinkFocusLayer.Create(renderer.RootPanel, ActivateLink);

        InstallDocument(renderer, layer, _linkFocusLayer, focusWasInDocument);

        // Every new render is treated as a fresh document — reset scroll position to the
        // top, since PART_ScrollViewer now persists across renders (it lives in the
        // template, not rebuilt per-render) and would otherwise keep its old offset.
        if (_scrollViewer is not null)
            _scrollViewer.Offset = default;

        // The active search goes on in the new document, keeping its current match where it can,
        // without scrolling to it.
        UpdateFind(CurrentFindMatchIndex);
    }

    /// <summary>
    /// Creates a renderer configured from the viewer's properties, hooked to its events, with the
    /// extensions registered and the pipeline set up. Renders nothing.
    /// </summary>
    private AvaloniaRenderer CreateRenderer()
    {
        var renderer = new AvaloniaRenderer
        {
            BaseUri = BaseUri ?? InferBaseUri(Source),
            ImageResizeMode = ImageResizeMode,
            ShowCodeBlockLanguage = ShowCodeBlockLanguage,
            ShowCodeBlockCopyButton = ShowCodeBlockCopyButton,
            ShowCodeBlockLineNumbers = ShowCodeBlockLineNumbers,
            WrapCodeBlockLines = WrapCodeBlockLines,
            CodeBlockMaxHeight = CodeBlockMaxHeight,
            AreTaskListsInteractive = AreTaskListsInteractive,
            AreHeadingsFoldable = AreHeadingsFoldable,
            TimeProvider = TimeProvider,
            IsImageZoomEnabled = IsImageZoomEnabled,
            DetectTextDirection = DetectTextDirection,
        };
        renderer.LinkClicked += OnLinkClicked;
        renderer.ImageZoomRequested += OpenLightbox;
        renderer.TaskToggled += (_, e) => OnTaskToggled(e);

        // Extensions register before pipeline.Setup() so they can swap renderers.
        // Globals first, then per-instance; skip exact-reference duplicates.
        // Allocate the dedup HashSet only when at least one list is non-empty.
        if (MarkdownViewerDefaults.Extensions.Count > 0 || Extensions.Count > 0)
        {
            var seen = new HashSet<IMarkViewExtension>(ReferenceEqualityComparer.Instance);

            foreach (var ext in MarkdownViewerDefaults.Extensions)
            {
                if (seen.Add(ext))
                    ext.Register(renderer);
            }
            foreach (var ext in Extensions)
            {
                if (seen.Add(ext))
                    ext.Register(renderer);
            }
        }

        var pipeline = EffectivePipeline;
        pipeline.Setup(renderer);
        // Markdig only sets these extensions up for its own HTML renderer.
        renderer.ApplyAttributeClasses = pipeline.Extensions.Contains<GenericAttributesExtension>();
        if (pipeline.Extensions.Find<SmartyPantsExtension>() is { } smartyPants
            && renderer.ObjectRenderers.Find<SmartyPantInlineRenderer>() is { } smartyPantRenderer)
            smartyPantRenderer.Options = smartyPants.Options;

        return renderer;
    }

    /// <summary>
    /// Replaces the viewer's content with the rendered document: its root panel with the selection and
    /// link focus layers on top, in a single-cell Grid.
    /// </summary>
    private void InstallDocument(AvaloniaRenderer renderer, DocumentSelectionLayer layer, LinkFocusLayer linkLayer, bool focusWasInDocument)
    {
        // Stack panel + transparent selection and link focus layers in a single-cell Grid
        // markdown-document lets a theme size the whole document, e.g. cap its width
        var contentGrid = new Grid { Classes = { "markdown-document" } };
        contentGrid.Children.Add(renderer.RootPanel);
        contentGrid.Children.Add(layer);
        contentGrid.Children.Add(linkLayer);
        // Tab moves through the document in reading order (ICustomKeyboardNavigation.GetNext),
        // so default navigation must not walk into it in visual-tree order as well.
        KeyboardNavigation.SetTabNavigation(contentGrid, KeyboardNavigationMode.None);

        // Tunnel handlers fire before any child receives the event
        contentGrid.AddHandler(InputElement.PointerPressedEvent,
            OnContentPointerPressed, RoutingStrategies.Tunnel);
        contentGrid.AddHandler(InputElement.PointerMovedEvent,
            OnContentPointerMoved, RoutingStrategies.Tunnel);
        contentGrid.AddHandler(InputElement.PointerReleasedEvent,
            OnContentPointerReleased, RoutingStrategies.Tunnel);
        contentGrid.PointerCaptureLost += OnContentPointerCaptureLost;
        contentGrid.AddHandler(InputElement.ContextRequestedEvent, OnContentContextRequested);

        Content = contentGrid;
        // The image it shows may not be part of the new document. Closed once the old document is gone,
        // so that focus does not return to one of its controls.
        CloseLightbox();
        if (focusWasInDocument) Focus();
    }

    // ── Source positions ──────────────────────────────────────────────────────

    /// <summary>
    /// Rewrites each "![alt](url =WxH)" size hint as a "=WxH" link title Markdig can parse. Each
    /// rewrite changes the text's length (and can drop line breaks), so <paramref name="shifts"/>
    /// records, in order, where each rewrite ends in the returned text and how far positions after
    /// it have moved, for <see cref="ToSourcePosition"/> to map them back.
    /// </summary>
    private static string PreprocessImageSizes(string markdown, out List<SourceShift> shifts)
    {
        var recorded = new List<SourceShift>();
        int offset = 0, lines = 0;
        var text = ImageSizePreprocessorRegex().Replace(markdown, m =>
        {
            if (m.Groups[1].Success)
                return m.Value;
            var replacement = $"{m.Groups[2].Value}{m.Groups[3].Value} \"={m.Groups[4].Value}\"{m.Groups[5].Value}";
            offset += replacement.Length - m.Length;
            lines += replacement.AsSpan().Count('\n') - m.ValueSpan.Count('\n');
            // Stryker disable once Arithmetic : only compared against positions outside the image syntax, which any point inside the rewrite tells apart as well as its end
            recorded.Add(new SourceShift(m.Index + m.Length + offset, offset, lines));
            return replacement;
        });
        shifts = recorded;
        return text;
    }

    /// <summary>
    /// Maps a position (and its line) in the preprocessed text back to the markdown as given. Valid for
    /// positions outside the rewritten image syntax, such as a task-list marker.
    /// </summary>
    private static (int Position, int Line) ToSourcePosition(List<SourceShift> shifts, int position, int line)
    {
        // The rewrites before the position are those ending at or before it; the last of them
        // carries the accumulated shift.
        for (int i = shifts.Count - 1; i >= 0; i--)
        {
            // Stryker disable once Equality : no position that is mapped (a task marker) can sit right where an image ends
            if (shifts[i].End <= position)
                return (position - shifts[i].Offset, line - shifts[i].Lines);
        }
        return (position, line);
    }

    /// <param name="End">Where the rewrite ends in the preprocessed text.</param>
    /// <param name="Offset">Total length change of this and all earlier rewrites.</param>
    /// <param name="Lines">Total line-count change of this and all earlier rewrites.</param>
    private readonly record struct SourceShift(int End, int Offset, int Lines);

    private void OnTaskToggled(TaskToggledEventArgs e)
    {
        var (start, line) = ToSourcePosition(_sourceShifts, e.SourceStart, e.Line);
        if (_renderedMarkdown is { } rendered)
            _renderedMarkdown = string.Concat(rendered.AsSpan(0, start), e.IsChecked ? "[x]" : "[ ]", rendered.AsSpan(start + e.SourceLength));
        // The session's next update renders its own text, which must not revert the toggle.
        _streamingSession?.PatchText(start, e.SourceLength, e.IsChecked ? "[x]" : "[ ]");
        RaiseEvent(new TaskToggledEventArgs(e.IsChecked, start, e.SourceLength, line) { RoutedEvent = TaskToggledEvent });
    }

    private void RebuildTableOfContents() =>
        TableOfContents = TocEntry.BuildTree(_headingEntries, TableOfContentsMaxDepth);

    // ── Block registration ────────────────────────────────────────────────────

    private static void RegisterBlocks(DocumentSelectionLayer layer, Panel panel) =>
        RegisterBlocks(layer, panel, 0);

    /// <summary>
    /// Registers the selectable blocks of <c>panel.Children[startIndex..]</c>, the children of a renderer's
    /// root panel, in document order, with no enclosing list item's marker. Each entry records the top-level
    /// block it renders part of.
    /// </summary>
    internal static void RegisterBlocks(DocumentSelectionLayer layer, Panel panel, int startIndex)
    {
        TextBlock? marker = null;
        for (var i = startIndex; i < panel.Children.Count; i++)
        {
            var first = layer.EntryCount;
            RegisterBlocks(layer, panel, ref marker, i, i + 1);
            var block = AvaloniaRenderer.GetTopLevelBlock(panel.Children[i]);
            for (var k = first; k < layer.EntryCount; k++)
                layer.Entries[k].TopLevelBlock = block;
        }
    }

    /// <summary>
    /// Registers every selectable block under <paramref name="panel"/> in document order.
    /// A non-null <paramref name="marker"/> (the enclosing list item's bullet, number or
    /// check glyph) is registered as its own entry just before the first registered entry,
    /// then is cleared, so a list item without selectable content contributes no marker.
    /// Only <c>panel.Children[startIndex..endIndex]</c> are registered.
    /// </summary>
    private static void RegisterBlocks(DocumentSelectionLayer layer, Panel panel, ref TextBlock? marker,
        int startIndex = 0, int endIndex = int.MaxValue)
    {
        for (var i = startIndex; i < Math.Min(endIndex, panel.Children.Count); i++)
        {
            var child = panel.Children[i];
            switch (child)
            {
                // A control a renderer gave copy text to (a formula, a diagram) is selected as a whole,
                // whatever it contains.
                case Control blockControl when MarkdownSelection.GetCopyText(blockControl) is { } copyText:
                    Register(layer, IndexEntry.ForBlockControl(blockControl, copyText), ref marker);
                    break;
                case MarkdownSelectableTextBlock tb:
                    var text = MarkdownSelectableTextBlock.ExtractPlainText(tb.Inlines!, out var embeddedText);
                    Register(layer, new IndexEntry(tb, text, "\n", embeddedText), ref marker);
                    break;
                case Border border when border.Classes.Contains("markdown-code-block")
                    && CodeBlockToolbar.FindCode(border) is { } codeTb:
                    Register(layer, new IndexEntry(codeTb,
                        codeTb.Inlines != null
                            ? MarkdownSelectableTextBlock.ExtractPlainText(codeTb.Inlines)
                            : codeTb.Text ?? string.Empty), ref marker);
                    break;
                case Panel listPanel when listPanel.Classes.Contains("markdown-list"):
                    RegisterListItems(layer, listPanel);
                    break;
                case Grid tableGrid when tableGrid.Classes.Contains("markdown-table"):
                    RegisterTableRows(layer, tableGrid, ref marker);
                    break;
                case Border { Child: Panel borderPanel }:
                    RegisterBlocks(layer, borderPanel, ref marker);
                    break;
                case Panel nested:
                    RegisterBlocks(layer, nested, ref marker);
                    break;
            }
        }
    }

    private static void RegisterListItems(DocumentSelectionLayer layer, Panel listPanel)
    {
        foreach (var itemGrid in listPanel.Children.OfType<Grid>())
        {
            // Column 0 holds the marker TextBlock (bullet, number or task-list check glyph), or
            // the toggle wrapping the check glyph of an interactive task-list item.
            // Column 1 holds the content StackPanel
            TextBlock? markerTb = null;
            Panel? contentPanel = null;
            foreach (var gridChild in itemGrid.Children)
            {
                if (gridChild is TextBlock tb && Grid.GetColumn(tb) == 0)
                    markerTb = tb;
                else if (gridChild is ToggleButton { Content: TextBlock glyph } && Grid.GetColumn(gridChild) == 0)
                    markerTb = glyph;
                else if (gridChild is Panel pnl && Grid.GetColumn(pnl) == 1)
                    contentPanel = pnl;
            }
            if (contentPanel is null) continue;

            var marker = string.IsNullOrEmpty(markerTb?.Text) ? null : markerTb;
            RegisterBlocks(layer, contentPanel, ref marker);
        }
    }

    private static void Register(DocumentSelectionLayer layer, IndexEntry entry, ref TextBlock? marker)
    {
        if (marker is not null)
        {
            layer.Register(IndexEntry.ForMarker(marker));
            marker = null;
        }
        layer.Register(entry);
    }

    private static void RegisterTableRows(DocumentSelectionLayer layer, Grid tableGrid, ref TextBlock? marker)
    {
        // TableRenderer adds cells in row-major order (rowIndex / colIndex ascending),
        // so iterating Children directly avoids the O(N log N) SortedDictionary sort.
        // Collect borders first, then emit each with "\t" or "\n" based on row boundary.
        var cells = new List<Border>(tableGrid.Children.Count);
        foreach (var child in tableGrid.Children)
            if (child is Border b) cells.Add(b);

        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            var tb = FindFirstTextBlock(cell);
            if (tb is null) continue;

            Dictionary<int, string>? embeddedText = null;
            var text = cell.Child is Panel p ? ExtractPanelText(p, out embeddedText) : string.Empty;
            // Separator: "\n" if this is the last cell in its row, "\t" otherwise.
            bool isLastInRow = i == cells.Count - 1
                || Grid.GetRow(cells[i + 1]) != Grid.GetRow(cell);
            Register(layer, new IndexEntry(tb, text, isLastInRow ? "\n" : "\t", embeddedText), ref marker);
        }
    }

    /// <summary>
    /// Extracts plain text from a panel, joining child texts with a space. <paramref name="embeddedText"/>
    /// maps the position of each embedded control to its copy text, as in
    /// <c>MarkdownSelectableTextBlock.ExtractPlainText</c>.
    /// </summary>
    private static string ExtractPanelText(Panel panel, out Dictionary<int, string>? embeddedText)
    {
        var sb = new StringBuilder();
        embeddedText = null;
        foreach (var child in panel.Children)
        {
            switch (child)
            {
                case MarkdownSelectableTextBlock tb:
                    var t = MarkdownSelectableTextBlock.ExtractPlainText(tb.Inlines!, out var tbEmbeddedText);
                    AppendSpaced(sb, t, tbEmbeddedText, ref embeddedText);
                    break;
                case Panel nested:
                    var nt = ExtractPanelText(nested, out var nestedEmbeddedText);
                    AppendSpaced(sb, nt, nestedEmbeddedText, ref embeddedText);
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Appends non-empty <paramref name="text"/> to <paramref name="sb"/>, space-separated from what
    /// precedes it, and shifts its embedded-control positions into <paramref name="embeddedText"/>.
    /// </summary>
    private static void AppendSpaced(StringBuilder sb, string text, Dictionary<int, string>? textEmbeddedText,
        ref Dictionary<int, string>? embeddedText)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (sb.Length > 0) sb.Append(' ');
        if (textEmbeddedText is not null)
        {
            embeddedText ??= [];
            foreach (var (position, copyText) in textEmbeddedText)
                embeddedText[sb.Length + position] = copyText;
        }
        sb.Append(text);
    }

    /// <summary>Returns the first <see cref="TextBlock"/> found in a control subtree.</summary>
    internal static TextBlock? FindFirstTextBlock(Control control)
    {
        switch (control)
        {
            case TextBlock tb: return tb;
            case Border { Child: Control child }: return FindFirstTextBlock(child);
            case Panel panel:
                foreach (var c in panel.Children)
                {
                    var found = FindFirstTextBlock(c);
                    if (found is not null) return found;
                }
                return null;
            default: return null;
        }
    }

    // ── Pointer handlers ──────────────────────────────────────────────────────

    private void OnContentPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _tabStart = null;
        if (!e.Properties.IsLeftButtonPressed || _selectionLayer is null) return;
        Focus();
        _isDragging = false;
        _dragStart = e.GetPosition(_selectionLayer);
        _selectionLayer.OnPointerPressed(_dragStart);
    }

    private void OnContentPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_selectionLayer is null) return;
        var pos = e.GetPosition(_selectionLayer);

        if (e.Properties.IsLeftButtonPressed)
        {
            if (!_isDragging)
            {
                var delta = pos - _dragStart;
                var dist = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
                if (dist > DragThreshold)
                {
                    _isDragging = true;
                    e.Pointer.Capture((IInputElement)sender!);
                }
            }
            if (_isDragging)
            {
                _selectionLayer.OnPointerMoved(pos);
                e.Handled = true;
                return;
            }
        }

        // Update cursor on hover (no drag)
        UpdateHyperlinkCursor(pos);
    }

    private void OnContentPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            EndDrag();
            e.Pointer.Capture(null);
            return;
        }

        // Left click without drag — check for hyperlink. A right click opens the context menu instead.
        if (e.InitialPressMouseButton == MouseButton.Left && _selectionLayer is not null)
            TryFireHyperlinkClick(e.GetPosition(_selectionLayer));
    }

    // The window lost the pointer in the middle of a drag (e.g. another window took it): the drag is over.
    private void OnContentPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_isDragging) EndDrag();
    }

    private void EndDrag()
    {
        _isDragging = false;
        _selectionLayer?.EndSelecting();
    }

    private void UpdateHyperlinkCursor(Point posInLayer)
    {
        if (_selectionLayer is null) return;
        var entry = _selectionLayer.HitTestEntry(posInLayer);
        if (entry?.TextBlock is not MarkdownSelectableTextBlock mstb) return;

        // Set directly on the hit text block, not on this or on _selectionLayer (which is
        // IsHitTestVisible=false, so it's never the element Avalonia actually tracks as
        // hovered). Avalonia only re-pushes the OS cursor when the tracked "cursor element"
        // itself changes, or when that same element's own Cursor property changes — matching
        // how a Button's hover style resets on enter/exit. mstb is the real hit-test-visible
        // control for the whole row, so moving within it (e.g. off a link, same paragraph)
        // never changes the tracked element; only setting Cursor on mstb itself refreshes it.
        var origin = mstb.TranslatePoint(new Point(0, 0), _selectionLayer) ?? default;
        var hyperlink = mstb.HitTestHyperlink(posInLayer - origin);
        mstb.Cursor = hyperlink?.NavigateUri != null ? HandCursor : Cursor.Default;
    }

    private void TryFireHyperlinkClick(Point posInLayer)
    {
        if (HyperlinkAt(posInLayer) is { } hyperlink)
            ActivateLink(hyperlink);
    }

    /// <summary>The link at <paramref name="posInLayer"/>, a position in the selection layer, if any.</summary>
    private MarkdownHyperlink? HyperlinkAt(Point posInLayer)
    {
        if (_selectionLayer is null) return null;
        var entry = _selectionLayer.HitTestEntry(posInLayer);
        if (entry?.TextBlock is not MarkdownSelectableTextBlock mstb) return null;
        var origin = mstb.TranslatePoint(new Point(0, 0), _selectionLayer) ?? default;
        return mstb.HitTestHyperlink(posInLayer - origin);
    }

    /// <summary>Opens <paramref name="hyperlink"/>, whether it was clicked or activated from the keyboard.</summary>
    private void ActivateLink(MarkdownHyperlink hyperlink)
    {
        if (hyperlink.NavigateUri is { } uri)
            OnLinkClicked(this, new LinkClickedEventArgs(uri.ToString()));
    }

    // ── Find API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds every occurrence of <paramref name="text"/> in the rendered document and highlights it,
    /// replacing the previous search. Returns the number of matches, also given by
    /// <see cref="FindMatchCount"/>. No match is current yet: <see cref="FindNext"/> goes to the first one
    /// and <see cref="FindPrevious"/> to the last one. An empty <paramref name="text"/> clears the search,
    /// as <see cref="ClearFind"/> does.
    /// <para>
    /// A match never spans two blocks (paragraphs, headings, code blocks, table cells...). Text inside
    /// collapsed alerts and details blocks is searched too; moving to a match there expands its block.
    /// The source of formulas and diagrams, and list markers, are not searched.
    /// </para>
    /// <para>
    /// The search stays active until it is cleared: when the document is rendered again (e.g.
    /// <see cref="Markdown"/> or <see cref="Source"/> changes), it runs again on the new document, which
    /// keeps the current match index when the new document has that many matches (the last match
    /// otherwise), without scrolling to it.
    /// </para>
    /// </summary>
    /// <param name="text">The text to find.</param>
    /// <param name="options">Case sensitivity and whole-word matching; by default the search ignores case and
    /// matches inside words too.</param>
    public int Find(string text, MarkdownFindOptions options = MarkdownFindOptions.None)
    {
        ArgumentNullException.ThrowIfNull(text);
        _findText = text.Length > 0 ? text : null;
        _findOptions = options;
        UpdateFind(-1);
        return FindMatchCount;
    }

    /// <summary>
    /// Makes the next match of the active search current, wrapping around from the last match to the first,
    /// and scrolls it into view, expanding any collapsed alert, details block or heading section it is in. Returns <c>false</c>
    /// when there is no match.
    /// </summary>
    public bool FindNext() => MoveToFindMatch(1);

    /// <summary>
    /// Makes the previous match of the active search current, wrapping around from the first match to the
    /// last, and scrolls it into view, expanding any collapsed alert, details block or heading section it is in. Returns
    /// <c>false</c> when there is no match.
    /// </summary>
    public bool FindPrevious() => MoveToFindMatch(-1);

    /// <summary>Clears the active search and its highlights.</summary>
    public void ClearFind()
    {
        _findText = null;
        UpdateFind(-1);
    }

    /// <summary>
    /// Runs the active search on the rendered document and makes the match at <paramref name="currentIndex"/>
    /// current (the last match when there are fewer, none for -1).
    /// </summary>
    private void UpdateFind(int currentIndex)
    {
        var layer = _selectionLayer;
        if (_findText is null)
            layer?.ClearFind();
        var count = _findText is null || layer is null ? 0 : layer.Find(_findText, _findOptions);
        FindMatchCount = count;
        SetCurrentFindMatch(Math.Min(currentIndex, count - 1));
    }

    private void SetCurrentFindMatch(int index)
    {
        if (_selectionLayer is not null)
            _selectionLayer.CurrentFindMatch = index;
        CurrentFindMatchIndex = index;
    }

    private bool MoveToFindMatch(int step)
    {
        var count = FindMatchCount;
        if (count == 0) return false;

        // With no current match (-1), stepping forward reaches the first match; stepping backward
        // must reach the last one rather than the one before it.
        var current = CurrentFindMatchIndex;
        // Stryker disable once Equality : stepping back from the first match reaches the last one either way, and step is never 0
        var index = current < 0 && step < 0 ? count - 1 : (current + step + count) % count;
        SetCurrentFindMatch(index);
        BringFindMatchIntoView(_selectionLayer!.FindMatches[index]);
        return true;
    }

    /// <summary>
    /// Shows <paramref name="match"/>: expands the collapsed alerts, details blocks and heading sections it is in, then
    /// scrolls it into view, with a margin above and below it.
    /// </summary>
    private void BringFindMatchIntoView(FindMatch match)
    {
        const double margin = 16;
        var textBlock = match.Entry.TextBlock!;

        // A match inside a collapsed block has no position until the block is expanded and laid out.
        if (RevealFolded(textBlock))
            textBlock.UpdateLayout();

        // A viewer that is not shown has laid nothing out, so there is nothing to scroll.
        var matchBounds = match.GetBounds();
        if (matchBounds is null)
            return;
        var bounds = matchBounds.Value.Inflate(new Thickness(0, margin));
        textBlock.BringIntoView(bounds);

        // The nearest scroll viewer handles that request, and when it had to scroll, the request goes no
        // further: the document stays where it is when that is the scroll viewer of a code block with a
        // maximum height, and so does an application's scroll viewer around the viewer when it is the
        // document's. So each scroll viewer around the match then asks those around it in turn to show
        // the match where it now is.
        for (var scrollViewer = textBlock.FindAncestorOfType<ScrollViewer>();
             scrollViewer is not null;
             scrollViewer = scrollViewer.FindAncestorOfType<ScrollViewer>())
        {
            scrollViewer.UpdateLayout();
            var transform = textBlock.TransformToVisual(scrollViewer)!.Value;
            scrollViewer.BringIntoView(bounds.TransformToAABB(transform));
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && HandleScrollKey(e)) { e.Handled = true; return; }
        var hotkeys = Application.Current?.PlatformSettings?.HotkeyConfiguration;
        if (hotkeys is null) return;
        if (hotkeys.Copy.Any(g => g.Matches(e))) { _ = CopyToClipboardAsync(); e.Handled = true; }
        else if (hotkeys.SelectAll.Any(g => g.Matches(e))) { SelectAll(); e.Handled = true; }
    }

    // ── Link handling ─────────────────────────────────────────────────────────

    private async void OnLinkClicked(object? sender, LinkClickedEventArgs e)
    {
        if (e.Url.StartsWith('#'))
        {
            ScrollToAnchor(e.Url[1..]);
            // A link activated from the keyboard hands the focus to the viewer, so that Tab goes on
            // from the anchor rather than from the link.
            if (_tabStart is not null && IsKeyboardFocusWithin)
                Focus();
            e.Handled = true;
            return;
        }

        e.RoutedEvent = LinkClickedEvent;
        RaiseEvent(e);
        if (e.Handled) return;

        // No subscriber intercepted the link (e.g. to render a local document in-place) —
        // the platform launcher is the only cross-platform way to open an external URI
        // (desktop shell, mobile intent, or browser new-tab).
        if (!Uri.TryCreate(e.Url, UriKind.Absolute, out var uri)) return;
        var launcher = TopLevel.GetTopLevel(this)?.Launcher;
        if (launcher is not null)
            await launcher.LaunchUriAsync(uri);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_scrollViewer is not null)
            _scrollViewer.ScrollChanged -= OnScrollChanged;
        _scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        if (_scrollViewer is not null)
            _scrollViewer.ScrollChanged += OnScrollChanged;
        // Applying a template replaces every visual child, the open lightbox included.
        if (_lightbox is not null)
            VisualChildren.Add(_lightbox);
    }

    // ── Image lightbox ────────────────────────────────────────────────────────

    /// <summary>The open lightbox, or <c>null</c>.</summary>
    internal ImageLightbox? Lightbox => _lightbox;

    /// <summary>
    /// Shows <paramref name="image"/>'s picture in a lightbox laid over the viewer, as a child of the
    /// viewer itself rather than of its template or of the window's overlay layer, so it covers
    /// exactly the viewer with any template and on any platform. The lightbox takes focus.
    /// </summary>
    private void OpenLightbox(Image image)
    {
        if (_lightbox is not null || image.Source is null) return;
        var lightbox = new ImageLightbox(image, TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement());
        lightbox.CloseRequested += CloseLightbox;
        _lightbox = lightbox;
        LogicalChildren.Add(lightbox);
        VisualChildren.Add(lightbox);
        // A new visual child is only laid out with its parent.
        InvalidateMeasure();
        lightbox.Focus(NavigationMethod.Pointer);
    }

    private void CloseLightbox()
    {
        if (_lightbox is not { } lightbox) return;
        _lightbox = null;
        var hadFocus = lightbox.IsKeyboardFocusWithin;
        VisualChildren.Remove(lightbox);
        LogicalChildren.Remove(lightbox);
        // Focus returns to where it was, unless that is gone (e.g. with the document the lightbox
        // was closed for), or focus has moved elsewhere since.
        if (hadFocus && lightbox.ReturnFocus?.Focus() != true)
            Focus();
    }

    /// <summary>
    /// Expands the collapsed alerts, details elements and heading sections that hide <paramref name="control"/>.
    /// Returns <c>true</c> when one was expanded.
    /// </summary>
    private static bool RevealFolded(Control control) =>
        CollapsibleAlert.ExpandAncestors(control) | HeadingFold.Reveal(control);

    /// <summary>The clock the renderer times a language model's reasoning with. Tests replace it.</summary>
    internal TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>The ids of the rendered document's anchors, for tests.</summary>
    internal IEnumerable<string> AnchorIds => _anchors.Keys;

    /// <summary>
    /// Scrolls to the anchor (e.g. a heading's slug) named <paramref name="anchorId"/>, if the document
    /// has one. While the viewer itself has the focus, Tab then goes on from that anchor, as in a browser.
    /// The scroll is animated while <see cref="IsSmoothScrollingEnabled"/> is <c>true</c>.
    /// </summary>
    public void ScrollToAnchor(string anchorId) => ScrollToAnchor(anchorId, animate: true);

    private void ScrollToAnchor(string anchorId, bool animate)
    {
        StopSmoothScroll();
        _tabStart = null;
        if (!_anchors.TryGetValue(anchorId, out var control))
            return;
        // While the viewer itself has the focus, Tab goes on from here.
        _tabStart = control;

        // A heading inside a collapsed alert, details element or section has no position until it is expanded and laid out.
        if (RevealFolded(control))
            control.UpdateLayout();

        if (_scrollViewer is null)
        {
            control.BringIntoView();
            return;
        }

        // Translating to the ScrollViewer itself (rather than to Content) gives a
        // viewport-relative point that already accounts for Padding — Padding is applied
        // as the ContentPresenter's Margin *inside* the ScrollViewer, so Content's origin
        // is offset from the scrollable extent's origin by Padding.Top. Adding the current
        // Offset.Y back converts the viewport-relative point to absolute content-space
        // coordinates, which is what the new Offset value must be expressed in.
        var point = control.TranslatePoint(new Point(0, 0), _scrollViewer);
        if (point is null)
        {
            control.BringIntoView();
            return;
        }

        const double topMargin = 16;
        var scrollViewer = _scrollViewer;
        ScrollVerticallyTo(() => control.TranslatePoint(default, scrollViewer) is { } current
            ? scrollViewer.Offset.Y + current.Y - topMargin
            : null, animate);
    }
}
