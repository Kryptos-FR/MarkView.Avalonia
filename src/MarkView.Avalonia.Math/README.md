# MarkView.Avalonia.Math

[![NuGet Version](https://img.shields.io/nuget/v/MarkView.Avalonia.Math)](https://www.nuget.org/packages/MarkView.Avalonia.Math)
[![NuGet Downloads](https://img.shields.io/nuget/dt/MarkView.Avalonia.Math)](https://www.nuget.org/packages/MarkView.Avalonia.Math)
[![Avalonia](https://img.shields.io/badge/Avalonia-12-blue)](https://avaloniaui.net)
[![CI](https://github.com/Kryptos-FR/MarkView.Avalonia/actions/workflows/ci.yml/badge.svg)](https://github.com/Kryptos-FR/MarkView.Avalonia/actions/workflows/ci.yml)
[![License](https://img.shields.io/github/license/Kryptos-FR/MarkView.Avalonia)](../../LICENSE.md)

LaTeX math rendering extension for [MarkView.Avalonia](https://www.nuget.org/packages/MarkView.Avalonia). Renders `$...$` (inline) and `$$...$$` (block) math using [CSharpMath](https://github.com/verybadcat/CSharpMath)'s SkiaSharp renderer — pure .NET, no browser, no WebView, no JavaScript runtime.

Because `$` is common in ordinary prose (currency amounts, etc.), this parsing is opt-in only — enabling it changes how literal `$` characters are interpreted in your documents.

## Installation

```bash
dotnet add package MarkView.Avalonia.Math
```

## Quick Start

Call `UseMath()` before setting `Markdown`:

```csharp
var viewer = new MarkdownViewer();
viewer.UseMath();
viewer.Markdown = markdownText;
```

Or activate globally at application startup:

```csharp
// App.axaml.cs
MarkdownViewerDefaults.Extensions.AddMath();
MarkdownViewerDefaults.Pipeline = new MarkdownPipelineBuilder()
    .UseSupportedExtensions()
    .UseMathematics()
    .Build();
```

Unlike most opt-in extensions in this repo, math needs both a pipeline change (`.UseMathematics()`,
so `$`/`$$` are even parsed) and a renderer registration — `UseMath()` does both for you.

```markdown
Inline: Einstein's famous equation is $E = mc^2$.

Block:

$$
\left( \sum_{k=1}^n a_k b_k \right)^2 \leq \left( \sum_{k=1}^n a_k^2 \right) \left( \sum_{k=1}^n b_k^2 \right)
$$
```

## Theme Awareness

Formulas are rendered with colours matching the active Avalonia theme variant (dark `#FAFAFA`,
light `#27272A` text), and are automatically re-rendered when the user switches between light and
dark.

## Combining with Mermaid

`MathExtension` and `MermaidExtension` can both be registered in either order — each scans for
(rather than assumes) its position in the renderer list, so `$$...$$` blocks are never mistaken
for a Mermaid diagram or a plain code block regardless of registration order.

## Error Handling

Invalid LaTeX renders CSharpMath's own inline error text rather than throwing. Unexpected
rendering failures fall back to a plain-text panel (block math) or leave the last successfully
rendered formula in place (inline math).

## License

[MIT](../../LICENSE.md) © Nicolas Musset
