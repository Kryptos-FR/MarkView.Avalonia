// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;

using Avalonia;
using Avalonia.Platform;
using Avalonia.Styling;

namespace MarkView.Avalonia.Demo;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private static readonly Uri ShowcaseSource =
        new("avares://MarkView.Avalonia.Demo/Assets/showcase.md");

    private static readonly Uri RightToLeftSource =
        new("avares://MarkView.Avalonia.Demo/Assets/right-to-left.md");

#if BROWSER
    private static readonly Uri ReadmeSource =
        new("avares://MarkView.Avalonia.Demo/Assets/README.md");
#else
    private static Uri ReadmeSource =>
        new(Path.Combine(AppContext.BaseDirectory, "README.md"));
#endif

    private const string ExtensionsShowcaseMarkdown = """
        # Opt-In Extensions Showcase

        This page demonstrates the opt-in extensions supported by MarkView.Avalonia.
        All are activated via a combined pipeline in this demo.

        ---

        ## Footnotes

        The CommonMark spec does not define footnotes, but Markdig supports them.[^1]
        You can reference the same note multiple times.[^1]
        Or add a second footnote.[^2]

        [^1]: This is the first footnote definition. It can contain **formatted** text.
        [^2]: This is the second footnote definition.

        ---

        ## Alert Blocks

        GitHub-style alert blocks use `> [!KIND]` syntax:

        > [!NOTE]
        > The NOTE alert is used for supplementary information.

        > [!TIP]
        > The TIP alert highlights useful advice and best practices.

        > [!IMPORTANT]
        > The IMPORTANT alert highlights key information required for success.

        > [!WARNING]
        > The WARNING alert indicates potential issues that could cause problems.

        > [!CAUTION]
        > The CAUTION alert advises about risks or negative consequences.

        Obsidian-style extras: a custom title after the kind, and `-` / `+` right after it for a foldable alert that starts collapsed / expanded. Click the header (or focus it and press Enter or Space) to toggle it:

        > [!TIP] Custom titles can use **inline** `markdown`
        > The title replaces the kind name in the header.

        > [!NOTE]- Click to expand
        > This alert starts collapsed. Its content is hidden from selection and copy until expanded,
        > but finding text in it (try "hidden") expands it.

        > [!WARNING]+ Click to collapse
        > This alert starts expanded.

        > [!QUESTION]
        > Kinds other than the five GitHub ones get a neutral style.

        ---

        ## Collapsible Details

        HTML `<details>` / `<summary>` blocks fold like on GitHub (part of `UseGitHubExtensions()`, or `UseHtmlDetails()` on its own). Separate the tags from the markdown inside with blank lines:

        <details>
        <summary>Click to expand</summary>

        The body is ordinary **markdown**, hidden from selection and copy until expanded:

        ```bash
        dotnet add package MarkView.Avalonia
        ```

        <details>
        <summary>Details can nest</summary>

        An inner block, collapsed again.

        </details>

        </details>

        <details open>
        <summary>Starts expanded with <code>&lt;details open&gt;</code></summary>

        Click the summary, or focus it and press Enter or Space, to fold it. Lists work too, as long as
        a blank line separates them from the tags:

        - one blank line after `</summary>`
        - and one before `</details>`

        Without them, the list belongs to the HTML block and shows as plain text, as on GitHub.

        </details>

        ---

        ## Abbreviations

        Define abbreviations once; every occurrence in the document gets a tooltip automatically.

        HTML and CSS are the building blocks of the web. The W3C maintains their specifications.
        API stands for Application Programming Interface. JSON is a common data format.

        *[HTML]: HyperText Markup Language
        *[CSS]: Cascading Style Sheets
        *[W3C]: World Wide Web Consortium
        *[API]: Application Programming Interface
        *[JSON]: JavaScript Object Notation

        ---

        ## Citations

        Double-quoted text — `""like this""` — renders as an italicized citation span:

        Marshall McLuhan wrote ""the medium is the message"" in *Understanding Media*.

        ---

        ## Figures

        Figures wrap block content in a borderd, centred container with an optional caption:

        ^^^
        ![Avalonia Logo](avares://MarkView.Avalonia.Demo/Assets/avalonia-logo.png =80x80)

        ^^^ **Figure 1** — The Avalonia UI logo (embedded avares:// resource).

        ---

        ## Definition Lists

        Markdown
        :   A lightweight markup language with plain-text formatting syntax.

        Avalonia
        :   A cross-platform UI framework for .NET.
        :   Also the name of a fictional island, in some stories.

        ---

        ## Custom Containers

        :::warning Before you upgrade
        Custom containers wrap any blocks. The container's name (`warning` here) becomes a style
        class, and the text after it becomes the title.
        :::

        Inline containers ::look like this:: and can be styled through `Span.markdown-custom-inline`.

        ---

        ## Smart Punctuation

        "Double quotes" and 'single quotes' curl, -- becomes an en dash, --- an em dash,
        ... an ellipsis and <<these>> guillemets.

        ---

        ## Letter and Roman Numeral Lists

        a. First
        b. Second
        c. Third

        Roman numerals start a list of their own:

        i. One
        ii. Two
        iii. Three

        ---

        ## Style Classes from Attributes

        This paragraph has the `demo-highlight` class, which the demo app styles in `App.axaml`. {.demo-highlight}

        ---

        ## Hardline Breaks

        With `UseHardlineBreaks()`, every soft line break renders as an explicit line break
        instead of collapsing to a space — useful for poetry, addresses, or lyrics:

        Roses are red,
        Violets are blue,
        Every line break here
        renders exactly as typed.

        ---

        ## YouTube Thumbnail Embed

        UseMediaLinks turns image-syntax YouTube links into clickable thumbnails.
        Click the thumbnail below to open the video in your browser:

        ![Rick Astley — Never Gonna Give You Up](https://www.youtube.com/watch?v=dQw4w9WgXcQ)

        Short URLs are also supported:

        ![Big Buck Bunny trailer](https://youtu.be/aqz-KE-bpKQ)

        ^^ Footer lines, written with ^^, close a document in smaller, dimmed text.
        """;

    private const string CustomStylingMarkdown = """
        # Custom Styling

        This page is rendered by the same `MarkdownViewer` as the other views. The window applies a
        style block scoped to `MarkdownViewer.custom-styled` (see `App.axaml`) that overrides a few
        of the library's `markdown-*` classes for this page only, without touching the global theme.

        ## Heading Two

        Every `markdown-h1` / `markdown-h2` element still carries its usual class; only the setter
        values differ here.

        > Blockquotes (`markdown-blockquote`) pick up a different accent colour and background on
        > this page.

        ```text
        Code blocks (markdown-code-block) get a different background here too.
        ```

        Switch back to **Feature Showcase** to compare against the default look for the same elements.
        """;

    private readonly List<(int SelectedIndex, Uri? Source, string? Markdown)> _history = [];
    private int _historyIndex = -1;
    private bool _navigating;
    // Whether the page is empty only because a replay emptied it; any other assignment to the page ends that.
    private bool _isReplaying;

    private Uri? _source;
    private Uri? _baseUri;
    private string? _markdown;
    private int _selectedIndex;
    private bool _isLightTheme;
    private bool _isCustomStyled;
    private bool _isColorExtensionSelected;
    private bool _healIncompleteSyntax = true;
    private int _replayChunkSize = 40;
    private int _replayInterval = 200;
    private bool _isCodeHeightLimited;
    private bool _isFirstLinesOnly;
    private bool _isFirstLinesExpanded;
    private int _themePresetIndex;
    private PageView _pageView;
    // The presets of the drop-down after the default theme (index 0), each created when first picked
    private readonly Styles?[] _themePresets = new Styles?[4];

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (SetField(ref _selectedIndex, value))
                LoadContent();
        }
    }

    public bool IsLightTheme
    {
        get => _isLightTheme;
        set
        {
            if (!SetField(ref _isLightTheme, value)) return;
            Application.Current!.RequestedThemeVariant = value ? ThemeVariant.Light : ThemeVariant.Dark;
        }
    }

    /// <summary>The theme preset the documents use: 0 for the default theme, 1 for GitHub, 2 for Chat, 3 for Docs.</summary>
    public int ThemePresetIndex
    {
        get => _themePresetIndex;
        set
        {
            var previous = _themePresetIndex;
            if (!SetField(ref _themePresetIndex, value)) return;
            var styles = Application.Current!.Styles;
            if (_themePresets[previous] is { } shown)
                styles.Remove(shown);
            if (value > 0)
            {
                // Right after MarkdownTheme.axaml, which it builds on, so the app's own styles below still win.
                var preset = _themePresets[value] ??= value switch
                {
                    1 => new GitHubPreset(),
                    2 => new ChatPreset(),
                    _ => new DocsPreset(),
                };
                // App.axaml's StyleInclude is compiled into the theme's own Styles, so it is found by one of its resources.
                var markdownTheme = styles.First(s => s.TryGetResource("MarkdownCodeBlockBackground", ThemeVariant.Dark, out _));
                styles.Insert(styles.IndexOf(markdownTheme) + 1, preset);
            }
        }
    }

    /// <summary>Caps code blocks at <see cref="LimitedCodeBlockHeight"/>, past which they scroll.</summary>
    public bool IsCodeHeightLimited
    {
        get => _isCodeHeightLimited;
        set
        {
            if (SetField(ref _isCodeHeightLimited, value))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CodeBlockMaxHeight)));
        }
    }

    private const double LimitedCodeBlockHeight = 160;

    /// <summary>Shows only the first <see cref="FirstLineCount"/> lines of each page, with a Show more button.</summary>
    public bool IsFirstLinesOnly
    {
        get => _isFirstLinesOnly;
        set
        {
            if (!SetField(ref _isFirstLinesOnly, value)) return;
            IsFirstLinesExpanded = false;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FirstLinesMaxLines)));
        }
    }

    private const int FirstLineCount = 10;

    public int FirstLinesMaxLines => _isFirstLinesOnly ? FirstLineCount : 0;

    /// <summary>Whether the whole page is shown despite <see cref="IsFirstLinesOnly"/>; each new page starts cut again.</summary>
    public bool IsFirstLinesExpanded
    {
        get => _isFirstLinesExpanded;
        set => SetField(ref _isFirstLinesExpanded, value);
    }

    /// <summary>
    /// Whether a replay closes the syntax left open at the end of the streamed text between updates
    /// (<c>BeginStreaming(healIncompleteSyntax:)</c>). A change takes effect at once in a replay.
    /// </summary>
    public bool HealIncompleteSyntax
    {
        get => _healIncompleteSyntax;
        set => SetField(ref _healIncompleteSyntax, value);
    }

    /// <summary>How many characters each step of a replay appends: <see cref="MinReplayChunkSize"/> to <see cref="MaxReplayChunkSize"/>.</summary>
    public int ReplayChunkSize
    {
        get => _replayChunkSize;
        set
        {
            if (SetField(ref _replayChunkSize, System.Math.Clamp(value, MinReplayChunkSize, MaxReplayChunkSize)))
                NotifyReplayLabels();
        }
    }

    /// <summary>The pause between two steps of a replay, in milliseconds: <see cref="MinReplayInterval"/> to <see cref="MaxReplayInterval"/>.</summary>
    public int ReplayInterval
    {
        get => _replayInterval;
        set
        {
            if (SetField(ref _replayInterval, System.Math.Clamp(value, MinReplayInterval, MaxReplayInterval)))
                NotifyReplayLabels();
        }
    }

    public const int MinReplayChunkSize = 8;
    public const int MaxReplayChunkSize = 64;
    public const int MinReplayInterval = 16;
    public const int MaxReplayInterval = 256;

    public string ReplayChunkSizeLabel => $"Chunk size: {_replayChunkSize} characters";

    public string ReplayIntervalLabel =>
        $"Interval: {_replayInterval} ms ({_replayChunkSize * 1000.0 / _replayInterval:N0} characters per second)";

    private void NotifyReplayLabels()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReplayChunkSizeLabel)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReplayIntervalLabel)));
    }

    public double CodeBlockMaxHeight => _isCodeHeightLimited ? LimitedCodeBlockHeight : double.PositiveInfinity;

    /// <summary>Whether the page area shows the rendered page, its markdown, or both side by side.</summary>
    public PageView PageView
    {
        get => _pageView;
        set
        {
            if (!SetField(ref _pageView, value)) return;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPreviewShown)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSourceShown)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPreviewSelected)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSourceSelected)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSideBySideSelected)));
        }
    }

    public bool IsPreviewShown => _pageView != PageView.Source;

    public bool IsSourceShown => _pageView != PageView.Preview;

    // One per button of the switch; a radio button only ever sets its own to true.
    public bool IsPreviewSelected
    {
        get => _pageView == PageView.Preview;
        set { if (value) PageView = PageView.Preview; }
    }

    public bool IsSourceSelected
    {
        get => _pageView == PageView.Source;
        set { if (value) PageView = PageView.Source; }
    }

    public bool IsSideBySideSelected
    {
        get => _pageView == PageView.SideBySide;
        set { if (value) PageView = PageView.SideBySide; }
    }

    public bool IsCustomStyled
    {
        get => _isCustomStyled;
        private set => SetField(ref _isCustomStyled, value);
    }

    public bool IsColorExtensionSelected
    {
        get => _isColorExtensionSelected;
        private set => SetField(ref _isColorExtensionSelected, value);
    }

    public Uri? Source
    {
        get => _source;
        private set
        {
            _isReplaying = false;
            // A replay's base belongs to the page it replayed; the viewer infers the new page's from its Source.
            BaseUri = null;
            IsFirstLinesExpanded = false;
            SetField(ref _source, value);
        }
    }

    /// <summary>
    /// The folder relative links and images resolve against while a page is replayed. The viewer infers
    /// it from <see cref="Source"/> otherwise, and a replay unbinds the page from <see cref="Source"/>.
    /// </summary>
    public Uri? BaseUri
    {
        get => _baseUri;
        private set => SetField(ref _baseUri, value);
    }

    public string? Markdown
    {
        get => _markdown;
        private set
        {
            _isReplaying = false;
            SetField(ref _markdown, value);
        }
    }

    /// <summary>Whether a replay has emptied the page to stream it back in; see <see cref="BeginReplay"/>.</summary>
    public bool IsReplaying => _isReplaying;

    public bool CanGoBack => _historyIndex > 0;
    public bool CanGoForward => _historyIndex < _history.Count - 1;

    public void GoBack()
    {
        if (!CanGoBack) return;
        _historyIndex--;
        RestoreEntry(_history[_historyIndex]);
    }

    public void GoForward()
    {
        if (!CanGoForward) return;
        _historyIndex++;
        RestoreEntry(_history[_historyIndex]);
    }

    private void RestoreEntry((int SelectedIndex, Uri? Source, string? Markdown) entry)
    {
        _navigating = true;
        _selectedIndex = entry.SelectedIndex;
        Source = entry.Source;
        Markdown = entry.Markdown;
        IsCustomStyled = entry.SelectedIndex == 2;
        IsColorExtensionSelected = entry.SelectedIndex == 3;
        _navigating = false;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedIndex)));
        NotifyNavigation();
    }

    private void PushEntry(Uri? source, string? markdown)
    {
        if (_navigating) return;
        // Discard any forward entries
        if (_historyIndex < _history.Count - 1)
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        _history.Add((_selectedIndex, source, markdown));
        _historyIndex = _history.Count - 1;
        NotifyNavigation();
    }

    private void NotifyNavigation()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanGoBack)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanGoForward)));
    }

    public MainViewModel()
    {
        LoadContent();
    }

    private void LoadContent()
    {
        IsCustomStyled = _selectedIndex == 2;
        IsColorExtensionSelected = _selectedIndex == 3;

        switch (_selectedIndex)
        {
            case 0:
                Source = ShowcaseSource;
                Markdown = null;
                break;
            case 1:
                Source = null;
                Markdown = ExtensionsShowcaseMarkdown;
                break;
            case 2:
                Source = null;
                Markdown = CustomStylingMarkdown;
                break;
            case 3:
                // Content is supplied by the dedicated ColorExtensionView control, not this Markdown/Source pair.
                Source = null;
                Markdown = null;
                break;
            case 4:
                Source = RightToLeftSource;
                Markdown = null;
                break;
            default:
                Source = ReadmeSource;
                Markdown = null;
                break;
        }
        PushEntry(Source, Markdown);
    }

    public void LoadFile(string filePath) => LoadFile(new Uri(Path.GetFullPath(filePath)));

    // Accepts a full Uri (rather than a bare path) so a "#fragment" from a clicked link
    // survives into Source — MarkdownViewer scrolls to it automatically once rendered.
    public void LoadFile(Uri fileUri)
    {
        Source = fileUri;
        Markdown = null;
        PushEntry(Source, null);
    }

    public void LoadMarkdownContent(string markdown)
    {
        Source = null;
        Markdown = markdown;
        PushEntry(null, markdown);
    }

    /// <summary>
    /// Reads the markdown the current page shows: <see cref="Markdown"/> when it is set, otherwise the
    /// text behind <see cref="Source"/> (an <c>avares:</c> resource or a file, without any fragment).
    /// </summary>
    /// <returns>
    /// The markdown, or <c>null</c> when the page has neither (the Color Extension page, whose content
    /// is not markdown from this view model) or when its file can no longer be read (moved or locked).
    /// </returns>
    public async Task<string?> ReadCurrentMarkdownAsync()
    {
        if (Markdown is not null)
            return Markdown;

        switch (Source)
        {
            case { Scheme: "avares" } resource:
                return await ReadAssetAsync(resource);
            case { IsFile: true } file:
                try
                {
                    return await File.ReadAllTextAsync(file.LocalPath);
                }
                catch (IOException)
                {
                    // A moved or locked file; the viewer swallows the same failure when it loads the page.
                    return null;
                }
            default:
                return null;
        }
    }

    /// <summary>
    /// Reads the markdown behind the page shown, for the source view: <see cref="ReadCurrentMarkdownAsync"/>,
    /// or the tutorial the Color Extension page renders with its own viewer.
    /// </summary>
    public Task<string?> ReadPageMarkdownAsync() =>
        IsColorExtensionSelected ? ReadAssetAsync(ColorExtension.ColorExtensionView.TutorialSource) : ReadCurrentMarkdownAsync();

    private static async Task<string?> ReadAssetAsync(Uri resource)
    {
        await using var stream = AssetLoader.Open(new Uri(resource.GetLeftPart(UriPartial.Path)));
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Empties the page so the viewer can stream its text back in. <c>BeginStreaming</c> refuses a
    /// viewer whose <c>Source</c> is set, so the page is unbound from <see cref="Source"/> first and
    /// the viewer cleared; the page's folder stays its <see cref="BaseUri"/>, so the relative links and
    /// images of the streamed text still resolve. A page replayed before is already unbound, so it
    /// keeps the base its first replay set. The history is left alone: the entry pushed when the page
    /// loaded is still what Back and Forward restore.
    /// </summary>
    public void BeginReplay()
    {
        // BaseUri is only set while the page shown is one a replay unbound: changing the page resets it.
        var baseUri = FolderOf(Source) ?? BaseUri;
        Source = null;
        BaseUri = baseUri;
        Markdown = "";
        _isReplaying = true;
    }

    /// <summary>The folder of a document, with the trailing slash, or <c>null</c> without a document.</summary>
    private static Uri? FolderOf(Uri? document)
    {
        if (document is null) return null;
        var absolute = document.AbsoluteUri;
        var lastSlash = absolute.LastIndexOf('/');
        return lastSlash > 0 ? new Uri(absolute[..(lastSlash + 1)]) : null;
    }

    /// <summary>
    /// Restores the page's text once the replay ends so the view model and the viewer agree again. After
    /// a completed replay the viewer normally already shows that text, so nothing changes; after an
    /// interrupted one (a second click, a view option toggled, a task toggled mid-replay) the restored
    /// text is rendered once. Does nothing when the page has changed since <see cref="BeginReplay"/>
    /// (another tab, a file, Back or Forward), so a replay never overwrites the page that interrupted it.
    /// </summary>
    /// <param name="text">The text the replay streamed.</param>
    public void EndReplay(string text)
    {
        if (!_isReplaying)
            return;
        Markdown = text;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

/// <summary>What the page area of the demo shows.</summary>
public enum PageView
{
    /// <summary>The rendered page.</summary>
    Preview,

    /// <summary>The markdown the page is rendered from.</summary>
    Source,

    /// <summary>The markdown on the left of the rendered page.</summary>
    SideBySide,
}
