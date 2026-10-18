# Find in Document

`MarkdownViewer` can search its rendered document for a piece of text, highlight every match, and
move from one match to the next, scrolling each into view. The control has no find bar of its own:
the application provides the text box and buttons, and binds them to the API below.

## API

```csharp
// Highlight every match; returns the number of matches
int count = viewer.Find("markdown");
int exact = viewer.Find("Markdown", MarkdownFindOptions.MatchCase | MarkdownFindOptions.WholeWord);

// Make the next / previous match current and scroll it into view (wraps around)
viewer.FindNext();       // false when there is no match
viewer.FindPrevious();

// Remove the search and its highlights
viewer.ClearFind();

// Read-only, bindable
int matches = viewer.FindMatchCount;          // 0 without an active search
int current = viewer.CurrentFindMatchIndex;   // zero-based, -1 when no match is current
```

`Find` replaces the previous search and makes no match current: `FindNext` then goes to the first
match and `FindPrevious` to the last one. A find bar that jumps to the first match as the user
types calls both:

```csharp
findBox.TextChanged += (_, _) =>
{
    viewer.Find(findBox.Text ?? string.Empty);
    viewer.FindNext();
};
```

An empty text clears the search, as `ClearFind` does.

`FindMatchCount` and `CurrentFindMatchIndex` are Avalonia direct properties, so a find bar can bind
to them or watch them change:

```xml
<TextBlock Text="{Binding #Viewer.FindMatchCount, StringFormat='{}{0} matches'}" />
```

```csharp
viewer.PropertyChanged += (_, e) =>
{
    if (e.Property == MarkdownViewer.FindMatchCountProperty || e.Property == MarkdownViewer.CurrentFindMatchIndexProperty)
        status.Text = $"{viewer.CurrentFindMatchIndex + 1} of {viewer.FindMatchCount}";
};
```

The [demo application](../samples/MarkView.Avalonia.Demo) has such a find bar: `Ctrl+F` focuses it,
`Enter` and `Shift+Enter` go to the next and previous match, and `Esc` clears the search.

## Options

| `MarkdownFindOptions` | Effect |
|---|---|
| `None` (default) | Case-insensitive, ordinal (no culture-specific rules), matches inside words too |
| `MatchCase` | Only text with the same case (ordinal comparison) |
| `WholeWord` | Only text not preceded or followed by a letter, a digit or an underscore: `cat` finds "cat" and "cat's", not "concat" or "cats" |

The options combine as flags. Matches never overlap: `aa` finds two matches in "aaaaa".

## What is searched

The search covers the same text as [text selection](text-selection.md#what-is-selectable):
paragraphs, headings, code blocks, blockquotes, list items, table cells and footnotes.

- **A match never spans two blocks.** Each paragraph, heading, code block, list item and table
  cell is searched on its own, as browsers search within table cells: `a b` does not match the
  end of one paragraph and the start of the next, nor two cells of a row. A match inside a block can
  be laid out on two lines when the block wraps; it is then highlighted on both.
- **The text of controls is not searched.** An image's alt text, the source of a formula
  (`MarkView.Avalonia.Math`) or a Mermaid diagram (`MarkView.Avalonia.Mermaid`), and any other
  control's [`MarkdownSelection.CopyText`](custom-extensions.md#content-that-is-not-text) are what
  selection copies in their place, not text on the page, so they are never matched.
- **Not searched either:** list markers (bullets, numbers and task-list glyphs), the header of an
  alert and the summary of a details block (neither is selectable), and a code block's toolbar and
  line numbers.
- **Collapsed content is searched.** Matches in the body of a collapsed
  [foldable alert](configuration.md#alert-blocks) or [details block](configuration.md#html-details-blocks)
  count in `FindMatchCount` but are not highlighted while hidden. Moving to such a match expands
  every collapsed block around it before scrolling to it, as browsers open `<details>` on find.

## Scrolling

`FindNext` and `FindPrevious` scroll the current match into view in the viewer's
`PART_ScrollViewer` with as little scrolling as possible, keeping 16 pixels between the match and
the top or bottom of the viewport. A match already in view, with that margin, does not scroll the
document. A match in a code block taller than
[`CodeBlockMaxHeight`](configuration.md#line-numbers-wrapping-and-height) first scrolls the code
block itself, then the document to that code block.

## When the document changes

The search stays active until `ClearFind` (or `Find` with an empty text). When the document is
rendered again (`Markdown` or `Source` changes, or a property that re-renders it such as
`ShowCodeBlockLineNumbers`), the same text is searched with the same options in the new document:

- `FindMatchCount` is updated, and so are the highlights;
- `CurrentFindMatchIndex` keeps its value when the new document has that many matches, becomes
  the last match's index when it has fewer, and stays -1 when no match was current;
- the viewer does not scroll to the current match (a new document starts at the top), nor expand
  anything: the next `FindNext` or `FindPrevious` does.

A theme switch does not render the document again; the highlights change colour with it. Resizing
the viewer, or any other change of layout (wrapping, expanding a block, scrolling a code block),
moves the highlights with the text.

## Find and selection

Find and [selection](text-selection.md) are independent: finding or moving to a match never
changes the selection, and `ClearSelection`, `SelectAll` or a click never clear the matches. A
selection is drawn over the match highlights, so both stay visible.

## Highlight colours

Matches are drawn by the same overlay as the selection, with one brush for every match and a
stronger one for the current match. The highlight is opaque and the text it covers is drawn again
on top of it, in its own colours. Override the brushes in your application's resources, for the
Light and Dark theme variants:

| Resource key | Dark | Light | Use |
|---|---|---|---|
| `MarkdownFindMatchBackground` | `#453A00` | `#FFF176` | Every match but the current one |
| `MarkdownFindCurrentMatchBackground` | `#652A00` | `#FFC584` | The current match |

The default colours keep the default text readable on them, above the 4.5:1 WCAG AA minimum:
the body text, code blocks (`MarkdownCodeBlockForeground`) and inline code
(`MarkdownCodeInlineForeground` on `MarkdownCodeInlineBackground`).

| Text | `#453A00` | `#652A00` | `#FFF176` | `#FFC584` |
|---|---|---|---|---|
| Body text (Fluent theme) | 11.3:1 | 11.1:1 | 15.1:1 | 11.7:1 |
| Code blocks | 7.6:1 | 7.5:1 | 14.4:1 | 10.8:1 |
| Inline code | 4.7:1 | 4.7:1 | 6.2:1 | 4.7:1 |

Text in another colour, such as links (`MarkdownLinkForeground`) or the tokens of a
[syntax-highlighted](extensions.md) code block, keeps that colour on the highlight, with whatever
contrast it gives. Without `MarkdownTheme.axaml` the viewer uses the same colours.
