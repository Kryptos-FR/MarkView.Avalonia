---
title: "Markdown and the pipeline"
---

# Markdown and the pipeline

A viewer renders Markdown as GitHub does, with no setup. The [Markdig](https://github.com/xoofx/markdig) pipeline decides which syntax is read: to add more, build one and set it on the viewer, or on every viewer with `MarkdownViewerDefaults.Pipeline`:

```csharp
viewer.Pipeline = new MarkdownPipelineBuilder()
    .UseGitHubExtensions()      // the default
    .UseDefinitionLists()
    .UseSmartyPants()
    .Build();
```

The [Markdown syntax](../reference/markdown-syntax.md) reference lists every construct and the method that enables it.

## The default: GitHub's Markdown

`UseGitHubExtensions()` is what a viewer uses when no pipeline is set. On top of CommonMark, it reads:

- Pipe tables and task lists (`- [x]`).
- Strikethrough, `~~text~~` and, as on GitHub, `~text~`.
- Bare URLs as links, and emoji shortcodes (`:rocket:` is 🚀). ASCII smileys such as `:)` stay as they are.
- [Alerts](#alerts) (`> [!NOTE]`) and footnotes (`[^1]`).
- Emphasis next to Chinese, Japanese and Korean punctuation.
- YAML front matter, hidden rather than shown as text.
- [`<details>` blocks](#details-blocks) and the [HTML that READMEs use](#html-subset).

Math and Mermaid diagrams, which GitHub also renders, come from their own packages: see [Math](math.md) and [Images and diagrams](images.md#mermaid-diagrams).

Both helpers, `UseGitHubExtensions()` and `UseSupportedExtensions()`, take a `MarkdownExtensionsOptions` for the pipe table options.

## Adding syntax

The methods below are Markdig's own, except `UseHardlineBreaks()`, MarkView's shorthand for Markdig's `UseSoftlineBreakAsHardlineBreak()`. MarkView adds `UseExtendedAlertBlocks()` ([alerts](#alerts)), `UseThinkingBlocks()` ([thinking blocks](chat.md#thinking-blocks)), `UseHtmlDetails()` and `UseHtmlSubset()`. Import both `Markdig` and `MarkView.Avalonia` to use them together.

```csharp
viewer.Pipeline = new MarkdownPipelineBuilder()
    .UseGitHubExtensions()
    .UseAbbreviations()         // *[HTML]: HyperText Markup Language
    .UseCitations()             // ""quoted text""
    .UseFigures()               // ^^^ figure blocks
    .UseHardlineBreaks()        // every line break is a hard break
    .UseMediaLinks()            // YouTube thumbnails
    .UseDefinitionLists()       // Term, then ":   definition"
    .UseCustomContainers()      // :::name … ::: blocks and ::text:: inlines
    .UseFooters()               // ^^ footer lines
    .UseSmartyPants()           // "quotes" -- --- ... << >> become “ ” – — … « »
    .UseListExtras()            // a. A. i. I. ordered lists
    .UseGenericAttributes()     // {.name} style classes; must be the last extension
    .Build();
```

For one extension, the viewer has shortcuts that set `UseGitHubExtensions()` plus that extension. Each replaces the viewer's pipeline, so build the pipeline yourself to combine several:

```csharp
viewer.UseAbbreviations();
viewer.UseAlertBlocks();      // foldable alerts and custom titles
viewer.UseCitations();
viewer.UseFigures();
viewer.UseFootnotes();        // already in the default
viewer.UseHardlineBreaks();
viewer.UseMediaLinks();
viewer.UseThinkingBlocks();   // a language model's <think> reasoning, folded away
```

How the extensions render:

- **Definition lists**: terms as `TextBlock.markdown-definition-term`, each definition as an indented `StackPanel.markdown-definition`.
- **Custom containers**: a `Border.markdown-custom-container` with the container's name as an extra class, so `:::warning` can be styled with `Border.markdown-custom-container.warning`. Text after the name (`:::warning Read this first`) becomes a `markdown-custom-container-title` header. Inline `::text::` is a `Span.markdown-custom-inline`.
- **Footers**: a `StackPanel.markdown-footer`, in smaller, dimmed text.
- **SmartyPants**: the replacements of the `SmartyPantOptions` passed to `UseSmartyPants()`, with HTML entities decoded, so French guillemets with non-breaking spaces work:

  ```csharp
  var smartyPants = new SmartyPantOptions();
  smartyPants.Mapping[SmartyPantType.LeftDoubleQuote] = "&laquo;&nbsp;";
  smartyPants.Mapping[SmartyPantType.RightDoubleQuote] = "&nbsp;&raquo;";
  viewer.Pipeline = new MarkdownPipelineBuilder().UseGitHubExtensions().UseSmartyPants(smartyPants).Build();
  ```

- **List extras**: items numbered with letters or roman numerals. The `.` or `)` after the number is kept for every ordered list.
- **Generic attributes**: each `{.name}` adds `name` to the style classes of the control rendered for that element (heading, paragraph, link, emphasis, container…), for your own styles such as `:is(TextBlock).markdown-paragraph.note`. Ids and other attributes are ignored. Markdig only reads attributes for the extensions set up before it, so call `UseGenericAttributes()` last.

## Subscript and the previous default

`~text~` is either strikethrough (GitHub) or subscript (Markdig's emphasis extras). With `UseGitHubExtensions()` it is always strikethrough, even with `UseEmphasisExtras()` added after it; `UseEmphasisExtras()` still adds superscript `^text^`, underline `++text++` and highlight `==text==`.

For subscript, start from `UseSupportedExtensions()`, the default before 12.4, and add what you need from the GitHub set. `~~text~~` stays strikethrough:

```csharp
viewer.Pipeline = new MarkdownPipelineBuilder()
    .UseSupportedExtensions()   // ~text~ is subscript
    .UseAlertBlocks()
    .UseFootnotes()
    .Build();
```

`UseSupportedExtensions()` reads the emphasis extras (`~~`, `~`, `^`, `++`, `==`) and RST-style grid tables, plus autolinks, pipe tables, task lists, CJK-friendly emphasis, YAML front matter, emoji shortcodes and the HTML subset, but not alerts, footnotes or details blocks.

## Alerts

GitHub's alerts render in the default pipeline. `UseExtendedAlertBlocks()` (or `viewer.UseAlertBlocks()`) adds the Obsidian-style extras, a custom title and folding, which Markdig alone leaves as a plain quote:

```markdown
> [!NOTE]
> Plain GitHub alert: the header shows the kind.

> [!TIP] Custom *title*
> The header shows "Custom title" (formatting kept) instead of "TIP".

> [!WARNING]- Collapsed by default
> Click the header, or focus it and press Enter or Space, to show this body.

> [!IMPORTANT]+
> Foldable, starts expanded.
```

- The fold marker (`-` collapsed, `+` expanded) must follow `]` directly; a title is separated from `]` or the marker by a space or tab. `> [!NOTE]x` is not an alert.
- Any kind made of ASCII letters is accepted, as in Markdig: `> [!QUESTION]` renders with the neutral alert style plus a `markdown-alert-question` class.
- `UseExtendedAlertBlocks()` and Markdig's `UseAlertBlocks()` can be combined in either order; with Markdig's alone, only plain alerts are read.
- A collapsed alert behaves like every [folded block](folding.md).

## Details blocks

HTML `<details>` and `<summary>` render as blocks that fold, as on GitHub. They are in the default pipeline; `UseHtmlDetails()` adds them to another one.

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

- Click the summary, or focus it and press Enter or Space, to show or hide the body. It starts hidden unless the tag has `open`. Without a `<summary>`, the summary reads "Details", as in browsers.
- Leave a blank line between the tags and the Markdown inside. As on GitHub, text on lines next to the tags belongs to the HTML block and is not read as Markdown. With the [HTML subset](#html-subset), such text and the summary render as HTML: `<summary><b>Title</b></summary>` reads "**Title**". Without it, they show as plain text, tags removed.
- A `<details>` without its `</details>`, or the reverse, or one closed in another container (opened outside a list and closed inside it), is left as raw HTML.
- A pipeline that disables HTML (Markdig's `DisableHtml()`) has no details blocks.
- A collapsed block behaves like every [folded block](folding.md).

## HTML subset

GitHub READMEs often use a little HTML: a centred logo and title, a row of badges, `<kbd>` keys, `<sub>` and `<sup>`. The default pipeline renders a small, safe subset of HTML with native controls, without a browser: nothing is run, and nothing is fetched but the `src` of an `<img>` (or the source of a `<picture>` for the theme), through the viewer's image loaders like a Markdown image. `UseHtmlSubset()` adds it to another pipeline (`UseSupportedExtensions()` includes it).

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
| `<sub>`, `<sup>` | Subscript and superscript, in a smaller font |
| `<kbd>` | A keyboard key; selection copies its text |
| `<code>` | Inline code, like `` `code` ``, of the element's text |
| `<a href="…" title="…">` | A link, like `[text](href "title")`: same click handling (`LinkClicked`), address resolution against `BaseUri` and style |
| `<img src="…" alt="…" title="…" width="…" height="…">` | An image, like `![alt](src)`: same image loaders, address resolution, tooltip and copy text. `width` and `height`, in pixels, cap its size like the `=WxH` [size hint](images.md#sizing), and either can be given alone; other values (`50%`) are ignored |
| `<picture>` with `<source media="…" srcset="…">` and an `<img>` | The `<img>`, showing the first `<source>` that applies to the application's theme: one without `media`, or one whose `media` is `(prefers-color-scheme: dark)` or `(prefers-color-scheme: light)`. It switches when the theme changes. Only the first address of a `srcset` is read; a source with any other `media` is ignored, and without a source that applies the `<img>` shows its own `src` |
| `<br>` | A line break |

HTML blocks, HTML that starts a line, which CommonMark does not read Markdown inside:

| Tags | Renders as |
|------|------------|
| `<p>`, `<h1>`…`<h6>` | A paragraph or heading of the inline HTML inside it. A heading gets an anchor and is listed in `TableOfContents`, like a Markdown heading |
| `<div>` | Its content; text and inline elements outside any `<p>` become a paragraph |
| `align="left\|center\|right\|justify"` on `<p>`, `<div>` and `<h1>`…`<h6>` | Aligns the text and images inside it, as `text-align` does |
| `dir="rtl\|ltr"` on `<p>`, `<div>` and `<h1>`…`<h6>` | Sets the direction of its text. See [Right-to-left text](right-to-left.md#the-html-dir-attribute) |
| `<hr>` | A thematic break, like `---` |

A `<div align="center">` and its `</div>`, each separated by blank lines from the Markdown between them, align that Markdown too, as on GitHub:

```markdown
<div align="center">

# MyProject

![Logo](docs/logo.png)

</div>
```

- Only the `href`, `src`, `srcset`, `media`, `alt`, `title`, `width`, `height`, `align` and `dir` attributes are read; any other (`style`, `class`, `onclick`…) is ignored.
- Any other tag (`<span>`, `<table>`, `<iframe>`…), and a tag without its match (`<b>` never closed, a stray `</i>`), is dropped, and the text around it still shows. The content of `<script>` and `<style>` is dropped with them, as GitHub does. Comments do not show.
- Tags pair like elements: `<b><i>x</i></b>` nests, and `</b>` in `<b><i>x</b>` also closes the `<i>`. Inline tags pair within the same Markdown inline only: in `*<b>x*</b>`, the `<b>` is inside the emphasis and the `</b>` outside, so both are dropped. A `<div>` pairs with a `</div>` among the blocks of the same container; one without its match is dropped.
- A `<p>` or heading ends at its end tag, at the next block tag, or at the end of its HTML block. Whitespace collapses as in HTML.
- HTML blocks that Markdig starts with `<script>`, `<style>`, `<pre>`, `<textarea>`, a comment or a declaration are not rendered.
- A link inside a details summary shows as a link but is not clickable: clicking the summary folds the block.
- A pipeline that disables HTML (Markdig's `DisableHtml()`) shows HTML as literal text.

See [Style classes](../reference/style-classes.md#html-subset) to restyle `<kbd>` and aligned blocks.
