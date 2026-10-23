---
title: "Live preview"
---

# Live preview

By default, every new `Markdown` text is rendered from scratch: the viewer parses it, builds a new
control tree and scrolls back to the top. That is the right behaviour for a viewer that shows one
document after another, but not for a live preview next to an editor, where the text changes on
every keystroke and the reader wants to stay where they are.

Set `IsIncrementalRenderingEnabled` to render a new text incrementally, re-rendering only the top-level blocks a new text changes:

```xml
<TextBox Text="{Binding Source}" AcceptsReturn="True" />
<mv:MarkdownViewer Markdown="{Binding Source}" IsIncrementalRenderingEnabled="True" />
```

```csharp
viewer.IsIncrementalRenderingEnabled = true;
viewer.Markdown = editor.Text;   // on each change
```

It is off by default.

## What is kept

The blocks before the first one that changed and after the last one that changed keep their
controls. Only the blocks in between are rendered again. So, from one text to the next:

- **Scroll position.** The text in view stays where it is. An edit above it that changes the height
  of the blocks rendered again moves the scroll offset by as much, as for content that grows above
  the view (see [lazy rendering](displaying-documents.md#lazy-rendering)).
- **Content already realized.** Images, formulas and code colours that [lazy rendering](displaying-documents.md#lazy-rendering)
  built for the kept blocks stay, so they do not flash or load again on every keystroke.
- **Selection.** A selection in kept blocks stays on the same text, even when an edit before it
  moves that text. An end of the selection in a block that was rendered again moves to the end of
  the last kept block before it; a selection entirely inside such blocks is cleared. See
  [text selection](text-selection.md).
- **Find.** An active [search](find-in-document.md) is run again, as after any new text.
- **Interactive state.** Folded alerts, `<details>`, headings and thinking blocks, toggled tasks,
  keyboard focus and an open image lightbox stay as they are, in the blocks that are kept. A block
  rendered again after an edit before it keeps its fold state too, as it does in a streaming session.
- **Content.** `Content` stays the same `Grid`; its root panel's children are replaced in place.

A task the user toggles raises `TaskToggled` as usual. When the application writes the change back
to `Markdown`, the block that holds the task is kept, as it already shows the new state. Its
`SourceStart` and `Line` always refer to the current text, also for a task in a block kept after an
edit moved it, and so does the source that [copying as Markdown](text-selection.md#copying-as-markdown) reads for a
kept block.

## When everything is rendered again

- The first text after the property is turned on, and the first text after an empty one.
- Any change to a property that affects rendering (`Pipeline`, `BaseUri`, `ImageResizeMode`, the
  code block options, `AreTaskListsInteractive`, `IsImageZoomEnabled`), and `Source`. These keep
  their usual behaviour, including the scroll back to the top.
- A text in which a link reference definition, a footnote label or an abbreviation changed, since
  any block can depend on them. The blocks are rendered again, but the scroll position is kept.

A text that has no top-level block in common with the previous one is taken as another document:
it is rendered from scratch and scrolls back to the top. A document of a single block (one long
list, say) has nothing in common with itself after any edit, so it keeps its scroll position.

## Streaming

[Streaming sessions](streaming.md) work as before. With the property on, the document a session
rendered stays in place: the text set after the session completes, or the direct `Markdown` set
that ends it, is rendered incrementally. A session started on a document that is already shown
appends to it without rendering it again.

## Cost

Each new text is parsed whole (2 to 7 ms for 26 KB, most of what an update costs; Markdig has no
incremental parser), then only the changed blocks are rendered and laid out. Typing 100 characters
one at a time into the repository's README (26 KB) in a 900 × 700 window measured, per keystroke,
including layout (median):

| Where the typing happens | From scratch | Incremental |
|---|---|---|
| A paragraph at 10% and 50%, a code block at 90% of the document | 190 to 215 ms, 21 MB allocated | 3 to 19 ms, 0.7 to 0.8 MB |
| A heading at 50% | 195 ms | 1.7 to 2.6 ms |
| The end of the document | 195 ms | 1.9 to 2.2 ms |
| The same, with TextMate highlighting | 195 to 235 ms, 22 MB | 1.4 to 16 ms, 0.6 to 0.8 MB |

These are orders of magnitude: they depend on the document and the machine.

## How it works

Each top-level block remembers where its source was, how many controls, selection entries, link
targets, anchors and headings it produced, and the syntax tree it was rendered from, which its
controls read source positions from later (a task's paragraph, a fold state, the source copied as
Markdown). A new text is parsed whole, then:

1. The leading blocks with the same type and the same source as before are kept.
2. Then the trailing blocks with the same type and the same source, wherever that source now
   starts, are kept as well. Their syntax trees move by what changed before them, so a task in one
   of them reports where it now is.
3. The blocks in between are rendered, and their controls, selection entries and link targets take
   the place of the old ones.

A block's source is its own text and the text up to the next block, but for blank lines: Markdig
moves some definitions out of the block tree, and leaves the span of a fence that is still open
at the end of the text on its first line. Two cases need more than the source:

- **Headings.** A kept heading's slug depends on the headings before it: adding a second
  `# Usage` makes the later `# Usage` `usage-1`. A kept block whose headings would get a different
  slug is rendered again.
- **Footnotes.** Markdig moves footnote definitions into one group at the end of the document. The
  group is rendered again with every text.

The unit of reuse is the top-level block. A list, a quote or a table is one block, so an edit in
one of its items renders the whole list again. A thinking block at the end of the text is the
exception, as in a streaming session: an edit in it renders only its children that changed.

Updates are synchronous: the viewer renders each `Markdown` set before the property setter returns.
An application that sets the text on every keystroke of a very large document can throttle the
sets itself.
