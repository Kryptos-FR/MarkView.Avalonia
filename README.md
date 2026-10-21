# MarkView.Avalonia

[![NuGet Version](https://img.shields.io/nuget/v/MarkView.Avalonia)](https://www.nuget.org/packages/MarkView.Avalonia)
[![NuGet Downloads](https://img.shields.io/nuget/dt/MarkView.Avalonia)](https://www.nuget.org/packages/MarkView.Avalonia)
[![Avalonia](https://img.shields.io/badge/Avalonia-12-blue)](https://avaloniaui.net)
[![License](https://img.shields.io/github/license/Kryptos-FR/MarkView.Avalonia)](https://github.com/Kryptos-FR/MarkView.Avalonia/blob/main/LICENSE.md)
[![CI](https://github.com/Kryptos-FR/MarkView.Avalonia/actions/workflows/ci.yml/badge.svg)](https://github.com/Kryptos-FR/MarkView.Avalonia/actions/workflows/ci.yml)

![Markview.Avalonia logo](https://raw.githubusercontent.com/Kryptos-FR/MarkView.Avalonia/main/docs/img/markview-logo.svg)

A [Markdig](https://github.com/xoofx/markdig)-powered markdown viewer control for [Avalonia UI](https://avaloniaui.net) v12. Drop `MarkdownViewer` into any Avalonia window or panel to render rich markdown — headings, code blocks, tables, task lists, links, images, and more — using native Avalonia controls with a fully customisable theme.

Documentation: **[kryptos-fr.github.io/MarkView.Avalonia](https://kryptos-fr.github.io/MarkView.Avalonia/)** · Try it in your browser: **[demo](https://kryptos-fr.github.io/MarkView.Avalonia/demo/)**

## Installation

```bash
dotnet add package MarkView.Avalonia
```

The package targets .NET 10 and Avalonia 12.

## Quick start

Include the theme in `App.axaml`, after Avalonia's own theme. It holds the viewer's template, which makes the document scroll:

```xml
<Application.Styles>
  <FluentTheme />
  <StyleInclude Source="avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml" />
</Application.Styles>
```

Then put a `MarkdownViewer` in a window, and give it a string or a file:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:mv="using:MarkView.Avalonia">
  <mv:MarkdownViewer Markdown="# Hello, MarkView!" />
  <!-- or Source="avares://MyApp/Docs/readme.md" -->
</Window>
```

Stream a language model's answer as it arrives:

```csharp
viewer.Markdown = "";
using var session = viewer.BeginStreaming();
foreach (var token in tokens)
    session.Append(token);
```

See [Getting started](https://kryptos-fr.github.io/MarkView.Avalonia/getting-started/) for the next steps.

## Features

- **GitHub-flavoured Markdown** out of the box: tables, task lists, alerts, footnotes, `<details>` and the HTML subset that READMEs use, plus Markdig's opt-in syntax. [Markdown syntax](https://kryptos-fr.github.io/MarkView.Avalonia/reference/markdown-syntax/)
- **Built for AI chat**: [streaming](https://kryptos-fr.github.io/MarkView.Avalonia/guides/streaming/) that re-renders only the end of the document and closes unfinished syntax, foldable [thinking blocks](https://kryptos-fr.github.io/MarkView.Avalonia/guides/chat/#thinking-blocks), and a compact chat preset. [Chat and AI answers](https://kryptos-fr.github.io/MarkView.Avalonia/guides/chat/)
- **Code blocks** with a copy button, language label, line numbers, wrapping and TextMate syntax highlighting, or your own control for any fence language. [Code blocks](https://kryptos-fr.github.io/MarkView.Avalonia/guides/code-blocks/)
- **Images and diagrams**: sizing, a zoomable lightbox, saving, an image cache, SVG and Mermaid diagrams, all in .NET. [Images and diagrams](https://kryptos-fr.github.io/MarkView.Avalonia/guides/images/)
- **Math**: `$…$` and `$$…$$` LaTeX formulas. [Math](https://kryptos-fr.github.io/MarkView.Avalonia/guides/math/)
- **Themes**: GitHub, Chat and Docs presets, light and dark switching live, and every colour and size a resource you can redefine. [Themes and presets](https://kryptos-fr.github.io/MarkView.Avalonia/guides/themes/)
- **Selection, copy and find** across the whole document, with copy as Markdown and as HTML. [Selection and copy](https://kryptos-fr.github.io/MarkView.Avalonia/guides/text-selection/) · [Find in document](https://kryptos-fr.github.io/MarkView.Avalonia/guides/find-in-document/)
- **Navigation**: heading anchors, a table of contents, foldable headings and interactive task lists. [Displaying documents](https://kryptos-fr.github.io/MarkView.Avalonia/guides/displaying-documents/)
- **Accessible**: keyboard navigation, screen reader roles, WCAG AA colours, a high-contrast palette and right-to-left text. [Accessibility](https://kryptos-fr.github.io/MarkView.Avalonia/guides/accessibility/)
- **Lazy rendering**: images, formulas, diagrams and code colours are built as they come near the visible area. [Lazy rendering](https://kryptos-fr.github.io/MarkView.Avalonia/guides/displaying-documents/#lazy-rendering)
- **Extensible**: renderers, image loaders and code highlighters are extension points. [Custom extensions](https://kryptos-fr.github.io/MarkView.Avalonia/guides/custom-extensions/)

## Packages

All five packages are released together under the same version.

| Package | Adds | Enable with |
|---------|------|-------------|
| [MarkView.Avalonia](https://www.nuget.org/packages/MarkView.Avalonia) | The `MarkdownViewer` control and its theme | |
| [MarkView.Avalonia.SyntaxHighlighting](https://www.nuget.org/packages/MarkView.Avalonia.SyntaxHighlighting) | TextMate syntax highlighting of code blocks | `UseTextMateHighlighting()` |
| [MarkView.Avalonia.Svg](https://www.nuget.org/packages/MarkView.Avalonia.Svg) | SVG images | `UseSvg()` |
| [MarkView.Avalonia.Mermaid](https://www.nuget.org/packages/MarkView.Avalonia.Mermaid) | Mermaid diagrams, rendered in .NET | `UseMermaid()` |
| [MarkView.Avalonia.Math](https://www.nuget.org/packages/MarkView.Avalonia.Math) | LaTeX math | `UseMath()` |

Enable an extension on one viewer (`viewer.UseMermaid()`), or on every viewer of the application at startup (`MarkdownViewerDefaults.Extensions.AddMermaid()`). See [Choosing packages](https://kryptos-fr.github.io/MarkView.Avalonia/packages/).

## Versioning

Versions read `12.MAJOR.MINOR`: `12` is the Avalonia version, the second number changes with breaking changes, and the third with features and fixes. To receive only compatible updates, use a floating version such as `12.4.*`. See [Versioning](https://kryptos-fr.github.io/MarkView.Avalonia/reference/versioning/) and, when upgrading from 12.3, [Upgrading to 12.4](https://kryptos-fr.github.io/MarkView.Avalonia/upgrading/).

## Contributing

Issues and pull requests are welcome. The [demo app](https://github.com/Kryptos-FR/MarkView.Avalonia/tree/main/samples) shows every feature, and [CLAUDE.md](https://github.com/Kryptos-FR/MarkView.Avalonia/blob/main/CLAUDE.md) describes how to build and test the repository and how the code is organised.

## License

[MIT](https://github.com/Kryptos-FR/MarkView.Avalonia/blob/main/LICENSE.md) © Nicolas Musset
