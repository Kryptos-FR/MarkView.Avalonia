---
title: "Long documents"
---

# Long documents

Set `IsVirtualizationEnabled` to keep only the top-level blocks near the visible area in the visual tree, so that a long document opens, scrolls and switches theme in about the time a short one does:

```xml
<mv:MarkdownViewer Source="avares://MyApp/Assets/CHANGELOG.md" IsVirtualizationEnabled="True" />
```

```csharp
viewer.IsVirtualizationEnabled = true;
```

It is off by default. It pays off from a few hundred kilobytes of Markdown; a README or a chat answer shows no difference.

## What it changes

Most of the time a long document takes to show goes to styling, laying out and drawing controls, and the viewer builds several for each paragraph, list item or table cell. With virtualization, every top-level block (a paragraph, a list, a table, a code block) is still rendered, but only the blocks within one viewport height of the visible area are attached to the visual tree, styled, laid out and drawn. The others are attached as the reader scrolls near them, and detached again once they are more than three viewport heights away.

| A 529 KB document, desktop | Off | On |
|---|---|---|
| First frame | 7.0 s | 1.5 s |
| Memory kept | 335 MB | 95 MB |
| Theme switch | 1.6 s | 52 ms |

Measured with the generated sections of the benchmark corpus (text, lists, tables, alerts, code and formulas), 3,203 top-level blocks, in a 900×700 window.

Everything that works on the document still covers all of it:

- **Selection and copy**: Select All copies the whole document, and a selection stays when the blocks it spans scroll away.
- **Find**: matches are counted in every block, and moving to one attaches its block and scrolls it into view.
- **Anchors and the table of contents**: a jump attaches the block it goes to first, then scrolls; scroll anchoring keeps the target in place while the blocks around it are laid out.
- **Tab**: links and focusable controls keep their place in the tab order, and reaching one attaches its block. The block that holds the focus stays attached wherever the reader scrolls.
- **Folding, task lists, live preview and streaming** work as without it.

## The scroll bar

A block that has never been laid out has no height yet, so the viewer estimates one from its source (lines of code, length of text, rows of a table), scaled by how tall the blocks already laid out turned out to be. The scroll bar's size and position are approximate until the reader has scrolled through the document, and dragging its thumb lands on an estimated position. When a block's real height replaces its estimate above the text in view, the text in view stays where it is.

## When to leave it off

- **Screen readers** see the blocks attached near the visible area, as in any virtualizing list, rather than the whole document at once.
- **A viewer rendered to a bitmap** (printing, export) shows only what is attached: leave it off, as you would turn off [lazy rendering](displaying-documents.md#lazy-rendering).
- **Code that walks the visual tree** of the document finds only the attached blocks. The document's root panel still holds every top-level block in its `Children`.

With lazy rendering on as well (the default), images, formulas, diagrams and code colours inside an attached block still wait until they come near the visible area.
