# Configuration

## MarkdownViewer properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Markdown` | `string?` | `null` | The markdown text to render. Setting this triggers a full re-render. |
| `Pipeline` | `MarkdownPipeline?` | `null` | The Markdig pipeline. `null` falls back to `MarkdownViewerDefaults.Pipeline`, or the built-in default (`UseSupportedExtensions`). |
| `BaseUri` | `Uri?` | `null` | Base URI for resolving relative links and image paths. |
| `Extensions` | `IList<IMarkViewExtension>` | `[]` | Per-instance rendering extensions. Applied after global defaults. |

## Markdig Pipeline

The pipeline controls which Markdig extensions parse the input markdown. Build one with `MarkdownPipelineBuilder`:

```csharp
viewer.Pipeline = new MarkdownPipelineBuilder()
    .UseSupportedExtensions()   // bold, italic, strikethrough, subscript, superscript,
                                // underline, highlight, task lists, tables, autolinks
    .UseFootnotes()             // [^1] footnotes
    .UseAlertBlocks()           // > [!NOTE] / > [!WARNING] etc.
    .UseAbbreviations()         // *[HTML]: HyperText Markup Language
    .UseFigures()               // ^^^ figure blocks
    .UseMediaLinks()            // YouTube thumbnail embeds
    .Build();
```

`UseSupportedExtensions()` is a MarkView.Avalonia helper that enables the subset of Markdig extensions that have native renderers in the library:

- `EmphasisExtras` — strikethrough `~~`, subscript `~`, superscript `^`, underline `++`, highlight `==`
- `AutoLinks` — bare URL auto-linking
- `GridTables` — RST-style grid tables
- `PipeTables` — GFM pipe tables
- `TaskLists` — `- [x]` checkboxes

The five opt-in extensions above (`UseFootnotes()` etc.) are thin wrappers around the corresponding Markdig extension; they are defined in `MarkdownExtensions.cs` and re-exported as extension methods on `MarkdownPipelineBuilder`.

### Convenience extension methods

For simple use cases, call a single method instead of building the pipeline manually:

```csharp
viewer.UseFootnotes();     // pipeline + footnote rendering
viewer.UseAlertBlocks();   // pipeline + alert block rendering
viewer.UseAbbreviations(); // pipeline + abbreviation tooltips
viewer.UseFigures();       // pipeline + figure blocks
viewer.UseMediaLinks();    // pipeline + YouTube thumbnails
```

Each method calls `UseSupportedExtensions()` plus the requested feature. To combine several opt-in features, build the pipeline explicitly.

## Application-wide defaults (`MarkdownViewerDefaults`)

Set the pipeline and extensions once at application startup and they apply to **every** `MarkdownViewer` in the app:

```csharp
// App.axaml.cs  OnFrameworkInitializationCompleted()
MarkdownViewerDefaults.Pipeline = new MarkdownPipelineBuilder()
    .UseSupportedExtensions()
    .UseAlertBlocks()
    .Build();

MarkdownViewerDefaults.Extensions.AddTextMateHighlighting();
MarkdownViewerDefaults.Extensions.AddSvg();
MarkdownViewerDefaults.Extensions.AddMermaid();
```

**Priority rules:**

1. `viewer.Pipeline` is used if set; otherwise `MarkdownViewerDefaults.Pipeline`; otherwise the built-in default.
2. `MarkdownViewerDefaults.Extensions` are registered first, then `viewer.Extensions`.
3. If the same extension object appears in both lists it is registered only once (reference equality check).

## BaseUri

`BaseUri` is used to resolve relative links in markdown:

```csharp
// Load markdown from a GitHub URL — relative image paths resolve against this base
viewer.BaseUri = new Uri("https://raw.githubusercontent.com/org/repo/main/docs/");
viewer.Markdown = File.ReadAllText("README.md");
```

`BaseUri` is also passed to `IImageLoader.LoadAsync` for each image URL, allowing image loaders to convert relative paths to absolute ones.

## LinkClicked event

`LinkClicked` is an Avalonia **routed event** that bubbles up the visual tree. Subscribe on an individual viewer:

```csharp
viewer.LinkClicked += (_, e) =>
    Process.Start(new ProcessStartInfo(e.Url) { UseShellExecute = true });
```

Or register a class-level handler once at startup to catch all viewers:

```csharp
MarkdownViewer.LinkClickedEvent.AddClassHandler<MarkdownViewer>((sender, e) =>
{
    // e.Url — the target URL (may be relative, e.g. "#section")
    // sender — the MarkdownViewer that was clicked
    Process.Start(new ProcessStartInfo(e.Url) { UseShellExecute = true });
});
```

The event is raised for all hyperlinks, including anchor links (`#heading`). In-document anchor links are handled automatically by the viewer before raising the event — navigation happens regardless of whether you subscribe.

## Anchor navigation

Call `ScrollToAnchor` to programmatically scroll to any heading or footnote:

```csharp
viewer.ScrollToAnchor("installation");    // matches heading "## Installation"
viewer.ScrollToAnchor("fn-1");            // matches footnote [^1]
```

Anchors are generated from heading text using GitHub-compatible slug rules (lowercase, spaces to hyphens, non-alphanumeric stripped). Headings with identical slugs are disambiguated with a numeric suffix (`-1`, `-2`, …).
