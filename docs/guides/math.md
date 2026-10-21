---
title: "Math"
---

# Math

The `MarkView.Avalonia.Math` package renders LaTeX formulas with [CSharpMath](https://github.com/verybadcat/CSharpMath), in .NET: no browser, WebView or JavaScript.

```bash
dotnet add package MarkView.Avalonia.Math
```

```csharp
viewer.UseMath();
```

```markdown
Inline: Einstein's equation is $E = mc^2$, or \(E = mc^2\).

$$
\left( \sum_{k=1}^n a_k b_k \right)^2 \leq \left( \sum_{k=1}^n a_k^2 \right) \left( \sum_{k=1}^n b_k^2 \right)
$$
```

Math is opt-in: `$` is common in ordinary text (`$5`), so a viewer only reads it as math once enabled.

## Enabling math

Math needs both the syntax in the pipeline and the renderer. `UseMath()` does both: it adds the renderer and sets the viewer's pipeline to `UseGitHubExtensions()` plus Markdig's `UseMathematics()` (`$…$`, `$$…$$`) and the package's `UseMathBracketDelimiters()` (`\(…\)`, `\[…\]`).

Since it replaces the viewer's pipeline, build the pipeline yourself to combine math with other syntax, or to enable it for every viewer:

```csharp
MarkdownViewerDefaults.Extensions.AddMath();
MarkdownViewerDefaults.Pipeline = new MarkdownPipelineBuilder()
    .UseGitHubExtensions()
    .UseMathematics()            // $…$ and $$…$$
    .UseMathBracketDelimiters()  // \(…\) and \[…\]
    .UseThinkingBlocks()
    .Build();
```

## Bracket delimiters

Language models and LaTeX sources often write math as `\(…\)` (inline) and `\[…\]` (display) rather than with dollars. They render exactly like their dollar counterparts:

```markdown
The roots of \(ax^2 + bx + c = 0\) are
\[
x = \frac{-b \pm \sqrt{b^2 - 4ac}}{2a}
\]
for any \(a \neq 0\).
```

- `\(…\)` stays on one line, like `$…$`.
- `\[…\]` may span lines. When it has its lines to itself it is a block, even right under a line of text; when it shares a line with text it is inline.
- Code spans and code blocks are left alone.

In CommonMark, `\(` and `\[` are an escaped parenthesis and bracket, so with the bracket delimiters on, text such as `\(see below\)` becomes a formula. They stay escapes when the opening is glued to a letter or digit (`arr\[i\]`, `f\(x\)`), is never closed, or encloses nothing (`\(\)`). A formula's lines are read as a paragraph first: a line inside `\[…\]` that starts like a list item (`- `, `1. `) or a heading ends the paragraph, as does a blank line (which LaTeX does not allow in display math either), so the `\[` is left unclosed and shows as text.

To keep only the dollars, and the CommonMark escapes:

```csharp
viewer.UseMath(bracketDelimiters: false);
```

The bracket delimiters also work without the dollars, for documents where `$` is common in prose: build the pipeline with `UseMathBracketDelimiters()` and without `UseMathematics()`.

## Copy button

Each display formula has a copy button in its top right corner, which copies its TeX source without the `$$` or `\[` delimiters and shows a check mark, as the code block button does. It follows `ShowCodeBlockCopyButton` and `ShowCodeBlockCopyButtonOnHover` (see [Code blocks](code-blocks.md#toolbar-and-copy-button)). The formula keeps a margin on both sides as wide as the button, so it stays centred and never runs under it. The copy goes through [`CopyingToClipboard`](text-selection.md#changing-what-is-copied), with `MarkdownCopyOrigin.MathBlock` as its origin.

Inline formulas have no button. Selecting a formula, inline or not, copies its source with its delimiters.

## Colours, cache and streaming

- Formulas are drawn as bitmaps in the text colour of the theme variant (`#FAFAFA` dark, `#27272A` light), and drawn again when the variant changes.
- The bitmaps of recent formulas are kept (up to 16 MB of pixels, shared by every viewer), so a formula shown again (the document rendered again, a streamed paragraph updated, the theme switched back) is not typeset again.
- With [lazy rendering](displaying-documents.md#lazy-rendering), a formula is typeset when it is about to scroll into view.
- In a [streaming session](streaming.md), a `$$` or `\[` block at the end of the text shows as its source until a blank line or another block follows it; an open `\(` is closed for display.
- Math and Mermaid both take over fenced blocks; they can be registered in either order.

## Errors

Invalid LaTeX shows CSharpMath's own error text rather than throwing. A formula past the safety limits (10,000 characters, or braces nested more than 50 deep) shows as its source. An unexpected failure shows the source of a block formula as text, and leaves the last rendering of an inline formula in place.

## Styling

A display formula is a `Border.markdown-math-block`, an inline one an `Image.markdown-math-inline`; see [Style classes](../reference/style-classes.md#math). The package has no theme file of its own.
