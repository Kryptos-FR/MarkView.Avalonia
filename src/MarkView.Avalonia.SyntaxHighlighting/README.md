# MarkView.Avalonia.SyntaxHighlighting

[![NuGet Version](https://img.shields.io/nuget/v/MarkView.Avalonia.SyntaxHighlighting)](https://www.nuget.org/packages/MarkView.Avalonia.SyntaxHighlighting)
[![NuGet Downloads](https://img.shields.io/nuget/dt/MarkView.Avalonia.SyntaxHighlighting)](https://www.nuget.org/packages/MarkView.Avalonia.SyntaxHighlighting)
[![Avalonia](https://img.shields.io/badge/Avalonia-12-blue)](https://avaloniaui.net)
[![CI](https://img.shields.io/github/actions/workflow/status/Kryptos-FR/MarkView.Avalonia/ci.yml?branch=main&label=CI)](https://github.com/Kryptos-FR/MarkView.Avalonia/actions/workflows/ci.yml)
[![License](https://img.shields.io/github/license/Kryptos-FR/MarkView.Avalonia)](https://github.com/Kryptos-FR/MarkView.Avalonia/blob/main/LICENSE.md)

Syntax highlighting of code blocks for [MarkView.Avalonia](https://www.nuget.org/packages/MarkView.Avalonia), with the TextMate grammars and themes that VS Code uses. Switching between light and dark only recolours the code: the document is not rebuilt, and the scroll position is kept.

## Installation

```bash
dotnet add package MarkView.Avalonia.SyntaxHighlighting
```

## Usage

```csharp
viewer.UseTextMateHighlighting();   // Dark+ and Light+

// other themes, from TextMateSharp.Grammars
viewer.UseTextMateHighlighting(darkTheme: ThemeName.Monokai, lightTheme: ThemeName.QuietLight);

// every viewer of the application, at startup
MarkdownViewerDefaults.Extensions.AddTextMateHighlighting();
```

Fenced code blocks with a language are coloured; a language without a grammar shows as plain code. Viewers that use the same pair of themes share one highlighter, so the grammars are loaded once however many viewers there are.

## Documentation

- [Syntax highlighting](https://kryptos-fr.github.io/MarkView.Avalonia/guides/code-blocks/#syntax-highlighting): themes, lazy colouring and highlighting outside the viewer.
- [Writing a custom code highlighter](https://kryptos-fr.github.io/MarkView.Avalonia/guides/custom-extensions/#writing-a-custom-code-highlighter): `ICodeHighlighter` and theme-aware highlighters.

The [documentation site](https://kryptos-fr.github.io/MarkView.Avalonia/) covers the whole library, and the [demo](https://kryptos-fr.github.io/MarkView.Avalonia/demo/) shows every feature in the browser.

## License

[MIT](https://github.com/Kryptos-FR/MarkView.Avalonia/blob/main/LICENSE.md) © Nicolas Musset
