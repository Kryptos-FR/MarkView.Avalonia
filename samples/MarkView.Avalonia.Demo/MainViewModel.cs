// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

using Avalonia;
using Avalonia.Styling;

namespace MarkView.Avalonia.Demo;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private static readonly string ReadmeMarkdown = LoadReadme();

    private const string ShowcaseMarkdown = """
        # MarkView.Avalonia Feature Showcase

        > Welcome to the **MarkView.Avalonia** feature showcase. This document demonstrates every
        > rendering capability supported by the viewer and its extension packages.

        ## Table of Contents

        - [Headings](#headings)
        - [Text Formatting](#text-formatting)
        - [Emphasis Extras](#emphasis-extras)
        - [Blockquotes](#blockquotes)
        - [Task List](#task-list)
        - [Tables](#tables)
        - [Code Blocks](#code-blocks)
        - [Bitmap Image](#bitmap-image)
        - [SVG Image](#svg-image)
        - [Mermaid Diagram](#mermaid-diagram)

        ---

        ## Headings

        # Heading 1
        ## Heading 2
        ### Heading 3
        #### Heading 4
        ##### Heading 5
        ###### Heading 6

        ---

        ## Text Formatting

        Regular paragraph with **bold text**, *italic text*, ~~strikethrough~~, and `inline code`.

        You can also combine them: ***bold and italic***, **`bold code`**, *~~italic strikethrough~~*.

        ---

        ## Emphasis Extras

        The `EmphasisExtras` Markdig extension unlocks four additional inline styles:

        | Syntax | Result | Description |
        |--------|--------|-----------|
        | `~text~` | H~2~O | Subscript |
        | `^text^` | x^2^ + y^2^ = r^2^ | Superscript |
        | `++text++` | ++inserted++ | Underline (inserted) |
        | `==text==` | ==marked== | Highlighted (marked) |

        ---

        ## Blockquotes

        > This is a top-level blockquote. It can contain *formatted* text and **multiple** lines.
        >
        > > This is a nested blockquote inside the first one.
        > > Nested content can also span multiple lines.
        >
        > Back to the outer blockquote.

        ---

        ## Task List

        - [x] Core markdown rendering (headings, paragraphs, lists)
        - [x] Syntax-highlighted code blocks via `MarkView.Avalonia.SyntaxHighlighting`
        - [x] SVG image rendering via `MarkView.Avalonia.Svg`
        - [x] Mermaid diagram rendering via `MarkView.Avalonia.Mermaid`
        - [x] Tables, blockquotes, task lists
        - [ ] PDF export (planned)

        ---

        ## Tables

        | Extension Package | Feature | NuGet Status | Notes |
        |---|---|---|---|
        | `MarkView.Avalonia` | Core rendering | ✅ Published | Markdig-based |
        | `MarkView.Avalonia.SyntaxHighlighting` | Code highlighting | ✅ Published | TextMate grammars |
        | `MarkView.Avalonia.Svg` | SVG images | ✅ Published | Avalonia.Svg |
        | `MarkView.Avalonia.Mermaid` | Mermaid diagrams | ✅ Published | Pure .NET |

        ---

        ## Code Blocks

        ### C#

        ```csharp
        using MarkView.Avalonia;

        var viewer = new MarkdownViewer();
        viewer.UseTextMateHighlighting()
              .UseSvg()
              .UseMermaid();

        viewer.Markdown = "# Hello, **World**!";
        ```

        ### JSON

        ```json
        {
          "name": "MarkView.Avalonia",
          "version": "1.0.0",
          "extensions": [
            "SyntaxHighlighting",
            "Svg",
            "Mermaid"
          ],
          "targetFramework": "net8.0"
        }
        ```

        ### Python

        ```python
        import base64

        svg = '<svg xmlns="http://www.w3.org/2000/svg" width="200" height="120"></svg>'
        encoded = base64.b64encode(svg.encode()).decode()
        data_uri = f"data:image/svg+xml;base64,{encoded}"
        print(data_uri)
        ```

        ---

        ## Bitmap Image

        The image below is an Avalonia resource embedded in the demo app (`avares://` URI):

        ![Avalonia Logo](avares://MarkView.Avalonia.Demo/Assets/avalonia-logo.png =80x80)

        ---

        ## SVG Image

        The image below is rendered from an inline `data:image/svg+xml;base64` URI using the
        `MarkView.Avalonia.Svg` extension:

        ![Colorful shapes on dark background](data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSIyMDAiIGhlaWdodD0iMTIwIj4KICA8cmVjdCB3aWR0aD0iMjAwIiBoZWlnaHQ9IjEyMCIgZmlsbD0iIzFlMWUyZSIvPgogIDxjaXJjbGUgY3g9IjQwIiBjeT0iNjAiIHI9IjI4IiBmaWxsPSIjODliNGZhIi8+CiAgPHJlY3QgeD0iODAiIHk9IjMyIiB3aWR0aD0iNTAiIGhlaWdodD0iNTAiIGZpbGw9IiNhNmUzYTEiLz4KICA8cG9seWdvbiBwb2ludHM9IjE2MCwzMiAxNDAsOTIgMTgwLDkyIiBmaWxsPSIjZmFiMzg3Ii8+Cjwvc3ZnPg==)

        ---

        ## Mermaid Diagram

        The diagram below is rendered live by the `MarkView.Avalonia.Mermaid` extension:

        ```mermaid
        flowchart LR
            MD[Markdown Text] --> MV[MarkView.Avalonia]
            MV --> Core[Core Renderer]
            MV --> SH[SyntaxHighlighting\nextension]
            MV --> SVG[Svg\nextension]
            MV --> MM[Mermaid\nextension]
            Core --> Out[Avalonia UI]
            SH --> Out
            SVG --> Out
            MM --> Out
        ```

        ---

        *End of showcase.*
        """;

    private const string ExtensionsShowcaseMarkdown = """
        # Opt-In Extensions Showcase

        This page demonstrates the five opt-in extensions added to MarkView.Avalonia.
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

        ## Figures

        Figures wrap block content in a borderd, centred container with an optional caption:

        ^^^
        ![Avalonia Logo](avares://MarkView.Avalonia.Demo/Assets/avalonia-logo.png =80x80)

        ^^^ **Figure 1** — The Avalonia UI logo (embedded avares:// resource).

        ---

        ## YouTube Thumbnail Embed

        UseMediaLinks turns image-syntax YouTube links into clickable thumbnails.
        Click the thumbnail below to open the video in your browser:

        ![Rick Astley — Never Gonna Give You Up](https://www.youtube.com/watch?v=dQw4w9WgXcQ)

        Short URLs are also supported:

        ![Big Buck Bunny trailer](https://youtu.be/aqz-KE-bpKQ)
        """;

    private readonly List<(string? Markdown, Uri? BaseUri)> _history = [];
    private int _historyIndex = -1;
    private bool _navigating;

    private string? _markdown;
    private Uri? _baseUri;
    private int _selectedIndex;
    private bool _isLightTheme;

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

    public string? Markdown
    {
        get => _markdown;
        private set => SetField(ref _markdown, value);
    }

    public Uri? BaseUri
    {
        get => _baseUri;
        private set => SetField(ref _baseUri, value);
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

    private void RestoreEntry((string? Markdown, Uri? BaseUri) entry)
    {
        _navigating = true;
        BaseUri = entry.BaseUri;
        Markdown = entry.Markdown;
        _navigating = false;
        NotifyNavigation();
    }

    private void PushEntry(string? markdown, Uri? baseUri)
    {
        if (_navigating) return;
        // Discard any forward entries
        if (_historyIndex < _history.Count - 1)
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        _history.Add((markdown, baseUri));
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
        BaseUri = null;
        Markdown = _selectedIndex switch
        {
            0 => ShowcaseMarkdown,
            1 => ExtensionsShowcaseMarkdown,
            _ => ReadmeMarkdown,
        };
        PushEntry(Markdown, null);
    }

    public void LoadFile(string filePath)
    {
        var dir = Path.GetFullPath(Path.GetDirectoryName(filePath)!);
        BaseUri = new Uri(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar);
        Markdown = File.ReadAllText(filePath);
        PushEntry(Markdown, BaseUri);
    }

    private static string LoadReadme()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("README.md");
        if (stream is null)
            return "# README not found";
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
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
