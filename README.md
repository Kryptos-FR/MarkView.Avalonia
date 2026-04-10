# MarkView.Avalonia

[![NuGet Version](https://img.shields.io/nuget/v/MarkView.Avalonia)](https://www.nuget.org/packages/MarkView.Avalonia)
[![NuGet Downloads](https://img.shields.io/nuget/dt/MarkView.Avalonia)](https://www.nuget.org/packages/MarkView.Avalonia)
[![Avalonia](https://img.shields.io/badge/Avalonia-12-blue)](https://avaloniaui.net)
[![License](https://img.shields.io/github/license/Kryptos-FR/MarkView.Avalonia)](LICENSE.md)
[![CI](https://github.com/Kryptos-FR/MarkView.Avalonia/actions/workflows/ci.yml/badge.svg)](https://github.com/Kryptos-FR/MarkView.Avalonia/actions/workflows/ci.yml)

A [Markdig](https://github.com/xoofx/markdig)-powered markdown viewer control for [Avalonia UI](https://avaloniaui.net) v12. Drop `MarkdownViewer` into any Avalonia window or panel to render rich markdown — headings, code blocks, tables, task lists, links, images, and more — using native Avalonia controls with a fully customizable theme.

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

To handle link clicks in code-behind:

```csharp
viewer.LinkClicked += (_, e) =>
{
    // e.Url contains the clicked URL
    Process.Start(new ProcessStartInfo(e.Url) { UseShellExecute = true });
};
```

To use a custom Markdig pipeline:

```csharp
viewer.Pipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .Build();
```

## Supported Markdown Features

| Feature | Notes |
|---------|-------|
| Headings H1–H6 | CommonMark |
| Bold, italic | CommonMark |
| Strikethrough | Extension (`~~text~~`) |
| Inline code | CommonMark |
| Fenced code blocks | CommonMark |
| Blockquotes | CommonMark |
| Ordered and unordered lists | CommonMark, tight and loose |
| Task lists | Extension (`- [x] item`) |
| Pipe tables | Extension |
| Links and autolinks | CommonMark + extension |
| Images | CommonMark, remote URLs loaded async |
| Thematic breaks | CommonMark |
| Hard line breaks | CommonMark (`\` or two spaces) |
| HTML `<br>` / `<br />` | Rendered as line break |

## Extension Packages

MarkView.Avalonia ships optional NuGet packages that add richer rendering capabilities. Each package implements `IMarkViewExtension` and is activated via a convenience method on `MarkdownViewer`.

### Syntax Highlighting (`MarkView.Avalonia.SyntaxHighlighting`)

Adds TextMate grammar-based syntax highlighting to fenced code blocks.

```bash
dotnet add package MarkView.Avalonia.SyntaxHighlighting
```

```csharp
using TextMateSharp.Grammars;

viewer.UseTextMateHighlighting(ThemeName.DarkPlus); // default theme
```

The extension replaces the built-in `CodeBlockRenderer` with `TextMateCodeBlockRenderer`, which tokenises each line and emits coloured `Run` elements. Unsupported languages fall back to the default monochrome rendering automatically.

Available `ThemeName` values are defined by TextMateSharp.Grammars: `DarkPlus`, `LightPlus`, `Monokai`, `SolarizedDark`, `SolarizedLight`, and more.

### SVG Images (`MarkView.Avalonia.Svg`)

Renders SVG images embedded in markdown (`![desc](path/to/image.svg)`), including `data:image/svg+xml` data URIs.

```bash
dotnet add package MarkView.Avalonia.Svg
```

```csharp
viewer.UseSvg();
```

The extension inserts `SvgImageLoader` at the front of the image loader chain. Regular raster images continue to load via the built-in HTTP fallback.

### Mermaid Diagrams (`MarkView.Avalonia.Mermaid`)

Renders fenced `mermaid` code blocks as live diagrams using an embedded NativeWebView and bundled mermaid.min.js (v11). On Linux, a plain-text fallback is shown instead.

```bash
dotnet add package MarkView.Avalonia.Mermaid
```

```csharp
viewer.UseMermaid(initialHeight: 300); // height in device-independent pixels
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

All three can be stacked:

```csharp
viewer
    .UseTextMateHighlighting()
    .UseSvg()
    .UseMermaid();
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
        renderer.ReplaceOrAdd<CodeBlockRenderer>(new MyCodeBlockRenderer());
    }
}

viewer.Extensions.Add(new MyExtension());
```

## Theming / Customization

Include `MarkdownTheme.axaml` for default styles. Override any style class in your own `Styles` to customise appearance:

| Style class | Applied to | Controls |
|-------------|------------|----------|
| `markdown-h1` … `markdown-h6` | Headings | `TextBlock` |
| `markdown-paragraph` | Paragraphs | `TextBlock` |
| `markdown-code-block` | Code blocks | `Border` |
| `markdown-code-inline` | Inline code | `Border` |
| `markdown-blockquote` | Blockquotes | `Border` |
| `markdown-list` | Lists | `StackPanel` |
| `markdown-thematic-break` | Horizontal rules | `Separator` |
| `markdown-image` | Images | `Image` |
| `markdown-link` | Hyperlinks | `HyperlinkButton` |
| `markdown-table` | Tables | `Grid` |
| `markdown-table-cell` | Table cells | `Border` |
| `markdown-table-header` | Header cells | `Border` |

Example — increase heading size and add a bottom border:

```xml
<Style Selector="TextBlock.markdown-h1">
  <Setter Property="FontSize" Value="36" />
  <Setter Property="Foreground" Value="#1A1A2E" />
  <Setter Property="Margin" Value="0,12,0,6" />
</Style>
```

## Known Limitations

Text selection is scoped to individual blocks (paragraphs, headings). Selecting text across multiple blocks (e.g. from a heading into the next paragraph) is not supported in v1.

| Limitation | Detail |
|---|---|
| Within-block selection only | Each text block is an independent `SelectableTextBlock`. Cross-block selection requires a custom document container — see [Markdown.Avalonia's CTextBlock](https://github.com/whistyun/Markdown.Avalonia) for a geometry-based reference. |
| Images are non-selectable | Images in inline position are embedded as `InlineUIContainer` — selection skips around them. This is the same behaviour as all reference libraries. |
| Task checkboxes are non-selectable | Same reason as images. |
| Anchor scroll is instant | `BringIntoView()` jumps without animation. Smooth scrolling is a future improvement. |

## License

[MIT](LICENSE.md) © Nicolas Musset
