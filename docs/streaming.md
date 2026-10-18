# Streaming

`MarkdownViewer` can render markdown as it arrives, for text that grows at its end such as the
answer of a language model. A streaming session takes the text in pieces and re-renders only the
part of the document the new text can have changed, so the rest keeps its controls, its selection
and its scroll position.

## Starting a session

Clear the viewer, then call `BeginStreaming()`. It returns a `MarkdownStreamingSession`, which you
append text to and complete when the text ends:

```csharp
using MarkView.Avalonia;

async Task ShowAnswerAsync(MarkdownViewer viewer, IAsyncEnumerable<string> chunks)
{
    viewer.Markdown = "";
    using var session = viewer.BeginStreaming();

    await foreach (var chunk in chunks)
        session.Append(chunk);
}   // Dispose completes the session
```

`BeginStreaming` appends to the current `Markdown`. Set it to an empty string (or `null`) first to
stream a new document; leave it as it is to continue one. Call it on the UI thread. It throws
`InvalidOperationException` when a session is already active on the viewer, or when `Source` is
set: streaming appends to `Markdown`, not to a file or a URL.

Use one viewer per answer when you can, for example one per chat message. A viewer has one session
at a time, and a new one starts from the viewer's current text.

`IsStreaming` is true from `BeginStreaming()` until the session completes. It is a read-only,
bindable direct property, so a UI can show a "typing" indicator while it is true.

## Appending text

```csharp
session.Append("# Title\n\nSome ");
session.Append("text".AsSpan());
```

`Append` accepts a `string` or a `ReadOnlySpan<char>`. Appending an empty text does nothing, and a
`null` string throws `ArgumentNullException`. It throws `InvalidOperationException` once the
session is completed.

`Append` can be called from any thread. It only adds the text to a buffer, so it is cheap to call
for every token. The viewer renders the buffer at most once per update interval, on the UI
thread: all the text appended since the previous update is rendered together. A session with no
new text renders nothing and stops its timer until the next `Append`.

The interval is `MarkdownViewer.DefaultStreamingUpdateInterval`, 32 ms, unless you pass another one:

```csharp
// At most ten updates per second
var session = viewer.BeginStreaming(TimeSpan.FromMilliseconds(100));

// Append from a background thread, and complete the session when the text ends
_ = Task.Run(async () =>
{
    await foreach (var token in tokens)
        session.Append(token);
    await session.CompleteAsync();
});
```

Do not put the session in a `using` block when another thread appends to it: leaving the block
completes the session, and the next `Append` throws. Complete it where the text ends instead, or
await the task inside the block.

A longer interval renders less often and costs less CPU; a shorter one makes the text appear more
smoothly. The interval must be positive, or `BeginStreaming` throws `ArgumentOutOfRangeException`.

To react to each render, handle `session.Updated`. It is raised on the UI thread after each update,
the final one included.

## Completing

Call `Complete()` when the text has ended. It applies a final update with everything appended so
far and ends the session. Called on the UI thread, the final update is applied before `Complete`
returns; called from another thread, it is posted to the UI thread. Calling it again does nothing.

```csharp
session.Complete();

// Or wait until the final update is applied
await session.CompleteAsync();
```

`CompleteAsync()` returns a task that completes once the final update is applied. It never throws:
when an update throws, the task is faulted with that exception instead.

`Dispose()` does the same as `Complete()`, so a `using` block completes the session even when the
code that produces the text throws. If the final update itself throws, that exception comes out of
`Dispose()`; call `Complete()` or `CompleteAsync()` where you want to observe it. `IsCompleted` is
true as soon as the session accepts no more text.

When you complete a session from a background thread, await `CompleteAsync()` before calling
`BeginStreaming()` again: `Complete()` only posts the final update, and the session stays active
until it lands, so a second `BeginStreaming()` before then throws.

A session also ends, silently and without a final update, when the viewer renders something else:
setting `Markdown`, `Source`, `Pipeline` or a render option such as `BaseUri` or
`ShowCodeBlockLineNumbers`, or calling `UseFenceRenderer`. `IsCompleted` becomes true,
`IsStreaming` becomes false, and a later `Append` throws. Setting `Markdown` yourself is how you
drop an answer that is no longer wanted.

Adding to `Extensions` does not end a session, and does not reach it either: the session's
renderer is created by its first update. Add extensions before `BeginStreaming()`.

If a renderer or an `Updated` handler throws during an update, the session ends too. The exception
is thrown from the call that applied the update, and `CompleteAsync()`'s task is faulted with it.
An update applied by the timer, or posted from another thread, reaches the UI thread's unhandled
exception handling instead.

## What the viewer does during a session

- **`Markdown` and bindings.** After each update, `Markdown` holds the text received so far. It is
  set without replacing a binding, so a binding on `Markdown` stays in place. The viewer does not
  render the text a second time.
- **Content.** The viewer's content is installed once per session: the first update replaces the
  previous document, which closes an image lightbox that was open. Later updates change that same
  document.
- **Scroll position.** An update never resets the scroll offset, unlike setting `Markdown`.
- **`AutoScrollToEnd`.** Set it to `true` to keep the end of the document in view as text arrives.
  Scrolling up pauses following, so the user can read earlier text while the answer goes on;
  scrolling back to the bottom resumes it. Each new session starts following again. It has no
  effect outside a session, and it defaults to `false`.
- **Selection.** A selection in text that was kept survives the update. A selection that reaches
  into the re-rendered tail is clamped to the kept text; one inside the tail is cleared. See
  [text selection](text-selection.md).
- **Find.** An active [search](find-in-document.md) is run again after each update: the matches
  and highlights are refreshed, and the current match is kept.
- **Task lists.** A task the user toggled stays toggled across updates, when
  `AreTaskListsInteractive` is on.
- **Focus.** Keyboard focus inside a block that is re-rendered returns to the viewer, as the
  control that had it is replaced.
- **Screen readers.** Assistive technology gets one children-changed notification when the
  session completes, not one per update. It is sent through the viewer's own automation peer; a
  peer that is not a `MarkdownViewerAutomationPeer` gets none.

```csharp
viewer.AutoScrollToEnd = true;
```

## Healing

Text that stops in the middle of a construct parses badly: `` `code `` shows a backtick and the
words after it as plain text until the second backtick arrives, and `**bold` shows two asterisks.
Between updates the viewer closes what is left open at the end of the text, so the text renders as
it will once complete.

Healing is a step of each update, before the parse is rendered:

1. The text is parsed to find the block that contains its end: the last block, unless a blank
   line follows it.
2. If that block ends with open syntax, the closers are added to the text, before its trailing
   whitespace, and the text is parsed again. The second parse only happens when closers were added.
3. The result is rendered.

Healing only ever adds characters, and only to what is parsed. `Markdown` always holds the text as
it was received, and the final update, the one `Complete()` applies, renders the text as it is,
without healing. Only a paragraph or an ATX heading (`# Title`) at the end of the text is healed:
the text of other blocks, a code block or an HTML block for instance, is left as it is, and so is
everything before the last block.

What is healed:

- **Code spans.** An unmatched run of backticks is closed with a run of the same length, so
  `` `code `` becomes `` `code` ``. A run is left as it is when the text ends with a
  backtick (a run with nothing after it, for instance), as the closer would merge with it into a
  longer run that closes nothing.
- **Link and image addresses.** A link or image whose text is closed but whose address is not,
  such as `[docs](https://example.com/a(b` or `![logo](logo.png "The`, gets the closing quote of
  its title if one is open and the closing parentheses it needs. Parentheses nest in the address.
- **Emphasis and strikethrough.** `*`, `_` and `~~` runs that open and are not closed get their
  closing run, following CommonMark's flanking rules: in short, a run opens when it is followed by
  a non-space character and closes when it is preceded by one, and `_` does neither inside a word.
  `snake_case` and `2 * 3` stay as they are. A run with nothing after it opens nothing, so it
  gets no closer. A last line made only of `*`, `_` or `~` (after a quote marker, if any) is left as
  it is, since closers would turn it into a thematic break or a fence. Nested emphasis is closed innermost first. Only a run of exactly two `~`
  is strikethrough; other runs of `~`, and `^`, `+` and `=`, are left as they are.
- **Bracket math.** With the Math package's bracket delimiters enabled (`UseMath()` does, unless
  you pass `bracketDelimiters: false`), `\(` and `\[` that are not closed get `\)` and `\]`, the
  rest of the text being the formula. Without the bracket delimiters they are plain escapes and
  stay as they are. A `\(` with nothing after it is not closed.

What is never healed:

- A bracket that has no address yet, as in `[text` or `[text]`: it may become a link, a reference
  link, a footnote or a task list item, and the viewer does not guess.
- An HTML tag or an autolink that is not closed, such as `<span class="a` or `<https://exa`. What
  comes after it is not healed either, since it may be part of the tag.
- A reference link or a footnote marker (`[text][ref]`, `[^note]`).
- `$`, with or without the math extension: an unmatched `$` stays as it is, since `$5` is a price.
  With the math extension on, `$…$` and `$$…$$` pairs are not scanned for the syntax inside them.
- A paragraph that ends with an unescaped backslash: a closer written after it would be escaped.

Healing is on by default. Pass `healIncompleteSyntax: false` to render the text as it is:

```csharp
using var session = viewer.BeginStreaming(healIncompleteSyntax: false);
```

The healer recognises Markdig's math extension and the Math package's bracket delimiters from the
viewer's pipeline when the session starts. Set up the pipeline and the extensions before
`BeginStreaming()`.

## Provisional rendering

A link or an image can still change when the text ends inside it: an address that has not fully
arrived points somewhere else, or nowhere. During an update, the viewer renders the links and
images of the block that contains the end of the text as provisional:

- **Links** keep their text and their style, but have no address and no Tab stop, so clicking or
  tabbing to a link that is not complete does nothing.
- **Images** show their alt text, and nothing is requested: a half-written address is never
  fetched.

Provisional applies to every `[text](address)` link and image of that block, bare URLs turned into
links by the pipeline included, even the ones that were already complete in it. An angle-bracket
autolink (`<https://…>`) or a footnote reference keeps its address, since it is only parsed once
complete. A block that was rendered as provisional is never kept as it is, so the next
update renders it again. Once the text has moved past it, with a blank line or another block, that
update renders it for real, with the addresses and the images. The final update, from
`Complete()`, renders nothing as provisional.

## How rendering works

An update does not patch the previous rendering with a diff of the text. It parses the whole text
again and renders from the first block that can have changed.

1. The whole text is parsed. Markdig has no incremental parser.
2. The top-level blocks that cannot have changed keep their controls. Only the last top-level block
   can grow with appended text, so every block before it is closed.
3. The last block, and anything after the first changed block, is rendered again, along with the
   selection, link focus and heading entries it contributes.

A block is kept when it has the same type and the same source span as when it was rendered. Some
blocks stay open until they end:

- A list, a quote or a table is one top-level block. While it is the last block, it is rebuilt
  whole on each update, however many items it holds, because a new item or row extends it. It is
  kept once it closes.
- Markdig reports some tables open after they end. The viewer does not rely on that flag for
  blocks before the last one, so a finished table is kept.
- A link reference definition, a footnote or an abbreviation anywhere in the text can change how
  an earlier block renders. When a definition's label, URL or title, or an abbreviation, changes,
  the whole document is rendered again.

The cost per update is small. These figures come from measurements of a prototype, and depend on
the document and the machine, so read them as orders of magnitude:

- Parsing the whole text takes about 1 to 3 ms for 20 KB.
- On a chat-sized answer, an update takes roughly 0.5 ms at the median. On a README-sized
  document it takes roughly 1 to 1.4 ms, rising to about 10 to 13 ms only at the end of its
  longest code blocks, where the open block is the large one.
- An update allocates about 0.2 MB for a chat-sized answer and about 1 MB for a README, against
  roughly 10 to 18 MB to render the whole document again.
- During a session the viewer holds the renderer, the last parsed document and text, and a record
  of a few hundred bytes per block. All of it is released when the session ends.

Without a session, nothing changes: setting `Markdown` renders the document as before.

## For custom renderers

A [custom renderer](custom-extensions.md) runs once for each block it renders, including the open
tail on every update. Two members of `AvaloniaRenderer` tell it that the text may end in the middle
of that block:

- `IsStreaming` is true for every update of a session but the final one. It is false when the
  document is not streamed.
- `IsProvisional(MarkdownObject)` tells whether a block or an inline contains the end of the text so
  far: the block that contains it, and every block around it (a heading, or a paragraph and the
  list item and list that hold it). An inline is judged by its leaf block. A block is no longer
  provisional once a blank line or another block follows it. It always returns false when
  `IsStreaming` is false, so a renderer can call it without checking.

A fenced code block is provisional while its fence is open, and also just after the fence closes,
until a blank line or another block follows. A heading is provisional until the same happens.
A block that was rendered as provisional is rendered again on the next update.

In this sketch `MyChart` stands for your own control. See
[custom extensions](custom-extensions.md) for how a renderer is registered.

```csharp
using Avalonia.Controls;
using Markdig.Syntax;
using MarkView.Avalonia.Rendering;

public class ChartBlockRenderer : AvaloniaObjectRenderer<FencedCodeBlock>
{
    protected override void Write(AvaloniaRenderer renderer, FencedCodeBlock obj)
    {
        var source = obj.Lines.ToString();

        // The fence may be open, or the answer may stop here: show the text, not a chart.
        Control content = renderer.IsProvisional(obj)
            ? new TextBlock { Text = source }
            : new MyChart(source);

        renderer.WriteBlock(content);
    }
}
```

Provisional rendering of Mermaid diagrams and formulas arrives in a later release.
