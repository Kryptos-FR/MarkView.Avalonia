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
- List items (including markers)
- Table cells (tab-separated when copying)
- Footnote definitions

The following are **not selectable**:

- Images (`InlineUIContainer` — same limitation as all reference libraries)
- Task-list checkboxes (`InlineUIContainer`)

## How it works

`DocumentSelectionLayer` is a single transparent `Control` rendered in a single-cell `Grid` on top of the document. It tracks a start and end character offset across the entire flattened character index built by the renderer.

On `PointerMoved`, the layer calls `TextBlock.TextLayout.HitTestPoint` + `TranslatePoint` for each registered text block to find the nearest character offset, then redraws the selection highlight rectangles using `HitTestTextRange`.

On copy, `GetSelectedText()` extracts the substring from each text block's registered text and joins them with newlines (or tabs for table cells).
