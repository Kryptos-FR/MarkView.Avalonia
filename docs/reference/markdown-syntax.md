---
title: "Markdown syntax"
---

# Markdown syntax

What the viewer renders, and what enables it. See [Markdown and the pipeline](../guides/markdown.md) for how to build a pipeline.

## CommonMark

Always on, whatever the pipeline.

| Syntax | Notes |
|---------|-------|
| Headings, `#` and underlined | Each gets a GitHub-style anchor and is listed in `TableOfContents`; can [fold](../guides/folding.md) |
| `**bold**`, `*italic*`, `` `code` `` | |
| Links and `<https://…>` autolinks | Open with the platform's launcher, or raise [`LinkClicked`](../guides/displaying-documents.md#links) |
| Images | Loaded in the background, [sized](../guides/images.md#sizing) to the viewer; `![alt](url =WxH)` caps the size |
| Fenced and indented code blocks | Copy button, optional language, line numbers, wrapping; any language can render as [your own control](../guides/code-blocks.md#custom-fenced-blocks) |
| Block quotes, ordered and unordered lists, thematic breaks | Tight and loose lists |
| Hard line breaks | `\` or two spaces at the end of a line |
| HTML | `<br>` always; the [HTML subset](../guides/markdown.md#html-subset) with the default pipeline |

## The default pipeline

Enabled by `UseGitHubExtensions()`, the pipeline of a viewer without one.

| Feature | Syntax |
|---------|--------|
| Strikethrough | `~~text~~` or `~text~`, as on GitHub |
| Task lists | `- [x] item`; clickable with [`AreTaskListsInteractive`](../guides/task-lists.md) |
| Pipe tables | `\| col \| col \|`; the context menu copies a table for a spreadsheet or as Markdown |
| Autolinks | Bare `https://…` addresses |
| Emoji shortcodes | `:rocket:` is 🚀; ASCII smileys such as `:)` stay as they are |
| [Alerts](../guides/markdown.md#alerts) | `> [!NOTE]`, `[!TIP]`, `[!IMPORTANT]`, `[!WARNING]`, `[!CAUTION]` |
| Footnotes | `[^1]` |
| CJK-friendly emphasis | Emphasis next to Chinese, Japanese and Korean punctuation |
| YAML front matter | A `---` block at the top of the document, hidden |
| [Details blocks](../guides/markdown.md#details-blocks) | HTML `<details>` and `<summary>`; also `UseHtmlDetails()` on its own |
| [HTML subset](../guides/markdown.md#html-subset) | `<p align>` and `<div align>` headers, `<a><img></a>` badges, `<kbd>`, `<sub>`, `<sup>`, `<mark>`, `<b>`, `<code>`, `<hr>`…; nothing runs, only `<img>` sources are fetched. Also `UseHtmlSubset()` on its own |

## Opt-in syntax

Added to a pipeline with the method shown, or with the viewer's shortcut of the same name where there is one (`viewer.UseFigures()`…).

| Feature | Method | Syntax |
|---------|-----------|--------|
| Foldable alerts and custom titles | `UseExtendedAlertBlocks()`, `viewer.UseAlertBlocks()` | `> [!NOTE] Custom title`, `> [!NOTE]-` (collapsed), `> [!NOTE]+` (expanded) |
| [Thinking blocks](../guides/chat.md#thinking-blocks) | `UseThinkingBlocks()` | A reasoning model's `<think>…</think>` |
| Abbreviations | `UseAbbreviations()` | `*[HTML]: HyperText Markup Language` |
| Citations | `UseCitations()` | `""quoted text""` |
| Figures | `UseFigures()` | `^^^` blocks, `^^^ caption` |
| Hard line breaks | `UseHardlineBreaks()` | Every line break is a hard break |
| YouTube thumbnails | `UseMediaLinks()` | `![title](https://youtu.be/…)` |
| Definition lists | `UseDefinitionLists()` | `Term`, then `:   definition` |
| Custom containers | `UseCustomContainers()` | `:::name` … `:::` blocks, `::text::` inline |
| Footers | `UseFooters()` | `^^ footer text` |
| Smart punctuation | `UseSmartyPants()` | `"quotes"`, `--`, `---`, `...`, `<<…>>` become “ ” – — … « » |
| Letter and roman numeral lists | `UseListExtras()` | `a.`, `A.`, `i.`, `I.` |
| Style classes | `UseGenericAttributes()`, called last | `{.name}` adds `name` to the element's style classes |
| Superscript, underline, highlight | `UseEmphasisExtras()` | `^text^`, `++text++`, `==text==`; subscript `~text~` only without `UseGitHubExtensions()` |
| Grid tables | `UseGridTables()` | RST-style grid tables |

## Extension packages

| Feature | Package and method | Syntax |
|---------|-----------|--------|
| [Syntax highlighting](../guides/code-blocks.md#syntax-highlighting) | SyntaxHighlighting, `UseTextMateHighlighting()` | Fenced code blocks with a language |
| [SVG images](../guides/images.md#svg-images) | Svg, `UseSvg()` | `![alt](image.svg)`, `data:image/svg+xml` |
| [Mermaid diagrams](../guides/images.md#mermaid-diagrams) | Mermaid, `UseMermaid()` | ```` ```mermaid ```` fences |
| [Math](../guides/math.md) | Math, `UseMath()` | `$…$`, `$$…$$`, `\(…\)`, `\[…\]` |
