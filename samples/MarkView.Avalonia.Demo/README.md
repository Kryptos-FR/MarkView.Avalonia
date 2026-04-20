# MarkView.Avalonia Demo

An interactive showcase application for the [MarkView.Avalonia](../../README.md) library. The demo renders markdown content using every feature available in the library and its extension packages, and serves as a reference for how to configure the library in a real Avalonia application.

## Running the Demo

```bash
cd samples/MarkView.Avalonia.Demo
dotnet run
```

## What the Demo Shows

The demo window contains a top toolbar and a full-screen `MarkdownViewer`. Use the **View** dropdown to switch between three built-in documents; use **Open file…** to load any local `.md` file.

### Built-in Views

| View | Content |
|------|---------|
| **Feature Showcase** | Core markdown: headings, text formatting, EmphasisExtras, blockquotes, task lists, tables, code blocks (multi-language), bitmap image, SVG image, Mermaid diagram |
| **Extensions Showcase** | Opt-in extensions: footnotes, GitHub alert blocks, abbreviations with tooltips, figures with captions, YouTube thumbnail embeds |
| **README** | The project's own `README.md` loaded from disk (relative to the solution root) |

### Navigation and History

- **← / →** buttons navigate backward and forward through the history stack (loaded files and in-app anchor jumps).
- **Open file…** opens a file picker to load any local Markdown file. Once loaded, in-document relative links (`[text](./other.md)`) open further files in the same viewer.
- **In-document anchor links** (e.g. the Table of Contents) scroll immediately to the target heading via `ScrollToAnchor()`.

### Theme Toggle

The **Light theme** toggle button switches the entire application between Avalonia's `Light` and `Dark` theme variants. Syntax-highlighted code blocks and Mermaid diagrams update in-place — no document rebuild or scroll reset.

## How the Demo Is Configured

All configuration is done once in `App.axaml.cs` and applies globally to every `MarkdownViewer` instance:

```csharp
// Global pipeline — includes all opt-in extensions for the full showcase
MarkdownViewerDefaults.Pipeline = new MarkdownPipelineBuilder()
    .UseSupportedExtensions()
    .UseFootnotes()
    .UseAlertBlocks()
    .UseAbbreviations()
    .UseFigures()
    .UseMediaLinks()
    .Build();

// Global rendering extensions
MarkdownViewerDefaults.Extensions.AddTextMateHighlighting();
MarkdownViewerDefaults.Extensions.AddSvg();
MarkdownViewerDefaults.Extensions.AddMermaid();

// Global link handler
MarkdownViewer.LinkClickedEvent.AddClassHandler<MarkdownViewer>(OnLinkClicked);
```

The `OnLinkClicked` handler intercepts absolute `file://` links to `.md` / `.markdown` files and opens them in the viewer instead of the system browser. All other URLs are opened with `Process.Start`.

## Project Structure

```
MarkView.Avalonia.Demo/
├── App.axaml            — Application XAML (styles, resources)
├── App.axaml.cs         — Startup: global pipeline, extensions, link handler
├── MainWindow.axaml     — Single-window layout (toolbar + MarkdownViewer)
├── MainWindow.axaml.cs  — Back/forward buttons, file picker
├── MainViewModel.cs     — MVVM view model: history stack, built-in content, LoadFile()
└── Assets/
    └── avalonia-logo.png
```
