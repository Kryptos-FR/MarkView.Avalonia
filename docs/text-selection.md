# Text Selection

`MarkdownViewer` supports document-wide text selection via a transparent `DocumentSelectionLayer` overlay. The layer covers the entire rendered document and uses `TextBlock.TextLayout` hit-testing to map pointer positions to character offsets.

## User interactions

| Gesture / Key | Action |
|---------------|--------|
| Click + drag | Select a text range |
| `Ctrl+A` | Select all text in the document |
| `Ctrl+C` | Copy the current selection to the clipboard |

## Programmatic API

```csharp
// Select all text
viewer.SelectAll();

// Clear the selection
viewer.ClearSelection();

// Read the current selection (lines separated by '\n')
string text = viewer.GetSelectedText();

// Copy to clipboard (with the platform's line endings, "\r\n" on Windows)
await viewer.CopyToClipboardAsync();
```

## What is selectable

All text-bearing block types are registered with the selection layer:

- Paragraphs
- Headings
- Code blocks (the code only: never their toolbar or [line numbers](configuration.md#line-numbers-wrapping-and-height))
- Blockquotes
- List items, including their markers (`•`, `1.`, or the `☑`/`☐` task-list glyph — for a clickable task-list checkbox, its current state) and any code blocks or tables nested inside them
- Table cells (tab-separated when copying)
- Footnote definitions
- The paragraphs and headings of [HTML blocks](configuration.md#html-subset), and the blocks of an aligned `<div>`

Non-text content is copied as a text equivalent:

| Content | Highlighted | Copied as |
|---------|-------------|-----------|
| Image (including linked images, HTML `<img>` and YouTube thumbnails) | With the surrounding text | Its alt text; nothing when it has none |
| HTML `<kbd>` key ([HTML subset](configuration.md#html-subset)) | As a whole, with the surrounding text | Its text |
| Inline math (`MarkView.Avalonia.Math`) | With the surrounding text | Its source between its own delimiters: `$...$`, `\(...\)`, or `\[...\]` within a line |
| Display math (`MarkView.Avalonia.Math`) | As a whole block | Its source between `$$` lines, or `\[` and `\]` lines |
| Mermaid diagram (`MarkView.Avalonia.Mermaid`) | As a whole block | Its source in a ```` ```mermaid ```` fence |
| Control of a [fence renderer](custom-extensions.md#rendering-a-fence-language-as-your-own-control) | As a whole block | Its source in its fence, or the control's own `MarkdownSelection.CopyText` |

Hidden content is not selectable: the body of a collapsed [foldable alert](configuration.md#alert-blocks) (`> [!NOTE]-`) or [details block](configuration.md#html-details-blocks) (`<details>`) is skipped by hit-testing, highlighting and copy (`Ctrl+A` then `Ctrl+C` copies the text around it) until it is expanded. The same applies to any block a custom renderer hides with `IsVisible`. An alert's header and a details block's summary are not selectable; clicking one toggles its block instead of starting a selection.

Content scrolled out of view inside the document is treated the same way while it is out of view: the lines of a code block taller than [`CodeBlockMaxHeight`](configuration.md#line-numbers-wrapping-and-height) that lie above or below its scroll viewport are neither hit-tested nor highlighted, so pointing at the text right below such a block selects that text, not the block's hidden lines. They are still copied when the selection spans them.

Custom renderers opt their own non-text controls in through the `MarkdownSelection.CopyText` attached property — see [Content that is not text](custom-extensions.md#content-that-is-not-text).

## How it works

`DocumentSelectionLayer` is a single transparent `Control` rendered in a single-cell `Grid` on top of the document. It tracks a start and end character offset across the entire flattened character index built by the renderer.

On `PointerMoved`, the layer calls `TextBlock.TextLayout.HitTestPoint` + `TranslatePoint` for each registered text block to find the nearest character offset, then redraws the selection highlight rectangles using `HitTestTextRange`. Both are limited to the part of each text block that its clipping ancestors inside the document (such as a code block's `ScrollViewer`) leave visible.

On copy, `GetSelectedText()` extracts the substring from each text block's registered text and joins them with newlines (tabs between table cells, a space after a list marker). Every line ending in the result is `\n`, including those inside code blocks and copy texts. The clipboard, both from `Ctrl+C` and from a code block's copy button, gets the platform's line endings instead (`Environment.NewLine`, so `\r\n` on Windows). A list item's marker is registered as its own text block, so it is highlighted with the item and selection offsets inside the item text line up with what is rendered.

A control embedded in a text block (`InlineUIContainer`) occupies one character position in its `TextLayout`, so it is registered as a U+FFFC placeholder that keeps selection offsets aligned with the layout. A block control with copy text is registered as an entry of its own, one placeholder long, whose bounds are hit-tested and highlighted as a whole. On copy, each placeholder is replaced by the control's `MarkdownSelection.CopyText`.
