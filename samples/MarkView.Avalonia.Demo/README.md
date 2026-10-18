# MarkView.Avalonia Demo

An interactive showcase application for the [MarkView.Avalonia](../../README.md) library. The demo renders markdown content using every feature available in the library and its extension packages, and serves as a reference for how to configure the library in a real Avalonia application.

## Running the Demo

```bash
cd samples/MarkView.Avalonia.Demo.Desktop
dotnet run
```

## What the Demo Shows

The demo window contains a page tab strip, a toolbar, and a full-screen content area. Use the **tabs** to switch between five built-in pages; use **Open file…** to load any local `.md` file.

The toolbar has three groups: the outline, history and **Open…** buttons on the left; the find box in the middle (Ctrl+F, with the match count and the previous and next buttons inside it); and the appearance controls on the right: the light and dark switch, the theme preset and the **View options** menu.

### Built-in Views

| View | Content |
|------|---------|
| **Feature Showcase** | Core markdown: headings, text formatting (strikethrough, subscript, superscript, underline, highlight), blockquotes, task lists, tables, code blocks (multi-language), bitmap image, SVG image, Mermaid diagram |
| **Extensions Showcase** | Opt-in extensions: footnotes, GitHub alert blocks, abbreviations with tooltips, figures with captions, YouTube thumbnail embeds |
| **Custom Styling** | Same shared `MarkdownViewer` as the pages above, with a `custom-styled` class toggled on it; `App.axaml` scopes a handful of `markdown-*` class overrides to that class, showing that theming can be done per-instance, not only globally |
| **Color Extension** | A dedicated `ColorExtensionView` control hosting its own `MarkdownViewer`, configured with its own `Pipeline`/`Extensions` instead of `MarkdownViewerDefaults`. Demonstrates writing a complete custom Markdig + MarkView extension (`%[color:red]text%` colour spans) — see `ColorExtension/` and [docs/custom-extensions.md](../../docs/custom-extensions.md) |
| **README** | The project's own `README.md` loaded from disk (relative to the solution root) |

### Navigation and History

- **← / →** buttons navigate backward and forward through the history stack (loaded files and in-app anchor jumps).
- **Open file…** opens a file picker to load any local Markdown file. Once loaded, in-document relative links (`[text](./other.md)`) open further files in the same viewer.
- **In-document anchor links** (e.g. the Table of Contents) scroll immediately to the target heading via `ScrollToAnchor()`.

### Theme Toggle

The **sun / moon** button switches the entire application between Avalonia's `Light` and `Dark` theme variants; its icon shows the current one. Syntax-highlighted code blocks and Mermaid diagrams update in-place — no document rebuild or scroll reset.

The **theme preset** drop-down switches the documents between the default theme and the [GitHub theme preset](../../docs/theming.md#theme-presets) (`GitHubTheme.axaml`), by inserting or removing its `StyleInclude` right after `MarkdownTheme.axaml` in the application's styles (see `MainViewModel.ThemePresetIndex`). Both presets follow the Light/Dark switch.

### View Options

The **View options** menu (the sliders icon) toggles the viewer settings that change how the document is drawn:

- **Clickable tasks**: `AreTaskListsInteractive`, task-list items as check boxes you can toggle.
- **Code line numbers**: `ShowCodeBlockLineNumbers`.
- **Wrap long code lines**: `WrapCodeBlockLines`.
- **Short code blocks**: `CodeBlockMaxHeight` at 160 pixels, so taller blocks scroll inside themselves.
- **Stream the document**: replays the current page through `BeginStreaming()` with `AutoScrollToEnd` on, so you can watch healing and provisional rendering work. Changing the page, opening a file or changing one of the view options above stops the replay (the code block options re-render the document, which ends the session). The viewer's `BaseUri` is set to the page's folder for the replay, since the page is unbound from `Source` to stream, so relative links and images keep resolving.
- **Heal incomplete syntax**: `BeginStreaming(healIncompleteSyntax:)`, on by default. Off, the text renders as Markdig parses it at each update, so an unmatched `` ` `` or `**` shows until its closer arrives. A change takes effect at once in a replay: the session is completed and a new one continues from the text streamed so far.
- **Chunk size** and **Interval**: two sliders, 8 to 64 characters appended every 16 to 256 ms (40 every 20 ms by default). A small chunk every 200 ms or so is the pace of a language model, where the difference healing makes is easy to see. Both take effect at once in a replay.

## How the Demo Is Configured

Most configuration is done once in `App.axaml.cs` and applies globally to every `MarkdownViewer` instance. The **Color Extension** page is the exception: its dedicated control sets `Pipeline` and `Extensions` on its own `MarkdownViewer` instance instead, bypassing these app-wide defaults for parsing (see [docs/configuration.md](../../docs/configuration.md#markdig-pipeline) for the precedence rules, and [docs/custom-extensions.md](../../docs/custom-extensions.md) for the extension itself).

```csharp
// Global pipeline — includes all opt-in extensions for the full showcase
MarkdownViewerDefaults.Pipeline = new MarkdownPipelineBuilder()
    .UseSupportedExtensions()
    .UseAbbreviations()
    .UseAlertBlocks()
    .UseCitations()
    .UseFigures()
    .UseFootnotes()
    .UseHardlineBreaks()
    .UseMediaLinks()
    .Build();

// Global rendering extensions
MarkdownViewerDefaults.Extensions.AddTextMateHighlighting();
MarkdownViewerDefaults.Extensions.AddSvg();
MarkdownViewerDefaults.Extensions.AddMermaid();

// Global link handler
MarkdownViewer.LinkClickedEvent.AddClassHandler<MarkdownViewer>(OnLinkClicked);
```

The `OnLinkClicked` handler intercepts absolute `file://` links to `.md` / `.markdown` files and opens them in the viewer instead of the system browser. All other URLs are left unhandled, so `MarkdownViewer` opens them itself via the platform launcher.

## Project Structure

```
MarkView.Avalonia.Demo/
├── App.axaml               — Application XAML (styles, resources, Custom Styling overrides)
├── App.axaml.cs            — Startup: global pipeline, extensions, link handler
├── MainWindow.axaml        — Window layout: page tabs, toolbar, MarkdownViewer/ColorExtensionView
├── MainWindow.axaml.cs     — Back/forward buttons, file picker, outline popup
├── MainViewModel.cs        — MVVM view model: history stack, built-in content, LoadFile()
├── Converters/
│   └── OutlineFilterConverter.cs — Flattens TableOfContents for the outline popup
├── ColorExtension/
│   ├── ColorSpanInline.cs        — Markdig leaf inline node
│   ├── ColorSpanParser.cs        — Markdig inline parser
│   ├── ColorMarkdownExtension.cs — Markdig IMarkdownExtension + UseColorSpans()
│   ├── ColorSpanRenderer.cs      — MarkView.Avalonia renderer
│   ├── ColorSpanExtension.cs     — MarkView.Avalonia IMarkViewExtension
│   └── ColorExtensionView.axaml(.cs) — Dedicated control with its own Pipeline/Extensions
└── Assets/
    ├── avalonia-logo.png
    ├── showcase.md
    └── color-extension.md        — Tutorial content for the Color Extension page
```
