# Configuration

## MarkdownViewer properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Markdown` | `string?` | `null` | The markdown text to render. Setting this triggers a full re-render. |
| `Source` | `Uri?` | `null` | URI of a markdown document to load and render. Takes precedence over `Markdown` when set. Supports `avares://`, `file://`, and `http/https`. |
| `Pipeline` | `MarkdownPipeline?` | `null` | The Markdig pipeline. `null` falls back to `MarkdownViewerDefaults.Pipeline`, or the built-in default (`UseGitHubExtensions`). |
| `BaseUri` | `Uri?` | `null` | Base URI for resolving relative links and image paths. When `Source` is set and `BaseUri` is not, it is inferred automatically from the source location. |
| `Extensions` | `IList<IMarkViewExtension>` | `[]` | Per-instance rendering extensions. Applied after global defaults. See also [Custom fenced blocks](#custom-fenced-blocks). |
| `ImageResizeMode` | `ImageResizeMode` | `ScaleDownToFit` | Controls how images without an explicit `=WxH` size hint scale relative to their container. See [Image sizing](#image-sizing). |
| `ShowCodeBlockLanguage` | `bool` | `true` | Shows the language of a fenced code block (e.g. `csharp`) in a toolbar above the code. See [Code block toolbar](#code-block-toolbar). |
| `ShowCodeBlockCopyButton` | `bool` | `true` | Shows a button in a toolbar above each code block that copies the code to the clipboard. See [Code block toolbar](#code-block-toolbar). |
| `ShowCodeBlockLineNumbers` | `bool` | `false` | Shows the number of each line in a gutter next to the code. Selection and copy never include the numbers. See [Line numbers, wrapping and height](#line-numbers-wrapping-and-height). |
| `WrapCodeBlockLines` | `bool` | `false` | Wraps code lines longer than the block's width instead of letting them run past it. See [Line numbers, wrapping and height](#line-numbers-wrapping-and-height). |
| `CodeBlockMaxHeight` | `double` | `PositiveInfinity` | Maximum height of a code block (toolbar excluded); a taller block scrolls vertically inside itself. See [Line numbers, wrapping and height](#line-numbers-wrapping-and-height). |
| `AreTaskListsInteractive` | `bool` | `false` | Renders task-list items as checkboxes the user can toggle, raising `TaskToggled`. Setting it re-renders the document. See [Interactive task lists](#interactive-task-lists). |
| `IsImageZoomEnabled` | `bool` | `true` | Clicking an image or a Mermaid diagram opens it in a lightbox over the viewer, where it can be zoomed and panned. See [Image zoom](#image-zoom). |
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
                                // YAML front matter (hidden), <details> blocks, the HTML READMEs use.
                                // The viewer's default pipeline.
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
- `HtmlDetails` — HTML `<details>` / `<summary>` blocks fold as on GitHub (see [HTML details blocks](#html-details-blocks)); MarkView's `UseHtmlDetails()` adds them to another pipeline
- `HtmlSubset` — the HTML READMEs use (centered headers, badges, `<kbd>`, `<sub>`…) renders with native controls (see [HTML subset](#html-subset)); MarkView's `UseHtmlSubset()` adds it to another pipeline

Math (`$...$`, and the `\(...\)`/`\[...\]` delimiters LLMs emit) and Mermaid diagrams, which GitHub also renders, come from their own packages (`UseMath()`, `UseMermaid()`).

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

`UseSupportedExtensions()` was the default before `UseGitHubExtensions()`. It enables the other emphasis extras and grid tables, but not alerts, footnotes or details blocks:

- `EmphasisExtras` — strikethrough `~~`, subscript `~`, superscript `^`, underline `++`, highlight `==`
- `AutoLinks`, `PipeTables`, `TaskLists`, `CjkFriendlyEmphasis`, `YamlFrontMatter`, emoji shortcodes and the HTML subset, as above
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

### HTML details blocks

The default pipeline renders HTML `<details>` / `<summary>` elements as collapsible blocks, as GitHub does. To add them to a pipeline built from something else, call MarkView's `UseHtmlDetails()`:

```markdown
<details>
<summary>Click to expand</summary>

Ordinary **markdown**: paragraphs, lists, code blocks, headings, even nested `<details>`.

</details>

<details open>
<summary>Starts expanded</summary>

Shown until the summary is clicked.

</details>
```

- Click the summary, or focus it and press Enter or Space, to show or hide the body. It starts hidden unless the tag has the `open` attribute. Without a `<summary>`, the summary reads "Details", as in browsers.
- Leave a blank line between the tags and the markdown inside: as on GitHub, text on lines right next to the tags belongs to the same HTML block and is not parsed as markdown. With the [HTML subset](#html-subset) (in the default pipeline), such text, and the summary, render as that HTML: `<summary><b>Title</b></summary>` reads "**Title**". Without it, MarkView shows them as plain text (tags removed, entities decoded, `<br>` as a line break): "Title", not bold.
- A `<details>` without its `</details>` (or the reverse), or one whose closing tag is in another container (e.g. opened outside a list and closed inside it), is left as raw HTML: the HTML subset shows its text with the tags dropped, and a pipeline without it does not render it, as before. Other HTML blocks are left to the HTML subset too, inside a details block or not.
- As with [foldable alerts](#alert-blocks), the body of a collapsed details block cannot be selected or copied, and `ScrollToAnchor` expands the details blocks around a heading before scrolling to it. See [Theming](theming.md#details-blocks) for the style classes.
- A pipeline that disables HTML (Markdig's `DisableHtml()`) has no details blocks either.

### HTML subset

GitHub READMEs often use a little HTML: a centered logo and title, a row of badges, `<kbd>` keys, `<sub>` and `<sup>`. The default pipeline renders a small, safe subset of HTML with native controls, the way GitHub shows it, without building a browser: nothing is run, and nothing is fetched but the `src` of an `<img>`, through the viewer's image loaders like a markdown image. To add it to a pipeline built from something else, call MarkView's `UseHtmlSubset()` (also part of `UseSupportedExtensions()`).

```markdown
<p align="center">
  <img src="docs/logo.png" alt="Logo" width="120"><br>
  <b>MyProject</b>
</p>

<p align="center">
  <a href="https://github.com/me/myproject/actions"><img src="https://img.shields.io/badge/build-passing-green" alt="Build"></a>
  <a href="https://www.nuget.org/packages/MyProject"><img src="https://img.shields.io/badge/nuget-1.0-blue" alt="NuGet"></a>
</p>

Press <kbd>Ctrl</kbd>+<kbd>C</kbd> to copy H<sub>2</sub>O and E = mc<sup>2</sup>.
```

Inline elements, in paragraphs, headings, table cells, list items and details summaries:

| Tags | Renders as |
|------|------------|
| `<b>`, `<strong>` | Bold, like `**text**` |
| `<i>`, `<em>` | Italic, like `*text*` |
| `<del>`, `<s>`, `<strike>` | Strikethrough, like `~~text~~` |
| `<ins>`, `<u>` | Underline |
| `<mark>` | Highlighted, like `==text==` |
| `<sub>`, `<sup>` | Subscript and superscript, on a smaller font |
| `<kbd>` | A keyboard key; selection copies its text |
| `<code>` | Inline code, like `` `code` ``, of the element's text |
| `<a href="…" title="…">` | A link, like `[text](href "title")`: same click handling (`LinkClicked`), URL resolution against `BaseUri` and style |
| `<img src="…" alt="…" title="…" width="…" height="…">` | An image, like `![alt](src)`: same image loaders (SVG, bitmaps), URL resolution, tooltip and copy text. `width` and `height`, in pixels, cap its size like the `=WxH` [size hint](#image-sizing), and either can be given alone; other values (`50%`) are ignored |
| `<br>` | A line break |

HTML blocks — HTML that starts a line, which CommonMark does not parse markdown inside:

| Tags | Renders as |
|------|------------|
| `<p>`, `<h1>`…`<h6>` | A paragraph or heading of the inline HTML inside it. A heading gets an anchor and is listed in `TableOfContents`, like a markdown heading |
| `<div>` | Its content; text and inline elements outside any `<p>` become a paragraph |
| `align="left\|center\|right\|justify"` on `<p>`, `<div>` and `<h1>`…`<h6>` | Aligns the text and images inside it, as `text-align` does |
| `<hr>` | A thematic break, like `---` |

A `<div align="center">` and its `</div>`, each separated by blank lines from the markdown between them, align that markdown too, as on GitHub:

```markdown
<div align="center">

# MyProject

![Logo](docs/logo.png)

</div>
```

- Only the `href`, `src`, `alt`, `title`, `width`, `height` and `align` attributes are read; any other (`style`, `class`, `onclick`…) is ignored.
- Any other tag (`<span>`, `<table>`, `<iframe>`, `<picture>`…), and a tag without its match (`<b>` never closed, a stray `</i>`), is dropped, and the text around it still shows, as it did before. The content of `<script>` and `<style>` is dropped with them, as GitHub does. Comments do not show.
- Tags pair like elements: `<b><i>x</i></b>` nests, and `</b>` in `<b><i>x</b>` also closes the `<i>`. Inline tags pair within the same markdown inline only: in `*<b>x*</b>`, the `<b>` is inside the emphasis and the `</b>` outside, so both are dropped. A `<div>` pairs with a `</div>` among the blocks of the same container (as for [details blocks](#html-details-blocks)); one without its match is dropped.
- A tag that names no element of HTML or MathML shows as the text it was typed as, so `List<string>`, `Vec<u8>` and `Option<T>` read as written, where GitHub shows only `List`, `Vec` and `Option`. Custom elements (a name with a hyphen, `<my-widget>`) are elements and are dropped. The content of `<svg>` is dropped like that of `<script>`, so its shapes don't show as text. A placeholder that is also an element's name, such as `<input>` or `<title>`, is still dropped: put it in backticks, or use the `Escape` [HTML mode](#html-mode).
- A `<p>` or heading ends at its end tag, at the next block tag, or at the end of its HTML block. Whitespace collapses as in HTML.
- HTML blocks Markdig starts with `<script>`, `<style>`, `<pre>`, `<textarea>`, a comment or a declaration are still not rendered.
- A link inside a details summary shows as a link but is not clickable: clicking the summary toggles the details block.
- A pipeline that disables HTML (Markdig's `DisableHtml()`, or `UseHtmlMode(HtmlMode.Escape)`) shows HTML as literal text. See [Theming](theming.md#html-subset) for the style classes.

### HTML mode

`UseHtmlMode()` sets how a pipeline treats HTML, whichever extensions come before or after it (the last call wins):

| Mode | HTML renders as | For |
|------|-----------------|-----|
| `HtmlMode.SafeSubset` | The [HTML subset](#html-subset) and [details blocks](#html-details-blocks); other elements are dropped and their text shows, as on GitHub. The mode of the default pipeline | READMEs and documentation |
| `HtmlMode.Escape` | The literal text it was typed as: `<b>x</b>` reads `<b>x</b>`, as with Markdig's `DisableHtml()` | Text whose tags are not meant as HTML, such as chat messages that mention `<div>` or `<input>` |
| `HtmlMode.Ignore` | Nothing: tags are dropped, the text between inline tags shows, and HTML blocks don't show. `<br>` still breaks the line, as markdown has no other way to do so in a table cell | Showing only the markdown |

```csharp
// Chat messages: show what was typed
MarkdownViewerDefaults.Pipeline = new MarkdownPipelineBuilder()
    .UseGitHubExtensions()
    .UseHtmlMode(HtmlMode.Escape)
    .Build();
```

- In every mode, nothing is run, and nothing is fetched but the `<img>` sources of the subset.
- With `Escape`, the markdown between the tags of what would be an HTML block is parsed as usual: in `<div>` / `**bold**` / `</div>` on three lines, the tags show and **bold** is bold. Autolinks (`<https://example.com>`) and entities (`&amp;`) are not HTML and still work.
- With `Ignore`, the markdown between the tags of an HTML block does not show either, since CommonMark makes the whole block HTML. Leave blank lines around the markdown inside a `<div>` or `<details>` to keep it.

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
MarkdownViewerDefaults.Extensions.AddFenceRenderer("chart", (code, info) => new MyChart(code));
```

**Priority rules:**

1. `viewer.Pipeline` is used if set; otherwise `MarkdownViewerDefaults.Pipeline`; otherwise the built-in default.
2. `MarkdownViewerDefaults.Extensions` are registered first, then `viewer.Extensions`.
3. If the same extension object appears in both lists it is registered only once (reference equality check).
4. For [fence renderers](#custom-fenced-blocks), the last one registered for a language wins, so a viewer's own overrides a default one.

### Image cache

Loaded images are kept in an in-memory cache shared by every viewer, so rendering an image again
(the document edited and re-rendered, a viewer recreated when its tab is shown again, the same image
twice in a document) neither downloads nor decodes it again:

```csharp
MarkdownViewerDefaults.ImageCacheSize = 200; // images kept; 0 disables the cache. Default: 100
MarkdownViewerDefaults.ClearImageCache();    // e.g. after the images behind some URLs have changed
```

- An image is cached under its resolved URL (after `BaseUri`) and the types of the
  [image loaders](custom-extensions.md#writing-a-custom-image-loader) in use, since another loader
  chain (one with the SVG loader, say) can load the same URL differently. A custom loader whose result
  depends on more than the URL and its type (e.g. settings passed to its constructor) should use URLs
  that tell those results apart, or the cache should be cleared when the settings change.
- Once it holds `ImageCacheSize` images, the least recently used one is evicted first. Evicted images
  are never disposed, since a viewer may still show them; they are collected once nothing does.
  Lowering `ImageCacheSize` evicts the images beyond the new size at once.
- A failed load is never cached: the next render tries again.
- Loads of the same image that run at the same time (two viewers, or two copies of the image) share
  one download. It is cancelled only when every viewer waiting for it has dropped the image.
- `ClearImageCache()` keeps the images already shown; only the next render loads them again.
- The cache is thread-safe. With `ImageCacheSize = 0`, no image is cached and every image is loaded
  on its own, even when the same one is loading elsewhere.

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

A heading inside a collapsed [foldable alert](#alert-blocks) or [details block](#html-details-blocks) is still listed in `TableOfContents`; scrolling to its anchor expands it first.

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
to reach `WxH`, regardless of the active mode. The `width` and `height` attributes of an
HTML `<img>` ([HTML subset](#html-subset)) act the same way, and either can be given alone.

**Note:** images are no longer capped at 800px by the theme — they scale to the
container width instead (see `ScaleDownToFit` above). To restore the previous
fixed cap, add your own style:

```xml
<Style Selector="Image.markdown-image">
  <Setter Property="MaxWidth" Value="800" />
</Style>
```

### Images that fail to load

When an image cannot be loaded (no image loader accepts its URL, the loaders return nothing, a loader
throws, the server answers with an error, or the data cannot be decoded), the viewer shows a small
placeholder in its place: a drawn broken-image icon followed by the image's alt text, in a muted
colour (`Border.markdown-image-failed`, see [Theming](theming.md#images)). Selection still copies the
alt text. Removing an image while it loads (a re-render, a closed document) cancels its load and shows
nothing.

The placeholder keeps its own compact size, one line of text high, whatever `=WxH` hint the image
has: the hint only caps it, and an alt text wider than `W` is cut with an ellipsis. Blowing the
placeholder up to the size of a picture that isn't there would leave a large empty box in the text.

## Image zoom

Clicking an image opens it in a lightbox laid over the viewer: a dimmed backdrop with the image as
large as fits. Images are never enlarged past their natural size to fit, so a small image stays sharp at
its own size rather than being blown up. Clicking anywhere but on the zoom toolbar, the close button in
the top right corner or pressing Escape closes it.

[Mermaid diagrams](../src/MarkView.Avalonia.Mermaid/README.md) open the same way. They are drawn as
vectors, so the lightbox enlarges a small diagram to fit, up to twice its natural size.

In the lightbox, the picture can be zoomed and panned:

- The mouse wheel, a pinch (touch screen or touchpad) or the + and − keys zoom, about the pointer, the
  pinch or the centre. The toolbar at the bottom has zoom out and zoom in buttons, the zoom level, and a
  button that fits the picture again (as does the 0 key).
- Dragging or the arrow keys pan a picture larger than the lightbox, as far as its edges. A drag does not
  close the lightbox.
- The zoom goes from the fitted size (or the natural size, if smaller) to eight times the natural size
  (or the fitted size, if larger). A fitted picture is fitted again when the viewer is resized; a zoomed
  one keeps its zoom.
- The lightbox shows the current source of the image it was opened from, so a diagram re-rendered for a
  new theme shows in its new colours.

- Zoomable images and diagrams show a zoom-in cursor. They are also focusable: Enter or Space opens the
  lightbox, which takes focus (Tab moves between it and its buttons only); when it closes, focus returns
  to where it was.
- An image inside a link (`[![alt](image.png)](https://example.com)`) opens the link instead.
- Ending a text selection drag on an image does not open it.
- The lightbox covers the viewer (not the whole window) and is part of the viewer itself, so it works
  with any template and on every platform. Rendering another document closes it.

Turn it off with `IsImageZoomEnabled`, per viewer or for every viewer of an application:

```xml
<Style Selector="mv|MarkdownViewer">
  <Setter Property="IsImageZoomEnabled" Value="False" />
</Style>
```

The lightbox's colours, close button and toolbar are themable, see [Theming](theming.md#images).

## Code block toolbar

Code blocks get a small toolbar above the code, with the block's language on the left and a
copy button on the right. The button copies the code exactly as written in the markdown, and
switches to a check mark for two seconds once the copy succeeded. In a `MarkdownViewer`, the copy
raises [`CopyingToClipboard`](text-selection.md#changing-what-is-copied) first, so the
application can change or cancel it. Both parts can be turned off
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

### Line numbers, wrapping and height

Three more options change how the code itself is laid out. All are off by default:

```xml
<mv:MarkdownViewer ShowCodeBlockLineNumbers="True" WrapCodeBlockLines="True" CodeBlockMaxHeight="400" />
```

```csharp
viewer.ShowCodeBlockLineNumbers = true;
viewer.WrapCodeBlockLines = true;
viewer.CodeBlockMaxHeight = 400;
```

Like the toolbar's, they can be set for every viewer of the application with a style, and
changing one re-renders the document. They apply to the same code blocks as the toolbar.

- **`ShowCodeBlockLineNumbers`** puts the number of each line in a gutter on the left of the
  code. The numbers are not part of the code: dragging a selection over the block, `Ctrl+A` and
  the copy button all copy the code alone, and the gutter itself cannot be selected. With
  wrapping on, a wrapped line is numbered once, on its first row.
- **`WrapCodeBlockLines`** wraps lines longer than the block. Without it, code never wraps:
  long lines run past the block's right edge and are cut off by the viewer, unless the viewer
  scrolls horizontally (`ScrollViewer.HorizontalScrollBarVisibility="Auto"` on the
  `MarkdownViewer`, see [Image sizing](#image-sizing)). Wrapping needs a width to wrap at, so it
  has no effect while the viewer scrolls horizontally.
- **`CodeBlockMaxHeight`** caps the height of each code block, padding included and toolbar
  excluded (the toolbar stays in view). A taller block keeps that height and scrolls vertically
  inside itself; shorter blocks are unchanged. Horizontal scrolling is unaffected: it stays
  with the viewer, as without a maximum height. The mouse wheel over a block scrolls the block
  first; once the block has reached its top or bottom, the wheel scrolls the document again
  (Avalonia's scroll chaining), so a long block never traps it. Must be zero or more; the
  default, `double.PositiveInfinity`, means no limit.

Wrapping is a viewer property only, with no wrap toggle in the toolbar, to keep the toolbar
small; a host that wants one can bind its own toggle to `WrapCodeBlockLines`. See
[Theming](theming.md#line-numbers-and-scrolling-code) to restyle the gutter and the scroll viewer.

## Custom fenced blocks

Fenced code blocks of a language can render as your own control instead of code, without writing
a Markdig renderer:

```csharp
viewer.UseFenceRenderer("chart", (code, info) => new MyChart(code));
// for every viewer of the application:
MarkdownViewerDefaults.Extensions.AddFenceRenderer("chart", (code, info) => new MyChart(code));
```

The factory receives the block's content (`code`, without the fences) and its whole info string
(`info`, e.g. `chart {height=200}` for ```` ```chart {height=200} ````), and returns a new control,
or `null` to render that block as code. The language is matched case-insensitively, and a fence
renderer takes precedence over Mermaid diagrams, syntax highlighting and the plain code block,
whatever order they were registered in. A factory that throws renders its block as code and logs
a warning. `UseFenceRenderer` replaces the viewer's earlier fence renderer for the same language
and re-renders the document.

The control is wrapped in a `Border` with the `markdown-custom-fence` and `markdown-fence-<language>`
classes, and text selection copies it as its fenced source. See
[Writing custom extensions](custom-extensions.md#rendering-a-fence-language-as-your-own-control)
for the details, including how to follow theme changes.
