---
title: "Choosing packages"
---

# Choosing packages

The core package renders Markdown on its own. Four optional packages add what needs a large dependency, so an application only ships what it uses. All five are released together under the same version.

| Package | Adds | Enable with |
|---|---|---|
| `MarkView.Avalonia` | The `MarkdownViewer` control, GitHub-flavoured Markdown, the theme and its presets | |
| `MarkView.Avalonia.SyntaxHighlighting` | Coloured code blocks, from TextMate grammars | `UseTextMateHighlighting()` |
| `MarkView.Avalonia.Svg` | SVG images, including badges that are SVG without a `.svg` address | `UseSvg()` |
| `MarkView.Avalonia.Mermaid` | Mermaid diagrams, drawn in .NET without a browser | `UseMermaid()` |
| `MarkView.Avalonia.Math` | LaTeX formulas: `$…$`, `$$…$$`, `\(…\)` and `\[…\]` | `UseMath()` |

```bash
dotnet add package MarkView.Avalonia.SyntaxHighlighting
dotnet add package MarkView.Avalonia.Svg
dotnet add package MarkView.Avalonia.Mermaid
dotnet add package MarkView.Avalonia.Math
```

Math is opt-in even once installed: `$` is common in ordinary text (`$5`), so a viewer only reads it as math after `UseMath()`.

## Enabling a package

Each package adds an extension to a viewer, or to every viewer of the application:

```csharp
// One viewer
viewer.UseTextMateHighlighting()
      .UseSvg()
      .UseMermaid()
      .UseMath();

// Every viewer, at startup
MarkdownViewerDefaults.Extensions.AddTextMateHighlighting();
MarkdownViewerDefaults.Extensions.AddSvg();
MarkdownViewerDefaults.Extensions.AddMermaid();
MarkdownViewerDefaults.Extensions.AddMath();
```

The order does not matter. Mermaid and math both take over fenced blocks, and each finds its place among the renderers already registered, so a `mermaid` fence is a diagram, a `$$` block a formula and any other fence highlighted code, whichever was added first. A [fence renderer](guides/code-blocks.md#custom-fenced-blocks) of your own wins over all of them for its language.

`AddMath()` registers the renderer only. A viewer's `UseMath()` also adds the math syntax to its pipeline, so with `AddMath()` add it to the default pipeline too, as shown in [Math](guides/math.md).

## Theme files

The Mermaid package has a theme file of its own for the diagrams' colours. Include it after the core theme to change them; without it, diagrams use the same colours built in:

```xml
<StyleInclude Source="avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml" />
<StyleInclude Source="avares://MarkView.Avalonia.Mermaid/Themes/MermaidTheme.axaml" />
```

The other packages need none: code colours come from the TextMate themes, and formulas are drawn in the text colour of the theme variant.

## What each package does

- [Code blocks](guides/code-blocks.md#syntax-highlighting): TextMate themes, and how colours follow the theme variant.
- [Images and diagrams](guides/images.md#svg-images): the addresses the SVG loader accepts, Mermaid's diagram styles and fonts.
- [Math](guides/math.md): delimiters, the copy button, and formulas in streamed text.
- [Custom extensions](guides/custom-extensions.md): writing your own renderers, image loaders and highlighters.
