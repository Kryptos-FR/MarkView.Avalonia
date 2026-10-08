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

// Read the current selection
string text = viewer.GetSelectedText();

// Copy to clipboard
await viewer.CopyToClipboardAsync();
```

## What is selectable

All text-bearing block types are registered with the selection layer:

- Paragraphs
- Headings
- Code blocks (all text within the block)
- Blockquotes
- List items, including their markers (`•`, `1.`, or the `☑`/`☐` task-list glyph) and any code blocks or tables nested inside them
- Table cells (tab-separated when copying)
- Footnote definitions

Non-text content is copied as a text equivalent:

| Content | Highlighted | Copied as |
|---------|-------------|-----------|
| Image (including linked images and YouTube thumbnails) | With the surrounding text | Its alt text; nothing when it has none |
| Inline math (`MarkView.Avalonia.Math`) | With the surrounding text | Its source between `$` delimiters |
| Display math (`MarkView.Avalonia.Math`) | As a whole block | Its source between `$$` lines |
| Mermaid diagram (`MarkView.Avalonia.Mermaid`) | As a whole block | Its source in a ```` ```mermaid ```` fence |

Custom renderers opt their own non-text controls in through the `MarkdownSelection.CopyText` attached property — see [Content that is not text](custom-extensions.md#content-that-is-not-text).

## How it works

`DocumentSelectionLayer` is a single transparent `Control` rendered in a single-cell `Grid` on top of the document. It tracks a start and end character offset across the entire flattened character index built by the renderer.

On `PointerMoved`, the layer calls `TextBlock.TextLayout.HitTestPoint` + `TranslatePoint` for each registered text block to find the nearest character offset, then redraws the selection highlight rectangles using `HitTestTextRange`.

On copy, `GetSelectedText()` extracts the substring from each text block's registered text and joins them with newlines (tabs between table cells, a space after a list marker). A list item's marker is registered as its own text block, so it is highlighted with the item and selection offsets inside the item text line up with what is rendered.

A control embedded in a text block (`InlineUIContainer`) occupies one character position in its `TextLayout`, so it is registered as a U+FFFC placeholder that keeps selection offsets aligned with the layout. A block control with copy text is registered as an entry of its own, one placeholder long, whose bounds are hit-tested and highlighted as a whole. On copy, each placeholder is replaced by the control's `MarkdownSelection.CopyText`.
