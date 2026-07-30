// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;

using Avalonia;
using Avalonia.Styling;

namespace MarkView.Avalonia.Demo;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private static readonly Uri ShowcaseSource =
        new("avares://MarkView.Avalonia.Demo/Assets/showcase.md");

    private static Uri ReadmeSource =>
        new(Path.Combine(AppContext.BaseDirectory, "README.md"));

    private const string ExtensionsShowcaseMarkdown = """
        # Opt-In Extensions Showcase

        This page demonstrates the seven opt-in extensions added to MarkView.Avalonia.
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
        """;

    private readonly List<(Uri? Source, string? Markdown)> _history = [];
    private int _historyIndex = -1;
    private bool _navigating;

    private Uri? _source;
    private string? _markdown;
    private int _selectedIndex;
    private bool _isLightTheme;
    private string _urlText = string.Empty;

    public string[] Views { get; } = ["Feature Showcase", "Extensions Showcase", "README"];

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

    public string UrlText
    {
        get => _urlText;
        set => SetField(ref _urlText, value);
    }

    public Uri? Source
    {
        get => _source;
        private set => SetField(ref _source, value);
    }

    public string? Markdown
    {
        get => _markdown;
        private set => SetField(ref _markdown, value);
    }

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

    private void RestoreEntry((Uri? Source, string? Markdown) entry)
    {
        _navigating = true;
        Source = entry.Source;
        Markdown = entry.Markdown;
        _navigating = false;
        NotifyNavigation();
    }

    private void PushEntry(Uri? source, string? markdown)
    {
        if (_navigating) return;
        // Discard any forward entries
        if (_historyIndex < _history.Count - 1)
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        _history.Add((source, markdown));
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
            default:
                Source = ReadmeSource;
                Markdown = null;
                break;
        }
        PushEntry(Source, Markdown);
    }

    public void LoadFile(string filePath)
    {
        Source = new Uri(Path.GetFullPath(filePath));
        Markdown = null;
        PushEntry(Source, null);
    }

    public void LoadFromUrl()
    {
        var text = UrlText.Trim();
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return;
        Source = uri;
        Markdown = null;
        PushEntry(Source, null);
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
