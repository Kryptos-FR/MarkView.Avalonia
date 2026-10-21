---
title: "Folding"
---

# Folding

Four kinds of blocks fold: headings with their sections, alerts, `<details>` blocks and a language model's reasoning. Headings fold once `AreHeadingsFoldable` is set; the others fold where the Markdown asks for it.

```xml
<mv:MarkdownViewer Source="avares://MyApp/Docs/guide.md" AreHeadingsFoldable="True" />
```

| Block | Folds when | Starts |
|---|---|---|
| Heading | `AreHeadingsFoldable` is set | Expanded |
| [Alert](markdown.md#alerts) | It has a fold marker, `> [!NOTE]-` or `> [!NOTE]+` (needs `UseExtendedAlertBlocks()` or `viewer.UseAlertBlocks()`) | Collapsed with `-`, expanded with `+` |
| [Details block](markdown.md#details-blocks) | Always (`<details>`, in the default pipeline) | Collapsed, unless `<details open>` |
| [Thinking block](chat.md#thinking-blocks) | Always (`UseThinkingBlocks()`) | Collapsed, except while a streaming session receives it |

## What every folded block does

- Click its header (the heading's button, the alert's title, the summary), or focus it with Tab and press Enter or Space, to show or hide it. Screen readers see a button that reports whether the block is expanded.
- A collapsed block cannot be selected or copied.
- `ScrollToAnchor`, and so in-document links and the table of contents, expands the blocks around its target before scrolling to it. The [find](find-in-document.md) methods expand them to show a match.
- In a [streaming session](streaming.md), a block the reader expanded or collapsed keeps that state when an update renders it again.

## Foldable headings

`AreHeadingsFoldable` starts each heading with a fold button, as in Obsidian or Notion, for long documents read section by section.

A heading's section is everything after it up to the next heading of the same or a higher level (`##` folds the blocks below it until the next `##` or `#`), nested sections included. Collapsing a heading hides its section; the heading stays. Expanding it again shows the section as it was, so a nested heading that was collapsed stays collapsed. Every section starts expanded, and a new document (another `Markdown`, `Source` or `Pipeline`) starts with every section expanded. Setting the property renders the document again.

- The heading's text stays selectable, and its links clickable.
- Only headings fold. A heading inside a quote, a list item or an alert folds what follows it there, not the rest of the document.
- `ScrollToAnchor` and find expand the sections around their target; a heading navigated to keeps its own section as it was.
- In a streaming session, blocks that arrive after a collapsed heading join its section, and a heading still being written keeps the state the reader gave it.

## Styling

Each kind has its classes, with an `:expanded` pseudo-class while its content shows; see [Style classes](../reference/style-classes.md#foldable-headings). Their content is hidden by the control, not by a style. For example, to dim collapsed headings and show their button more discreetly:

```xml
<Style Selector=":is(TextBlock).markdown-heading-foldable:not(:expanded)">
  <Setter Property="Opacity" Value="0.8" />
</Style>
<Style Selector=":is(Control).markdown-heading-fold">
  <Setter Property="Opacity" Value="0.6" />
</Style>
<Style Selector=":is(Control).markdown-heading-fold:pointerover">
  <Setter Property="Opacity" Value="1" />
</Style>
```
