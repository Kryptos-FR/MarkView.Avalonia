# Configuration

## MarkdownViewer properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Markdown` | `string?` | `null` | The markdown text to render. Setting this triggers a full re-render. |
| `Source` | `Uri?` | `null` | URI of a markdown document to load and render. Takes precedence over `Markdown` when set. Supports `avares://`, `file://`, and `http/https`. |
| `Pipeline` | `MarkdownPipeline?` | `null` | The Markdig pipeline. `null` falls back to `MarkdownViewerDefaults.Pipeline`, or the built-in default (`UseGitHubExtensions`). |
| `BaseUri` | `Uri?` | `null` | Base URI for resolving relative links and image paths. When `Source` is set and `BaseUri` is not, it is inferred automatically from the source location. |
| `Extensions` | `IList<IMarkViewExtension>` | `[]` | Per-instance rendering extensions. Applied after global defaults. |
| `ImageResizeMode` | `ImageResizeMode` | `ScaleDownToFit` | Controls how images without an explicit `=WxH` size hint scale relative to their container. See [Image sizing](#image-sizing). |
| `ShowCodeBlockLanguage` | `bool` | `true` | Shows the language of a fenced code block (e.g. `csharp`) in a toolbar above the code. See [Code block toolbar](#code-block-toolbar). |
| `ShowCodeBlockCopyButton` | `bool` | `true` | Shows a button in a toolbar above each code block that copies the code to the clipboard. See [Code block toolbar](#code-block-toolbar). |
| `AreTaskListsInteractive` | `bool` | `false` | Renders task-list items as checkboxes the user can toggle, raising `TaskToggled`. Setting it re-renders the document. See [Interactive task lists](#interactive-task-lists). |
| `TableOfContents` | `IReadOnlyList<TocEntry>` | `[]` | Get-only. The document's headings as a nested tree, recomputed after every render. See [Table of contents](#table-of-contents). |
| `TableOfContentsMaxDepth` | `int` | `6` | Maximum heading level included in `TableOfContents`; deeper headings (and their descendants) are excluded. See [Table of contents](#table-of-contents). |
| `HighContrastMode` | `HighContrastMode` | `System` | When to use the high-contrast palette: follow the OS (`System`), `Enabled` or `Disabled`. See [Accessibility](accessibility.md#high-contrast). |
| `IsHighContrast` | `bool` | `false` | Get-only. Whether the high-contrast palette is in use; mirrors the `:high-contrast` pseudo-class. |

## Markdig Pipeline

The pipeline controls which Markdig extensions parse the input markdown. Build one with `MarkdownPipelineBuilder`:

```csharp
viewer.Pipeline = new MarkdownPipelineBuilder()
    .UseGitHubExtensions()      // what GitHub renders: tables, task lists, ~~strike~~ and ~strike~,
                                // autolinks, emoji shortcodes, alerts, footnotes, CJK-friendly emphasis,
                                // YAML front matter (hidden). The viewer's default pipeline.
    .UseAbbreviations()         // *[HTML]: HyperText Markup Language
    .UseCitations()             // ""quoted text""
    .UseFigures()               // ^^^ figure blocks
    .UseHardlineBreaks()        // every soft line break renders as a hard break
    .UseMediaLinks()            // YouTube thumbnail embeds
    .UseDefinitionLists()       // Term + ":   definition"
    .UseCustomContainers()      // :::name … ::: blocks and ::text:: inlines
    .UseFooters()               // ^^ footer lines
    .UseSmartyPants()           // "quotes" -- --- ... << >> → “ ” – — … « »
    .UseListExtras()            // a. / A. / i. / I. ordered lists
    .UseGenericAttributes()     // {.name} style classes, must be the last extension
    .Build();
```

MarkView.Avalonia has two helpers that enable a base set of extensions. Both take a `MarkdownExtensionsOptions` (pipe table options).

`UseGitHubExtensions()` makes a document render as it does on GitHub. It is what a viewer uses when no pipeline is set:

- `PipeTables` — GFM pipe tables
- `TaskLists` — `- [x]` checkboxes
- Strikethrough — `~~text~~` and, as on GitHub, `~text~`
- `AutoLinks` — bare URL auto-linking
- `EmojiAndSmiley` (shortcodes only) — `:rocket:` renders as 🚀; ASCII smileys (`:)`) are intentionally left as plain text, since silently rewriting them is more surprising than useful
- `AlertBlocks` — `> [!NOTE]`, `> [!WARNING]` etc. (see [Alert blocks](#alert-blocks) for the extras)
- `Footnotes` — `[^1]` references and their notes
- `CjkFriendlyEmphasis` — parser-only fix for emphasis next to Chinese/Japanese/Korean (CJK) punctuation
- `YamlFrontMatter` — `---` metadata block at the top of a document is parsed and hidden, not shown as garbled text

Math and Mermaid diagrams, which GitHub also renders, come from their own packages (`UseMath()`, `UseMermaid()`).

> [!NOTE]
> **Subscript and `UseGitHubExtensions()` don't mix.** `~text~` is either strikethrough (GitHub) or subscript (Markdig's emphasis extras), and with `UseGitHubExtensions()` it is always strikethrough, even if `UseEmphasisExtras()` is added after it. Adding `UseEmphasisExtras()` still gives superscript `^`, underline `++` and highlight `==`.
>
> For subscript, start from `UseSupportedExtensions()` instead and add what you need from the GitHub set. `~~text~~` stays strikethrough:
>
> ```csharp
> viewer.Pipeline = new MarkdownPipelineBuilder()
>     .UseSupportedExtensions()   // ~text~ is subscript
>     .UseAlertBlocks()
>     .UseFootnotes()
>     .Build();
> ```

`UseSupportedExtensions()` was the default before `UseGitHubExtensions()`. It enables the other emphasis extras and grid tables, but not alerts or footnotes:

- `EmphasisExtras` — strikethrough `~~`, subscript `~`, superscript `^`, underline `++`, highlight `==`
- `AutoLinks`, `PipeTables`, `TaskLists`, `CjkFriendlyEmphasis`, `YamlFrontMatter` and emoji shortcodes, as above
- `GridTables` — RST-style grid tables

To keep the previous rendering (`~text~` as subscript), set it as the default pipeline:

```csharp
MarkdownViewerDefaults.Pipeline = new MarkdownPipelineBuilder().UseSupportedExtensions().Build();
```

The opt-in extensions above are Markdig's own pipeline methods, except `UseHardlineBreaks()`, a MarkView.Avalonia shorthand for Markdig's `UseSoftlineBreakAsHardlineBreak()`. Import both `Markdig` and `MarkView.Avalonia` to use them together.

The last six (`UseDefinitionLists()` to `UseGenericAttributes()`) render as follows:

- **Definition lists** render terms as `TextBlock.markdown-definition-term` and each definition as an indented `StackPanel.markdown-definition`.
- **Custom containers** render as `Border.markdown-custom-container` with the container's name as an extra class, so `:::warning` can be styled with `Border.markdown-custom-container.warning`. Text after the name (`:::warning Read this first`) becomes a `markdown-custom-container-title` header. Inline `::text::` renders as `Span.markdown-custom-inline`.
- **Footers** render as `StackPanel.markdown-footer`, in smaller, dimmed text by default.
- **SmartyPants** uses the replacements of the `SmartyPantOptions` passed to `UseSmartyPants()`, decoding HTML entities, so for example French guillemets with non-breaking spaces work:

  ```csharp
  var smartyPants = new SmartyPantOptions();
  smartyPants.Mapping[SmartyPantType.LeftDoubleQuote] = "&laquo;&nbsp;";
  smartyPants.Mapping[SmartyPantType.RightDoubleQuote] = "&nbsp;&raquo;";
  viewer.Pipeline = new MarkdownPipelineBuilder().UseGitHubExtensions().UseSmartyPants(smartyPants).Build();
  ```

- **List extras** number items with letters or roman numerals. The `.` or `)` after the number is kept for every ordered list.
- **Generic attributes** add each `{.name}` class to the control or inline rendered for that element (heading, paragraph, link, emphasis, container…), so your own styles can target it, for example `TextBlock.markdown-paragraph.note`. Ids and other attributes are ignored. Markdig only parses attributes for the extensions set up before it, so call `UseGenericAttributes()` last.

### Alert blocks

The default pipeline renders GitHub alerts. MarkView's `UseExtendedAlertBlocks()` enables them too (as Markdig's `UseAlertBlocks()` does) and extends them with the Obsidian-style first-line extras, which Markdig alone leaves as a plain blockquote. `viewer.UseAlertBlocks()` turns them on too:

```markdown
> [!NOTE]
> Plain GitHub alert: the header shows the kind.

> [!TIP] Custom *title*
> The header shows "Custom title" (inline formatting kept) instead of "TIP".

> [!WARNING]- Collapsed by default
> Click the header, or focus it and press Enter or Space, to show this body.

> [!IMPORTANT]+
> Foldable, starts expanded.
```

- The fold marker (`-` collapsed, `+` expanded) must follow `]` directly; a title must be separated from `]` or the marker by a space or tab. `> [!NOTE]x` is not an alert.
- Any kind made of ASCII letters is accepted, as in Markdig: `> [!QUESTION]` renders with the neutral alert style plus a `markdown-alert-question` class.
- The body of a collapsed alert cannot be selected or copied. `ScrollToAnchor` (and so in-document links and table-of-contents navigation) expands the collapsed alerts around a heading before scrolling to it.
- The extras need `UseExtendedAlertBlocks()`; a pipeline built with only Markdig's `UseAlertBlocks()`/`UseAdvancedExtensions()` parses plain alerts. The two can be combined in either order. See [Theming](theming.md#alert-blocks) for the style classes.

### Convenience extension methods

For simple use cases, call a single method instead of building the pipeline manually:

```csharp
viewer.UseAbbreviations();  // pipeline + abbreviation tooltips
viewer.UseAlertBlocks();    // pipeline + foldable alerts and custom alert titles
viewer.UseCitations();      // pipeline + citation rendering
viewer.UseFigures();        // pipeline + figure blocks
viewer.UseFootnotes();      // pipeline (footnotes are already in it)
viewer.UseHardlineBreaks(); // pipeline + hardline breaks
viewer.UseMediaLinks();     // pipeline + YouTube thumbnails
```

Each method calls `UseGitHubExtensions()` plus the requested feature. To combine several opt-in features, build the pipeline explicitly.

## Application-wide defaults (`MarkdownViewerDefaults`)

Set the pipeline and extensions once at application startup and they apply to **every** `MarkdownViewer` in the app:

```csharp
// App.axaml.cs  OnFrameworkInitializationCompleted()
MarkdownViewerDefaults.Pipeline = new MarkdownPipelineBuilder()
    .UseGitHubExtensions()
    .UseExtendedAlertBlocks()
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

**Fragment navigation:** if `Source` has a `#fragment`, the viewer scrolls to that
anchor automatically once the document has rendered — e.g. `new Uri(path + "#installation")`
behaves like `viewer.Source = new Uri(path); viewer.ScrollToAnchor("installation")`,
but without needing to wait for rendering to complete first.

For `avares://` sources, everything after the first `#` is treated as the fragment, so an
embedded asset whose path contains `#` (e.g. `Docs/C#.md`) cannot be loaded through `Source`.
Rename the asset or load it yourself with `AssetLoader.Open` and assign the text to `Markdown`.

## BaseUri

`BaseUri` is used to resolve relative links in markdown when `Source` is not set
(or when you need to override the inferred base):

```csharp
// Load markdown from a GitHub URL — relative image paths resolve against this base
viewer.BaseUri = new Uri("https://raw.githubusercontent.com/org/repo/main/docs/");
viewer.Markdown = File.ReadAllText("README.md");
```

## LinkClicked event

By default, clicking an external link opens it with the platform's `ILauncher`
(the OS default browser/app on desktop, an intent on mobile, a new tab on
WebAssembly) — no application code required.

`LinkClicked` is an Avalonia **routed event** that bubbles up the visual tree, raised
before that default launch happens. Subscribe to override the behavior for specific
links — set `e.Handled = true` to suppress the default launch:

```csharp
viewer.LinkClicked += (_, e) =>
{
    if (e.Url.EndsWith(".md"))
    {
        // Render the linked document in-place instead of opening it externally.
        viewer.Source = new Uri(e.Url);
        e.Handled = true;
    }
};
```

Or register a class-level handler once at startup to catch all viewers:

```csharp
MarkdownViewer.LinkClickedEvent.AddClassHandler<MarkdownViewer>((sender, e) =>
{
    // e.Url — the target URL (may be relative, e.g. "#section")
    // sender — the MarkdownViewer that was clicked
    ...
});
```

The event is raised for all hyperlinks, including anchor links (`#heading`). In-document anchor links are handled automatically by the viewer before raising the event — navigation happens regardless of whether you subscribe, and the default external-link launch never runs for them.

## Interactive task lists

By default, task-list items (`- [ ]` / `- [x]`) render a static ☐/☑ glyph. Set
`AreTaskListsInteractive` to render a checkbox instead — for note-taking or to-do apps where the
reader ticks items off:

```xml
<mv:MarkdownViewer Markdown="{Binding Notes}" AreTaskListsInteractive="True" />
```

Clicking the checkbox (or pressing Space / Enter while it has keyboard focus) toggles it and raises
the `TaskToggled` routed event (bubbles up, like `LinkClicked`). **The viewer never edits its own
markdown**: the checkbox shows the new state, but `Markdown` is unchanged until the host writes
the change back. `TaskToggledEventArgs` carries what that takes:

| Property | Description |
|----------|-------------|
| `IsChecked` | The item's new state. |
| `SourceStart` | Zero-based index of the marker's `[` in the rendered markdown. |
| `SourceLength` | Length of the marker — always `3` (`[ ]`, `[x]` or `[X]`). |
| `Line` | Zero-based line of the marker in the rendered markdown. |

Positions refer to the markdown the viewer rendered: the `Markdown` string exactly as you set it
(the viewer's internal rewriting of `![alt](url =WxH)` image size hints is already accounted for),
or the document text loaded from `Source`. Patch the marker and assign the result back:

```csharp
viewer.TaskToggled += (_, e) =>
{
    var source = viewer.Markdown!;
    viewer.Markdown = string.Concat(
        source.AsSpan(0, e.SourceStart),
        e.IsChecked ? "[x]" : "[ ]",
        source.AsSpan(e.SourceStart + e.SourceLength));
    // ...and save it to wherever the markdown came from.
};
```

The viewer applies each toggle to its own copy of the rendered text the same way (`[x]` or `[ ]`).
When the text assigned back equals that copy, as in the handler above, the document is not
re-rendered, so the reader keeps their scroll position and keyboard focus. Any other text re-renders
as usual and scrolls back to the top. A toggle that is never written back is lost at the next
re-render, for example after assigning a different text or changing `Pipeline`.

Pressing the checkbox and dragging selects text instead of toggling it, and a selection started on
text can still be dragged across items. When copied, a checkbox contributes its current ☐/☑ glyph.

## Anchor navigation

Call `ScrollToAnchor` to programmatically scroll to any heading or footnote:

```csharp
viewer.ScrollToAnchor("installation");    // matches heading "## Installation"
viewer.ScrollToAnchor("fn-1");            // matches footnote [^1]
```

Anchors are generated from heading text using GitHub-compatible slug rules (lowercase, spaces to hyphens, non-alphanumeric stripped). Headings with identical slugs are disambiguated with a numeric suffix (`-1`, `-2`, …).

A heading inside a collapsed [foldable alert](#alert-blocks) is still listed in `TableOfContents`; scrolling to its anchor expands the alert first.

## Table of contents

`TableOfContents` exposes the document's headings as a nested tree you can bind
to your own UI (a side panel, dropdown, breadcrumb strip, etc.) — the library
does not ship a TOC control itself, since the right presentation varies by app.

```csharp
foreach (var entry in viewer.TableOfContents)
{
    Console.WriteLine($"{new string(' ', (entry.Level - 1) * 2)}{entry.Text}");
    // entry.Slug matches the anchor id registered for this heading —
    // pass it to ScrollToAnchor to navigate.
}
```

Each `TocEntry` has `Level`, `Text`, `Slug`, and `Children` (nested headings one or
more levels deeper). `TableOfContentsMaxDepth` (default `6`, i.e. no limit) drops
headings deeper than the given level, along with their descendants:

```csharp
viewer.TableOfContentsMaxDepth = 2; // only H1 and H2 headings
```

`TableOfContents` is recomputed automatically whenever `Markdown`, `Source`, or
`TableOfContentsMaxDepth` changes. See `samples/MarkView.Avalonia.Demo` for
example side-panel, dropdown, and top-dock integrations.

## Image sizing

`ImageResizeMode` controls how images without an explicit size scale relative to
the viewer's width:

| Mode | Behaviour |
|------|----------|
| `ScaleDownToFit` (default) | Scales down to fit the container width; never enlarges past the image's native resolution. |
| `Natural` | No scaling — renders at native pixel size. May appear cropped if the image is wider than the viewer, since horizontal scrolling is disabled by default. |
| `Fill` | Always fills the container width, enlarging small images if necessary. |

`Fill` and `ScaleDownToFit` scale images against the nearest width-constraining
ancestor in the control's template. The default template provides this via
`PART_ScrollViewer`'s `HorizontalScrollBarVisibility`, which defaults to
`Disabled` and constrains width without permitting horizontal scroll. Setting
`ScrollViewer.HorizontalScrollBarVisibility="Auto"` (or `Visible`) directly on
a `MarkdownViewer` instance — or a custom `Template` that drops
`PART_ScrollViewer` entirely — removes that constraint; those modes may then
behave like `Natural`. `Natural` itself is unaffected, since it never scales.

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

## Code block toolbar

Code blocks get a small toolbar above the code, with the block's language on the left and a
copy button on the right. The button copies the code exactly as written in the markdown, and
switches to a check mark for two seconds once the copy succeeded. Both parts can be turned off
independently:

```xml
<mv:MarkdownViewer ShowCodeBlockLanguage="False" ShowCodeBlockCopyButton="True" />
```

```csharp
viewer.ShowCodeBlockCopyButton = false;
```

Or for every viewer of the application, with a normal Avalonia style:

```xml
<Style Selector="mv|MarkdownViewer">
  <Setter Property="ShowCodeBlockLanguage" Value="False" />
  <Setter Property="ShowCodeBlockCopyButton" Value="False" />
</Style>
```

The toolbar is only added when it has something to show: a block without a language
(an indented block, or a fence without an info string) with the copy button turned off is
rendered exactly as before, with no toolbar. Changing either property re-renders the document.

The toolbar applies to the built-in code block renderer and to the code blocks of the
`SyntaxHighlighting` and `Mermaid` (non-diagram fences) packages. Mermaid diagrams and math
blocks have no toolbar. Text selection skips the toolbar, so selecting and copying across a
code block copies only the code. See [Theming](theming.md#code-block-toolbar) to restyle it.
