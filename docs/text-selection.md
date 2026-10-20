# Text Selection

`MarkdownViewer` supports document-wide text selection via a transparent `DocumentSelectionLayer` overlay. The layer covers the entire rendered document and uses `TextBlock.TextLayout` hit-testing to map pointer positions to character offsets.

## User interactions

| Gesture / Key | Action |
|---------------|--------|
| Click + drag | Select a text range |
| `Ctrl+A` | Select all text in the document |
| `Ctrl+C` | Copy the current selection to the clipboard |
| Right-click or the menu key | Open the [context menu](#context-menu) |

## Programmatic API

```csharp
// Select all text
viewer.SelectAll();

// Clear the selection
viewer.ClearSelection();

// Read the current selection (lines separated by '\n')
string text = viewer.SelectedText;          // or viewer.GetSelectedText()

// Copy to clipboard (with the platform's line endings, "\r\n" on Windows), with HTML next to the text
await viewer.CopyToClipboardAsync();

// Read or copy the selection as Markdown
string markdown = viewer.GetSelectedMarkdown();
await viewer.CopyAsMarkdownAsync();
```

## Copying as Markdown

`GetSelectedMarkdown()` returns the selection as Markdown, and `CopyAsMarkdownAsync()` writes it to the clipboard as plain text. Lines are separated by `\n` in the first, by the platform's line endings in the second.

- A top-level block selected whole is copied as its own source text, with the source between consecutive whole blocks. Selecting the whole document (`Ctrl+A`) gives back the Markdown it was rendered from, as written, task toggles included.
- A block only partly selected, at either end of the selection, is rebuilt for its selected part: its text with its bold, italics, strikethrough, code, links and images, inside its heading, list item, task item, quote, alert, code fence or table row (a table keeps its header row). This part is written as normalised Markdown (`**bold**`, `*italic*`, `-` bullets, inline HTML for subscript, superscript, marked and inserted text), not with the source's own characters. Text that Markdown would read as syntax is escaped.
- A selection inside a single block copies only its text, with its inline syntax: a few words of a heading are not a heading, and part of a code block is its raw code.
- Formulas, diagrams and the controls of [fence renderers](custom-extensions.md#rendering-a-fence-language-as-your-own-control) are copied as their `MarkdownSelection.CopyText`, which is their source.
- Hidden content (a collapsed alert or details block) is left out of a partly selected block, as it is from the plain text.

A link written with a [reference](https://spec.commonmark.org/0.31.2/#link-reference-definitions) copied as part of a whole block keeps its `[text][ref]` form; the definition is only included when it is selected too.

## HTML on the clipboard

When the selection is copied (`Ctrl+C` or `CopyToClipboardAsync()`), the viewer also writes it as HTML, next to the plain text, so that pasting into a word processor, a chat or a web editor keeps headings, lists, emphasis, links, code and tables. The HTML is rendered by Markdig, with the viewer's pipeline, from what `GetSelectedMarkdown()` returns, so it holds exactly what was selected. Relative addresses are resolved against `BaseUri` (or the folder of `Source`).

Avalonia has no cross-platform HTML clipboard format, so the viewer writes `HTML Format` on Windows, `public.html` on macOS and `text/html` on Linux. In the browser and on mobile platforms, only the plain text is written.

Set `IsHtmlCopyEnabled="False"` to write the plain text alone. `CopyAsMarkdownAsync()` and a code block's copy button never write HTML.

## Context menu

The viewer has a context menu, opened by a right-click or, when the viewer or a link has the focus, by the menu key (the platform's `OpenContextMenu` hotkeys):

| Item | Action |
|------|--------|
| Copy | Copies the selection, as `Ctrl+C` does. Disabled when nothing is selected. |
| Copy as Markdown | Copies the selection [as Markdown](#copying-as-markdown). Disabled when nothing is selected. |
| Copy link | Copies the address of the link the menu was opened on, as written in the document. Shown only on a link, under the pointer or focused with `Tab`. |
| Select all | Selects the whole document, as `Ctrl+A` does. |

A right-click on text outside the selection clears it first, as in a text editor; a right-click inside it keeps it, so it can be copied.

The menu is the viewer's default `ContextFlyout`, used only when the application sets neither `ContextFlyout` nor `ContextMenu`. Set either one, locally or in a style, to show your own menu, or set `ContextFlyout` to `null` to have none:

```xml
<mv:MarkdownViewer ContextFlyout="{x:Null}" />
```

The labels are resources, read each time the menu opens, so they can be translated or reworded like any other resource:

| Resource key | Default |
|--------------|---------|
| `StringTextFlyoutCopyText` | `Copy` (the key a `TextBox`'s own menu uses, so a translation of it applies to both) |
| `MarkdownStringCopyAsMarkdown` | `Copy as Markdown` |
| `MarkdownStringCopyLink` | `Copy link` |
| `MarkdownStringSelectAll` | `Select all` |

```xml
<Application.Resources>
  <x:String x:Key="StringTextFlyoutCopyText">Copier</x:String>
  <x:String x:Key="MarkdownStringCopyAsMarkdown">Copier en Markdown</x:String>
  <x:String x:Key="MarkdownStringCopyLink">Copier le lien</x:String>
  <x:String x:Key="MarkdownStringSelectAll">Tout sélectionner</x:String>
</Application.Resources>
```

Each item has a style class (`markdown-context-copy`, `markdown-context-copy-markdown`, `markdown-context-copy-link`, `markdown-context-select-all`), for instance to give it an icon or hide it:

```xml
<Style Selector="MenuItem.markdown-context-copy-markdown">
  <Setter Property="IsVisible" Value="False" />
</Style>
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

`CopyingToClipboard` is raised before the viewer writes anything to the clipboard, when the selection is copied (`Ctrl+C`, `CopyToClipboardAsync()` or `CopyAsMarkdownAsync()`), when the [copy button](configuration.md#code-block-toolbar) of a code block or a [display formula](configuration.md#copying-a-formula) is clicked, and when the [context menu](#context-menu) copies a link. It is not raised when there is nothing to copy.

| Property | Meaning |
|----------|---------|
| `Origin` | What is copied: `MarkdownCopyOrigin.Selection`, `MarkdownCopyOrigin.SelectionAsMarkdown`, `MarkdownCopyOrigin.CodeBlock`, `MarkdownCopyOrigin.MathBlock` (a display formula's TeX source) or `MarkdownCopyOrigin.Link` |
| `Text` | The text to write, lines separated by `\n`. Set it to change what is written; it is written with the platform's line endings. Set to `null` or `""`, nothing is written. |
| `Html` | The [HTML](#html-on-the-clipboard) written next to the text, for `Selection` only; `null` otherwise, and when `IsHtmlCopyEnabled` is off or the platform has no HTML format. Set it to change or drop the HTML. |
| `Cancel` | Set to `true` to write nothing, for instance when the application writes the clipboard itself. A copy button then shows no confirmation. |

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
| Control of a [container renderer](custom-extensions.md#rendering-a-custom-container-as-your-own-control) | The markdown inside it, as anywhere else; the control's own text (e.g. an expander's header) is not selectable | The markdown inside it, or, when the control has its own `MarkdownSelection.CopyText`, that text as a whole block |

Hidden content is not selectable: the body of a collapsed [foldable alert](configuration.md#alert-blocks) (`> [!NOTE]-`) [details block](configuration.md#html-details-blocks) (`<details>`) or [heading section](configuration.md#foldable-headings) is skipped by hit-testing, highlighting and copy (`Ctrl+A` then `Ctrl+C` copies the text around it) until it is expanded. The same applies to any block a custom renderer hides with `IsVisible`. An alert's header and a details block's summary are not selectable; clicking one toggles its block instead of starting a selection.

Content scrolled out of view inside the document is treated the same way while it is out of view: the lines of a code block taller than [`CodeBlockMaxHeight`](configuration.md#line-numbers-wrapping-and-height) that lie above or below its scroll viewport are neither hit-tested nor highlighted, so pointing at the text right below such a block selects that text, not the block's hidden lines. They are still copied when the selection spans them.

Custom renderers opt their own non-text controls in through the `MarkdownSelection.CopyText` attached property — see [Content that is not text](custom-extensions.md#content-that-is-not-text).

[Finding text](find-in-document.md) searches the same text blocks and draws its matches on the same overlay, under the selection; the two are independent, so finding never changes the selection and selecting never clears the matches.

## How it works

`DocumentSelectionLayer` is a single transparent `Control` rendered in a single-cell `Grid` on top of the document. It tracks a start and end character offset across the entire flattened character index built by the renderer.

On `PointerMoved`, the layer calls `TextBlock.TextLayout.HitTestPoint` + `TranslatePoint` for each registered text block to find the nearest character offset, then redraws the selection highlight rectangles using `HitTestTextRange`. Both are limited to the part of each text block that its clipping ancestors inside the document (such as a code block's `ScrollViewer`) leave visible.

On copy, `GetSelectedText()` extracts the substring from each text block's registered text and joins them with newlines (tabs between table cells, a space after a list marker). Every line ending in the result is `\n`, including those inside code blocks and copy texts. The clipboard, both from `Ctrl+C` and from a code block's copy button, gets the platform's line endings instead (`Environment.NewLine`, so `\r\n` on Windows). A list item's marker is registered as its own text block, so it is highlighted with the item and selection offsets inside the item text line up with what is rendered.

A control embedded in a text block (`InlineUIContainer`) occupies one character position in its `TextLayout`, so it is registered as a U+FFFC placeholder that keeps selection offsets aligned with the layout. A block control with copy text is registered as an entry of its own, one placeholder long, whose bounds are hit-tested and highlighted as a whole. On copy, each placeholder is replaced by the control's `MarkdownSelection.CopyText`.
