# MarkView.Avalonia.Math

[![NuGet Version](https://img.shields.io/nuget/v/MarkView.Avalonia.Math)](https://www.nuget.org/packages/MarkView.Avalonia.Math)
[![NuGet Downloads](https://img.shields.io/nuget/dt/MarkView.Avalonia.Math)](https://www.nuget.org/packages/MarkView.Avalonia.Math)
[![Avalonia](https://img.shields.io/badge/Avalonia-12-blue)](https://avaloniaui.net)
[![CI](https://github.com/Kryptos-FR/MarkView.Avalonia/actions/workflows/ci.yml/badge.svg)](https://github.com/Kryptos-FR/MarkView.Avalonia/actions/workflows/ci.yml)
[![License](https://img.shields.io/github/license/Kryptos-FR/MarkView.Avalonia)](../../LICENSE.md)

LaTeX math rendering extension for [MarkView.Avalonia](https://www.nuget.org/packages/MarkView.Avalonia). Renders `$...$` and `\(...\)` (inline) and `$$...$$` and `\[...\]` (display) math using [CSharpMath](https://github.com/verybadcat/CSharpMath)'s SkiaSharp renderer — pure .NET, no browser, no WebView, no JavaScript runtime.

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
    .UseGitHubExtensions()
    .UseMathematics()
    .UseMathBracketDelimiters()
    .Build();
```

Unlike most opt-in extensions in this repo, math needs both a pipeline change (`.UseMathematics()`
and `.UseMathBracketDelimiters()`, so the delimiters are even parsed) and a renderer registration —
`UseMath()` does both for you.

```markdown
Inline: Einstein's famous equation is $E = mc^2$.

Block:

$$
\left( \sum_{k=1}^n a_k b_k \right)^2 \leq \left( \sum_{k=1}^n a_k^2 \right) \left( \sum_{k=1}^n b_k^2 \right)
$$
```

## LaTeX Bracket Delimiters

LLMs and LaTeX sources often write math as `\(...\)` (inline) and `\[...\]` (display) rather than with
dollars. `UseMath()` enables these too, through `UseMathBracketDelimiters()`, and renders them exactly
like their dollar counterparts:

```markdown
The roots of \(ax^2 + bx + c = 0\) are
\[
x = \frac{-b \pm \sqrt{b^2 - 4ac}}{2a}
\]
for any \(a \neq 0\).
```

- `\(...\)` stays on one line, like `$...$`.
- `\[...\]` may span several lines. When it has its lines to itself, it renders as a block (even
  directly under a line of text, with no blank line in between); when it shares a line with text,
  it renders inline, like `$$...$$` within a paragraph.
- Code spans and code blocks are left alone.

In CommonMark, `\(` and `\[` are an escaped parenthesis and bracket, so with the bracket delimiters on,
text such as `\(see below\)` becomes a formula. They keep their CommonMark meaning when the opening
is glued to a letter or digit (`arr\[i\]`, `f\(x\)`), is never closed, or encloses nothing (`\(\)`).
A formula's lines are still read as a paragraph first: a line inside `\[...\]` that starts like a list
item (`- `, `+ `, `1. `) or a heading ends the paragraph, as does a blank line (which LaTeX does not
allow in display math either), leaving the `\[` unclosed, so the source shows as text.

To keep only the dollar delimiters (and the CommonMark escapes):

```csharp
viewer.UseMath(bracketDelimiters: false);
```

The bracket delimiters can also be used without the dollar ones, for documents where `$` is
common in prose: build the pipeline with `.UseMathBracketDelimiters()` and no `.UseMathematics()`.

## Theme Awareness

Formulas are rendered with colours matching the active Avalonia theme variant (dark `#FAFAFA`,
light `#27272A` text), and are automatically re-rendered when the user switches between light and
dark.

## Combining with Mermaid

`MathExtension` and `MermaidExtension` can both be registered in either order — each scans for
(rather than assumes) its position in the renderer list, so `$$...$$` blocks are never mistaken
for a Mermaid diagram or a plain code block regardless of registration order. `\[...\]` blocks are
math blocks too, so the same applies to them.

## Error Handling

Invalid LaTeX renders CSharpMath's own inline error text rather than throwing. Unexpected
rendering failures fall back to a plain-text panel (block math) or leave the last successfully
rendered formula in place (inline math).

## License

[MIT](../../LICENSE.md) © Nicolas Musset
