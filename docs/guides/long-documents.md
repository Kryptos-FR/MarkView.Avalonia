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

Most of the time a long document takes to show goes to building, styling, laying out and drawing controls, and the viewer builds several for each paragraph, list item or table cell. With virtualization, the viewer builds the controls of a top-level block (a paragraph, a list, a table, a code block) only when it comes within one viewport height of the visible area, and builds the others a few at a time while the application is idle, so that the whole document is built a moment later without holding up input or drawing. Only the blocks near the visible area are attached to the visual tree, styled, laid out and drawn. The others are attached as the reader scrolls near them, and detached again once they are more than three viewport heights away.

| Desktop, 900×700 window | Off | On |
|---|---|---|
| A 529 KB document: first frame | 4.9 to 7.0 s | 0.24 s |
| Memory kept, once the whole document is built | 335 MB | 95 MB |
| Theme switch | 1.6 s | 45 ms |
| A 2.1 MB document: first frame | 17 s | 0.4 s |
| Memory kept, once the whole document is built | 1.35 GB | 380 MB |

Measured with the generated sections of the benchmark corpus (text, lists, tables, alerts, code and formulas): 3,203 top-level blocks in 529 KB. Most of the first frame then goes to parsing the Markdown; the blocks built later while idle take 1.7 s at 529 KB, in slices of a few milliseconds.

Everything that works on the document still covers all of it:

- **Selection and copy**: Select All copies the whole document, and a selection stays when the blocks it spans scroll away.
- **Find**: matches are counted in every block, and moving to one attaches its block and scrolls it into view. A search, Select All and Tab build the blocks not built yet first.
- **Anchors and the table of contents**: a jump builds and attaches the block it goes to first, then scrolls; scroll anchoring keeps the target in place while the blocks around it are laid out. `TableOfContents` lists the headings once the whole document is built, a moment after it opens.
- **Tab**: links and focusable controls keep their place in the tab order, and reaching one attaches its block. The block that holds the focus stays attached wherever the reader scrolls.
- **Folding, task lists, live preview and streaming** work as without it.

## The scroll bar

A block that has never been laid out has no height yet, so the viewer estimates one from its source (lines of code, length of text, rows of a table), scaled by how tall the blocks already laid out turned out to be. The scroll bar's size and position are approximate until the reader has scrolled through the document, and dragging its thumb lands on an estimated position. When a block's real height replaces its estimate above the text in view, the text in view stays where it is.

## When to leave it off

- **Screen readers** see the blocks attached near the visible area, as in any virtualizing list, rather than the whole document at once.
- **A viewer rendered to a bitmap** (printing, export) shows only what is attached: leave it off, as you would turn off [lazy rendering](displaying-documents.md#lazy-rendering).
- **Code that walks the visual tree** of the document finds only the attached blocks. The document's root panel holds every top-level block built so far in its `Children`.

With lazy rendering on as well (the default), images, formulas, diagrams and code colours inside an attached block still wait until they come near the visible area.
