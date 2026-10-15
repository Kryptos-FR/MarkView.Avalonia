# MarkView.Avalonia

[![NuGet Version](https://img.shields.io/nuget/v/MarkView.Avalonia)](https://www.nuget.org/packages/MarkView.Avalonia)
[![NuGet Downloads](https://img.shields.io/nuget/dt/MarkView.Avalonia)](https://www.nuget.org/packages/MarkView.Avalonia)
[![Avalonia](https://img.shields.io/badge/Avalonia-12-blue)](https://avaloniaui.net)
[![License](https://img.shields.io/github/license/Kryptos-FR/MarkView.Avalonia)](LICENSE.md)
[![CI](https://github.com/Kryptos-FR/MarkView.Avalonia/actions/workflows/ci.yml/badge.svg)](https://github.com/Kryptos-FR/MarkView.Avalonia/actions/workflows/ci.yml)

![Markview.Avalonia logo](https://raw.githubusercontent.com/Kryptos-FR/MarkView.Avalonia/main/docs/img/markview-logo.svg)

A [Markdig](https://github.com/xoofx/markdig)-powered markdown viewer control for [Avalonia UI](https://avaloniaui.net) v12. Drop `MarkdownViewer` into any Avalonia window or panel to render rich markdown — headings, code blocks, tables, task lists, links, images, and more — using native Avalonia controls with a fully customisable theme.

## Installation

```bash
dotnet add package MarkView.Avalonia
```

## Quick Start

Include the default theme and add `MarkdownViewer` to your XAML:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:mv="using:MarkView.Avalonia">
  <Window.Styles>
    <StyleInclude Source="avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml" />
  </Window.Styles>

  <mv:MarkdownViewer Markdown="# Hello, MarkView!" />
</Window>
```

### Setting a base URI for relative links and images

```csharp
viewer.BaseUri = new Uri("https://raw.githubusercontent.com/Kryptos-FR/MarkView.Avalonia/main/");
viewer.Markdown = markdownText; // relative image paths resolved against BaseUri
```

### Handling link clicks

External links open automatically via the platform's launcher (OS browser on desktop,
an intent on mobile, a new tab on WebAssembly) — no code required. `LinkClicked` is an
Avalonia **routed event** (bubbles up), raised before that default launch happens;
subscribe to override it for specific links by setting `e.Handled = true`:

```csharp
viewer.LinkClicked += (_, e) =>
{
    // e.Url contains the clicked URL
    if (e.Url.EndsWith(".md"))
    {
        viewer.Source = new Uri(e.Url); // render in-place instead of opening externally
        e.Handled = true;
    }
};
```

`LinkClickedEvent` is an Avalonia **routed event** (bubbles up) — subscribe globally once at app startup to handle all viewers:

```csharp
// App.axaml.cs — applies to every MarkdownViewer in the application
MarkdownViewer.LinkClickedEvent.AddClassHandler<MarkdownViewer>((_, e) =>
{
    // ...
});
```

### Interactive task lists

Set `AreTaskListsInteractive` to make `- [ ]` / `- [x]` items clickable (off by default, so
read-only viewers keep the static ☐/☑ glyph). The viewer only toggles the checkbox; it never edits
its own markdown — handle `TaskToggled` to write the change back to your source:

```csharp
viewer.AreTaskListsInteractive = true;
viewer.TaskToggled += (_, e) =>
{
    // e.SourceStart / e.SourceLength locate the "[ ]" or "[x]" marker in viewer.Markdown
    var source = viewer.Markdown!;
    viewer.Markdown = string.Concat(
        source.AsSpan(0, e.SourceStart),
        e.IsChecked ? "[x]" : "[ ]",
        source.AsSpan(e.SourceStart + e.SourceLength));
};
```

See [Interactive task lists](docs/configuration.md#interactive-task-lists) for details.

### Navigating to an anchor

```csharp
viewer.ScrollToAnchor("my-heading");  // scrolls to the heading's anchor position
```

### Code block toolbar

Code blocks show their language and a copy button above the code. Turn either off per viewer or
for the whole application with a style:

```xml
<mv:MarkdownViewer ShowCodeBlockLanguage="False" ShowCodeBlockCopyButton="False" />
```

See [Code block toolbar](docs/configuration.md#code-block-toolbar) for details.

### Using a custom Markdig pipeline

```csharp
viewer.Pipeline = new MarkdownPipelineBuilder()
    .UseSupportedExtensions()
    .UseAlertBlocks()
    .UseFootnotes()
    .Build();
```

## Extension Methods

Convenience methods configure a pipeline with a single opt-in feature:

```csharp
viewer.UseAbbreviations();
viewer.UseAlertBlocks();
viewer.UseCitations();
viewer.UseFigures();
viewer.UseFootnotes();
viewer.UseHardlineBreaks();
viewer.UseMediaLinks();
```

To combine several opt-in features, build the pipeline explicitly (see above).

## Application-Wide Defaults

`MarkdownViewerDefaults` lets you set a pipeline and/or extensions once so that every `MarkdownViewer` in the application inherits them automatically — no need to configure each instance:

```csharp
// App.axaml.cs
MarkdownViewerDefaults.Pipeline = new MarkdownPipelineBuilder()
    .UseSupportedExtensions()
    .UseFootnotes()
    .UseAlertBlocks()
    .Build();

MarkdownViewerDefaults.Extensions.AddTextMateHighlighting();
MarkdownViewerDefaults.Extensions.AddSvg();
MarkdownViewerDefaults.Extensions.AddMermaid();
```

Per-instance `Pipeline` and `Extensions` take precedence over the defaults; an extension object shared between the defaults list and an instance list is only registered once per render.

## Supported Markdown Features

### CommonMark baseline (always on)

| Feature | Notes |
|---------|-------|
| Headings H1–H6 | CommonMark |
| Bold, italic | CommonMark |
| Inline code | CommonMark |
| Fenced code blocks | CommonMark |
| Blockquotes | CommonMark |
| Ordered and unordered lists | CommonMark, tight and loose |
| Links and autolinks | CommonMark + extension |
| Images | CommonMark, remote URLs loaded async |
| Image sizing | `![alt](url =WxH)` — inline pixel dimensions, e.g. `=80x80` |
| Thematic breaks | CommonMark |
| Hard line breaks | CommonMark (`\` or two spaces) |
| HTML `<br>` / `<br />` | Rendered as line break |

### Extensions (enabled by `UseSupportedExtensions()`)

| Feature | Syntax |
|---------|--------|
| Strikethrough | `~~text~~` |
| Subscript | `~text~` |
| Superscript | `^text^` |
| Underline (inserted) | `++text++` |
| Highlight (marked) | `==text==` |
| Task lists | `- [x] item` (clickable with `AreTaskListsInteractive`) |
| Pipe tables | GFM-style `\| col \| col \|` |
| Grid tables | RST-style grid tables |
| Autolinks | bare `https://…` URLs |
| Emoji shortcodes | `:rocket:` → 🚀 (ASCII smileys like `:)` are intentionally left as plain text) |
| CJK-friendly emphasis | parser-only fix for emphasis next to Chinese/Japanese/Korean (CJK) punctuation |
| YAML front matter | `---` metadata block at the top of a document is parsed and hidden |

### Opt-in extensions

These require adding `.UseXxx()` to the pipeline (see [Extension Methods](#extension-methods) above):

| Feature | Activation | Syntax |
|---------|-----------|--------|
| Abbreviations | `UseAbbreviations()` | `*[HTML]: …` |
| GitHub alert blocks | `UseAlertBlocks()` | `> [!NOTE]` etc. |
| Foldable alerts and custom titles | `UseExtendedAlertBlocks()` (also enabled by `viewer.UseAlertBlocks()`) | Obsidian-style `> [!NOTE] Custom title`, foldable `> [!NOTE]-` (collapsed) / `> [!NOTE]+` (expanded) |
| Citations | `UseCitations()` | `""quoted text""` |
| Figures | `UseFigures()` | `^^^` / `^^^ caption` |
| Footnotes | `UseFootnotes()` | `[^1]` / `[^1]: …` |
| Hardline breaks | `UseHardlineBreaks()` | every soft line break renders as a hard break |
| YouTube embeds | `UseMediaLinks()` | `![title](https://youtu.be/…)` |
| Definition lists | `UseDefinitionLists()` | `Term` followed by `:   definition` |
| Custom containers | `UseCustomContainers()` | `:::name` … `:::` blocks, `::text::` inline |
| Footers | `UseFooters()` | `^^ footer text` |
| Smart punctuation | `UseSmartyPants()` | `"quotes"`, `--`, `---`, `...`, `<<…>>` become “ ” – — … « » |
| Letter and roman numeral lists | `UseListExtras()` | `a.`, `A.`, `i.`, `I.` |
| Style classes from attributes | `UseGenericAttributes()` (call it last) | `{.name}` adds `name` to the element's style classes |

The last six are Markdig's own pipeline methods; MarkView.Avalonia renders what they parse.

## Extension Packages

MarkView.Avalonia ships optional NuGet packages that add richer rendering capabilities. Each package implements `IMarkViewExtension` and is activated via a convenience method on `MarkdownViewer`, or globally via `MarkdownViewerDefaults.Extensions.AddXxx()`.

### Syntax Highlighting (`MarkView.Avalonia.SyntaxHighlighting`)

Adds TextMate grammar-based syntax highlighting to fenced code blocks.

```bash
dotnet add package MarkView.Avalonia.SyntaxHighlighting
```

```csharp
viewer.UseTextMateHighlighting(); // DarkPlus / LightPlus by default
// or with custom themes:
viewer.UseTextMateHighlighting(darkTheme: ThemeName.Monokai, lightTheme: ThemeName.QuietLight);
```

The extension replaces the built-in `CodeBlockRenderer` with `TextMateCodeBlockRenderer`, which tokenises each line and emits coloured `Run` elements. Unsupported languages fall back to the default monochrome rendering automatically.

Colours update in-place when the user switches between light and dark themes — no document rebuild or scroll reset.

Available `ThemeName` values are defined by TextMateSharp.Grammars: `DarkPlus`, `LightPlus`, `Monokai`, `SolarizedDark`, `SolarizedLight`, and more.

### SVG Images (`MarkView.Avalonia.Svg`)

Renders SVG images embedded in markdown (`![desc](path/to/image.svg)`), including `data:image/svg+xml` data URIs and badge-service URLs that return SVG without a `.svg` extension.

```bash
dotnet add package MarkView.Avalonia.Svg
```

```csharp
viewer.UseSvg();
```

The extension inserts `SvgImageLoader` at the front of the image loader chain. Regular raster images continue to load via the built-in HTTP fallback.

### LaTeX Math (`MarkView.Avalonia.Math`)

Renders `$...$` (inline) and `$$...$$` (block) LaTeX math using CSharpMath's SkiaSharp renderer — pure .NET, no browser or WebView required.

```bash
dotnet add package MarkView.Avalonia.Math
```

```csharp
viewer.UseMath();
```

### Mermaid Diagrams (`MarkView.Avalonia.Mermaid`)

Renders fenced `mermaid` code blocks as SVG diagrams using the [Mermaider](https://github.com/nullean/mermaider) library (pure .NET, no browser required). Works on all platforms including Linux. Diagrams re-render automatically when the user switches between light and dark themes.

```bash
dotnet add package MarkView.Avalonia.Mermaid
```

```csharp
viewer.UseMermaid();
```

Markdown syntax:

````markdown
```mermaid
graph TD
  A[Start] --> B{Decision}
  B -- Yes --> C[End]
  B -- No  --> A
```
````

### Combining extensions

All four can be stacked:

```csharp
viewer
    .UseTextMateHighlighting()
    .UseSvg()
    .UseMermaid()
    .UseMath();
```

Extensions are applied in the order they are added to `viewer.Extensions`. Each extension's `Register` method is called once per render pass, before the Markdig pipeline is set up.

### Writing your own extension

Implement `IMarkViewExtension` from the core package:

```csharp
using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;

public class MyExtension : IMarkViewExtension
{
    public void Register(AvaloniaRenderer renderer)
    {
        // swap a renderer, add an image loader, or set a code highlighter
        renderer.ObjectRenderers.ReplaceOrAdd<CodeBlockRenderer>(new MyCodeBlockRenderer());
    }
}

viewer.Extensions.Add(new MyExtension());
```

## Theming / Customisation

Include `MarkdownTheme.axaml` for default styles. Override any style class in your own `Styles` to customise appearance:

| Style class | Applied to | Controls |
|-------------|------------|----------|
| `markdown-h1` … `markdown-h6` | Headings | `TextBlock` |
| `markdown-paragraph` | Paragraphs | `TextBlock` |
| `markdown-code-block` | Code blocks | `Border` |
| `markdown-code-container` | Code block with its toolbar | `StackPanel` |
| `markdown-code-header` | Code block toolbar | `Border` |
| `markdown-code-language` | Code block language label | `TextBlock` |
| `markdown-code-copy` | Code block copy button | `Button` |
| `markdown-code-inline` | Inline code | `Border` |
| `markdown-blockquote` | Blockquotes | `Border` |
| `markdown-list` | Lists | `StackPanel` |
| `markdown-list-marker` | List item bullet, number or task-list glyph | `TextBlock` |
| `markdown-task-list` | Task-list check glyph (☐/☑) | `TextBlock` |
| `markdown-task-toggle` | Clickable task-list checkbox (`AreTaskListsInteractive`) | `ToggleButton` |
| `markdown-thematic-break` | Horizontal rules | `Separator` |
| `markdown-image` | Images | `Image` |
| `markdown-link` | Hyperlinks | `HyperlinkButton` |
| `markdown-table` | Tables | `Grid` |
| `markdown-table-cell` | Table cells | `Border` |
| `markdown-table-header` | Header cells | `Border` |
| `markdown-marked` | Highlighted text (`==…==`) | `Span` |
| `markdown-alert` | Alert block container | `Border` |
| `markdown-alert-note` … `markdown-alert-caution` | Per-kind border colour | `Border` |
| `markdown-alert-header` | Alert kind label or custom title | `TextBlock` |
| `markdown-alert-collapsible` | Foldable alert (`:expanded` while open) | `Border` |
| `markdown-alert-chevron` | Fold indicator in a foldable alert's header | `Run` |
| `markdown-figure` | Figure container | `Border` |
| `markdown-figure-caption` | Figure caption | `TextBlock` |
| `markdown-abbr` | Abbreviation with tooltip | `TextBlock` |
| `markdown-footnote-ref` | Footnote reference link | `HyperlinkButton` |
| `markdown-footnote-group` | Footnote definition list | `StackPanel` |
| `markdown-footnote-item` | Individual footnote row | `Grid` |
| `markdown-definition-list` | Definition list | `StackPanel` |
| `markdown-definition-term` | Defined term | `TextBlock` |
| `markdown-definition` | Definition (indented) | `StackPanel` |
| `markdown-custom-container` | Custom container (`:::name`, also gets `name` as a class) | `Border` |
| `markdown-custom-container-title` | Custom container title | `TextBlock` |
| `markdown-custom-inline` | Inline custom container (`::text::`) | `Span` |
| `markdown-footer` | Footer (`^^`) | `StackPanel` |

Extension packages may ship their own theme file with additional overridable colours — check the extension's README for its `StyleInclude` path and resource keys.

Example — increase heading size and add a bottom border:

```xml
<Style Selector="TextBlock.markdown-h1">
  <Setter Property="FontSize" Value="36" />
  <Setter Property="Foreground" Value="#1A1A2E" />
  <Setter Property="Margin" Value="0,12,0,6" />
</Style>
```

## Text Selection

`MarkdownViewer` supports full document-wide text selection:

- **Click + drag** to select a range.
- **`Ctrl+A`** to select all text.
- **`Ctrl+C`** to copy the selection to the clipboard.

Programmatic API:

```csharp
viewer.SelectAll();
viewer.ClearSelection();
string text = viewer.GetSelectedText();
await viewer.CopyToClipboardAsync();
```

A selection that spans an image copies the image's alt text in its place; math formulas and Mermaid diagrams copy their source. See [Text Selection](docs/text-selection.md) for details.

## Known Limitations

| Limitation | Detail |
|---|---|
| Anchor scroll is instant | `BringIntoView()` jumps without animation. Smooth scrolling is a future improvement. |

## License

[MIT](LICENSE.md) © Nicolas Musset
