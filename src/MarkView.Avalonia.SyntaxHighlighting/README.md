# MarkView.Avalonia.SyntaxHighlighting

[![NuGet Version](https://img.shields.io/nuget/v/MarkView.Avalonia.SyntaxHighlighting)](https://www.nuget.org/packages/MarkView.Avalonia.SyntaxHighlighting)
[![NuGet Downloads](https://img.shields.io/nuget/dt/MarkView.Avalonia.SyntaxHighlighting)](https://www.nuget.org/packages/MarkView.Avalonia.SyntaxHighlighting)
[![Avalonia](https://img.shields.io/badge/Avalonia-12-blue)](https://avaloniaui.net)
[![CI](https://github.com/Kryptos-FR/MarkView.Avalonia/actions/workflows/ci.yml/badge.svg)](https://github.com/Kryptos-FR/MarkView.Avalonia/actions/workflows/ci.yml)
[![License](https://img.shields.io/github/license/Kryptos-FR/MarkView.Avalonia)](../../LICENSE.md)

TextMate grammar-based syntax highlighting for [MarkView.Avalonia](https://www.nuget.org/packages/MarkView.Avalonia) fenced code blocks. Colours update in-place when the user switches between light and dark themes — no document rebuild or scroll reset.

## Installation

```bash
dotnet add package MarkView.Avalonia.SyntaxHighlighting
```

## Quick Start

Call `UseTextMateHighlighting()` before setting `Markdown`:

```csharp
var viewer = new MarkdownViewer();
viewer.UseTextMateHighlighting();
viewer.Markdown = markdownText;
```

Or activate globally at application startup so every `MarkdownViewer` in the app gets highlighting automatically:

```csharp
// App.axaml.cs
MarkdownViewerDefaults.Extensions.AddTextMateHighlighting();
```

By default, `DarkPlus` is used for dark themes and `LightPlus` for light themes. Both highlighters are created lazily — only the one matching the active variant is loaded at startup.

## Theme Selection

Pass explicit `ThemeName` values to override the defaults:

```csharp
using TextMateSharp.Grammars;

viewer.UseTextMateHighlighting(
    darkTheme:  ThemeName.Monokai,
    lightTheme: ThemeName.QuietLight);
```

Available themes (from `TextMateSharp.Grammars.ThemeName`):

| Dark | Light |
|------|-------|
| `DarkPlus` | `LightPlus` |
| `Monokai` | `QuietLight` |
| `SolarizedDark` | `SolarizedLight` |
| `TomorrowNightBlue` | `Abyss` |
| `HighContrastLight` | `HighContrastLight` |
| `KimbieDark` | — |

## How It Works

`UseTextMateHighlighting()` registers a `TextMateExtension` which:

1. Creates a `DualThemeTextMateHighlighter` wrapping two `TextMateHighlighter` instances (one per variant).
2. Replaces the built-in `CodeBlockRenderer` with `TextMateCodeBlockRenderer`.

At render time, each line is tokenised via `IGrammar.TokenizeLine` and emitted as coloured `Run` elements inside a `TextBlock`. Grammars are cached per language per highlighter instance.

When the user switches between light and dark themes, only `TextBlock.Inlines` is rebuilt — the surrounding `Border` and the rest of the document are untouched. Images stay loaded, scroll position is preserved, and Mermaid diagrams are not retriggered.

Languages not supported by the grammar registry fall back to the default monochrome rendering automatically.

## Low-Level API

Use `TextMateExtension` directly for full control:

```csharp
using MarkView.Avalonia.SyntaxHighlighting;
using MarkView.Avalonia.Rendering;
using TextMateSharp.Grammars;

var renderer = new AvaloniaRenderer();
new TextMateExtension(ThemeName.Monokai, ThemeName.QuietLight).Register(renderer);
```

Or supply a `TextMateHighlighter` as a standalone `ICodeHighlighter` (for use outside `MarkdownViewer`):

```csharp
var highlighter = new TextMateHighlighter(ThemeName.DarkPlus);
var tokens = highlighter.Highlight("var x = 1;", "csharp");
```

## Implementing a Custom Highlighter

Any `ICodeHighlighter` from the core package can be used instead:

```csharp
using Avalonia.Media;
using MarkView.Avalonia.Extensions;

public class MyHighlighter : ICodeHighlighter
{
    public IReadOnlyList<(string Text, IBrush? Foreground)>? Highlight(
        ReadOnlyMemory<char> line, string? language)
    {
        // return null to fall back to monochrome rendering
        return null;
    }
}

renderer.CodeHighlighter = new MyHighlighter();
```

For automatic dark/light updates without a document rebuild, implement `IThemeAwareCodeHighlighter` instead:

```csharp
public class MyHighlighter : IThemeAwareCodeHighlighter
{
    public IReadOnlyList<(string Text, IBrush? Foreground)>? Highlight(
        ReadOnlyMemory<char> line, string? language)
        => HighlightVariant(line, language, isDark: false);

    public IReadOnlyList<(string Text, IBrush? Foreground)>? HighlightVariant(
        ReadOnlyMemory<char> line, string? language, bool isDark)
    {
        // return coloured tokens for the requested variant
        return null;
    }
}
```

## License

[MIT](../../LICENSE.md) © Nicolas Musset
