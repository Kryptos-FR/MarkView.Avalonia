# Configuration

## MarkdownViewer properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Markdown` | `string?` | `null` | The markdown text to render. Setting this triggers a full re-render. |
| `Source` | `Uri?` | `null` | URI of a markdown document to load and render. Takes precedence over `Markdown` when set. Supports `avares://`, `file://`, and `http/https`. |
| `Pipeline` | `MarkdownPipeline?` | `null` | The Markdig pipeline. `null` falls back to `MarkdownViewerDefaults.Pipeline`, or the built-in default (`UseSupportedExtensions`). |
| `BaseUri` | `Uri?` | `null` | Base URI for resolving relative links and image paths. When `Source` is set and `BaseUri` is not, it is inferred automatically from the source location. |
| `Extensions` | `IList<IMarkViewExtension>` | `[]` | Per-instance rendering extensions. Applied after global defaults. |
| `ImageResizeMode` | `ImageResizeMode` | `ScaleDownToFit` | Controls how images without an explicit `=WxH` size hint scale relative to their container. See [Image sizing](#image-sizing). |

## Markdig Pipeline

The pipeline controls which Markdig extensions parse the input markdown. Build one with `MarkdownPipelineBuilder`:

```csharp
viewer.Pipeline = new MarkdownPipelineBuilder()
    .UseSupportedExtensions()   // bold, italic, strikethrough, subscript, superscript, underline,
                                // highlight, task lists, tables, autolinks, emoji shortcodes,
                                // CJK-friendly emphasis, YAML front matter (hidden)
    .UseAbbreviations()         // *[HTML]: HyperText Markup Language
    .UseAlertBlocks()           // > [!NOTE] / > [!WARNING] etc.
    .UseCitations()             // ""quoted text""
    .UseFigures()               // ^^^ figure blocks
    .UseFootnotes()             // [^1] footnotes
    .UseHardlineBreaks()        // every soft line break renders as a hard break
    .UseMediaLinks()            // YouTube thumbnail embeds
    .Build();
```

`UseSupportedExtensions()` is a MarkView.Avalonia helper that enables the subset of Markdig extensions that have native renderers in the library, or that are safe to enable unconditionally because they can't surprise ordinary markdown:

- `EmphasisExtras` — strikethrough `~~`, subscript `~`, superscript `^`, underline `++`, highlight `==`
- `AutoLinks` — bare URL auto-linking
- `GridTables` — RST-style grid tables
- `PipeTables` — GFM pipe tables
- `TaskLists` — `- [x]` checkboxes
- `CjkFriendlyEmphasis` — parser-only fix for emphasis next to CJK punctuation
- `YamlFrontMatter` — `---` metadata block at the top of a document is parsed and hidden, not shown as garbled text
- `EmojiAndSmiley` (shortcodes only) — `:rocket:` renders as 🚀; ASCII smileys (`:)`) are intentionally left as plain text, since silently rewriting them is more surprising than useful

The seven opt-in extensions above (`UseFootnotes()` etc.) are thin wrappers around the corresponding Markdig extension; they are defined in `MarkdownExtensions.cs` and re-exported as extension methods on `MarkdownPipelineBuilder`.

### Convenience extension methods

For simple use cases, call a single method instead of building the pipeline manually:

```csharp
viewer.UseAbbreviations();  // pipeline + abbreviation tooltips
viewer.UseAlertBlocks();    // pipeline + alert block rendering
viewer.UseCitations();      // pipeline + citation rendering
viewer.UseFigures();        // pipeline + figure blocks
viewer.UseFootnotes();      // pipeline + footnote rendering
viewer.UseHardlineBreaks(); // pipeline + hardline breaks
viewer.UseMediaLinks();     // pipeline + YouTube thumbnails
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

## Source property

`Source` is a `Uri?` that tells the viewer where to load its markdown from, instead
of supplying the text directly via `Markdown`.

```xml
<!-- Embedded Avalonia resource (avares://) -->
<mv:MarkdownViewer Source="avares://MyApp/Docs/guide.md" />
```

```csharp
// File on disk (file://)
viewer.Source = new Uri("/home/user/documents/notes.md");

// Remote URL
viewer.Source = new Uri("https://example.com/readme.md");

// Clear Source — falls back to the Markdown property
viewer.Source = null;
```

**Supported schemes:**

| Scheme | Loading | Notes |
|--------|---------|-------|
| `avares://` | Synchronous | `ManifestResourceStream` — already in memory, no I/O cost |
| `file://` | Async | Suitable for local and network paths |
| `http/https` | Async | In-flight request is cancelled when `Source` changes |

**Precedence:** when both `Source` and `Markdown` are set, `Source` wins.

**BaseUri inference:** when `Source` is set and `BaseUri` is not, the viewer infers
the base URI from the source directory. Relative image links in the loaded document
resolve correctly without any extra configuration.

## BaseUri

`BaseUri` is used to resolve relative links in markdown when `Source` is not set
(or when you need to override the inferred base):

```csharp
// Load markdown from a GitHub URL — relative image paths resolve against this base
viewer.BaseUri = new Uri("https://raw.githubusercontent.com/org/repo/main/docs/");
viewer.Markdown = File.ReadAllText("README.md");
```

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

## Image sizing

`ImageResizeMode` controls how images without an explicit size scale relative to
the viewer's width:

| Mode | Behavior |
|------|----------|
| `ScaleDownToFit` (default) | Scales down to fit the container width; never enlarges past the image's native resolution. |
| `Natural` | No scaling — renders at native pixel size. May appear cropped if the image is wider than the viewer, since horizontal scrolling is disabled by default. |
| `Fill` | Always fills the container width, enlarging small images if necessary. |

```csharp
viewer.ImageResizeMode = MarkView.Avalonia.ImageResizeMode.Natural;
```

Set an app-wide default with a normal Avalonia style:

```xml
<Style Selector="mv|MarkdownViewer">
  <Setter Property="ImageResizeMode" Value="Fill" />
</Style>
```

**Explicit per-image sizing:** `![alt](url =WxH)` sets a maximum size hint,
independent of `ImageResizeMode`. It always acts as a ceiling — the image is
scaled down to fit `WxH` if larger, but never enlarged past its native resolution
to reach `WxH`, regardless of the active mode.

**Note:** images are no longer capped at 800px by the theme — they scale to the
container width instead (see `ScaleDownToFit` above). To restore the previous
fixed cap, add your own style:

```xml
<Style Selector="Image.markdown-image">
  <Setter Property="MaxWidth" Value="800" />
</Style>
```
