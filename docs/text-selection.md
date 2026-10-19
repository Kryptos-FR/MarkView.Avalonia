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
string text = viewer.SelectedText;          // or viewer.GetSelectedText()

// Copy to clipboard (with the platform's line endings, "\r\n" on Windows)
await viewer.CopyToClipboardAsync();
```

## Selection changes

`SelectionChanged` is raised whenever the selected range changes: at each step of a drag that moves its end to another character, on `SelectAll()` and `ClearSelection()`, on a click that clears it, when a new document replaces the one it was in, and when a [streaming](streaming.md) update shortens or clears it. It is not raised when the range stays the same, so pressing `Ctrl+A` twice raises it once.

When a drag that changed the selection ends, `SelectionChanged` is raised once more. `e.IsSelecting` (`MarkdownSelectionChangedEventArgs`) is `true` for the events raised during the drag and `false` for that last one and every other change, so a handler that only needs the final selection skips the rest:

```csharp
viewer.SelectionChanged += (_, e) =>
{
    if (!e.IsSelecting) preview.Text = viewer.SelectedText;
};
```

`HasSelection` is a bindable read-only property, `true` while some text is selected. It only changes when the selection starts or stops being empty, so it suits a Copy button's `IsEnabled`:

```xml
<Button Content="Copy" IsEnabled="{Binding #Viewer.HasSelection}" Click="OnCopy" />
```

```csharp
viewer.SelectionChanged += (_, _) => status.Text = $"{viewer.SelectedText.Length} characters selected";
```

The event args carry no text, so raising the event allocates no string. `SelectedText` is built on the first read after the selection changes, and later reads return the same string until it changes again (or an alert in it is folded or unfolded). It is not a bindable property.

`SelectionChanged` is a bubbling routed event (`MarkdownViewer.SelectionChangedEvent`), so a container holding several viewers, such as a chat list, can handle it once for all of them; `e.Source` is the viewer.

## Changing what is copied

`CopyingToClipboard` is raised before the viewer writes anything to the clipboard, when the selection is copied (`Ctrl+C` or `CopyToClipboardAsync()`) and when a code block's [copy button](configuration.md#code-block-toolbar) is clicked. It is not raised when there is nothing to copy.

| Property | Meaning |
|----------|---------|
| `Origin` | What is copied: `MarkdownCopyOrigin.Selection` or `MarkdownCopyOrigin.CodeBlock` |
| `Text` | The text to write, lines separated by `\n`. Set it to change what is written; it is written with the platform's line endings. Set to `null` or `""`, nothing is written. |
| `Cancel` | Set to `true` to write nothing, for instance when the application writes the clipboard itself. A code block's copy button then shows no confirmation. |

```csharp
viewer.CopyingToClipboard += (_, e) =>
{
    if (e.Origin == MarkdownCopyOrigin.Selection)
        e.Text += "\n\nCopied from the docs";
};
```

Like `SelectionChanged`, it is a bubbling routed event (`MarkdownViewer.CopyingToClipboardEvent`).

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

[Finding text](find-in-document.md) searches the same text blocks and draws its matches on the same overlay, under the selection; the two are independent, so finding never changes the selection and selecting never clears the matches.

## How it works

`DocumentSelectionLayer` is a single transparent `Control` rendered in a single-cell `Grid` on top of the document. It tracks a start and end character offset across the entire flattened character index built by the renderer.

On `PointerMoved`, the layer calls `TextBlock.TextLayout.HitTestPoint` + `TranslatePoint` for each registered text block to find the nearest character offset, then redraws the selection highlight rectangles using `HitTestTextRange`. Both are limited to the part of each text block that its clipping ancestors inside the document (such as a code block's `ScrollViewer`) leave visible.

On copy, `GetSelectedText()` extracts the substring from each text block's registered text and joins them with newlines (tabs between table cells, a space after a list marker). Every line ending in the result is `\n`, including those inside code blocks and copy texts. The clipboard, both from `Ctrl+C` and from a code block's copy button, gets the platform's line endings instead (`Environment.NewLine`, so `\r\n` on Windows). A list item's marker is registered as its own text block, so it is highlighted with the item and selection offsets inside the item text line up with what is rendered.

A control embedded in a text block (`InlineUIContainer`) occupies one character position in its `TextLayout`, so it is registered as a U+FFFC placeholder that keeps selection offsets aligned with the layout. A block control with copy text is registered as an entry of its own, one placeholder long, whose bounds are hit-tested and highlighted as a whole. On copy, each placeholder is replaced by the control's `MarkdownSelection.CopyText`.
