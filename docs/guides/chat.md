---
title: "Chat and AI answers"
---

# Chat and AI answers

For a chat window, give each message its own viewer, include the Chat preset, and stream each answer into its viewer as it arrives:

```xml
<!-- App.axaml -->
<StyleInclude Source="avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml" />
<StyleInclude Source="avares://MarkView.Avalonia/Themes/ChatTheme.axaml" />
```

```csharp
async Task ShowAnswerAsync(MarkdownViewer viewer, IAsyncEnumerable<string> tokens)
{
    viewer.Markdown = "";
    using var session = viewer.BeginStreaming();

    await foreach (var token in tokens)
        session.Append(token);
}   // leaving the block completes the session
```

## One viewer per message

A viewer per message keeps each answer independent: a new answer never re-renders the earlier ones, and a viewer has one streaming session at a time. The costly parts are shared between viewers: the syntax highlighter (one per pair of TextMate themes, however many viewers use it), the [image cache](images.md#image-cache) and the cache of typeset formulas.

Set the pipeline and extensions once with `MarkdownViewerDefaults` rather than on each viewer; see [Settings for every viewer](displaying-documents.md#settings-for-every-viewer).

## The Chat preset

`ChatTheme.axaml` makes the document compact, in the style of AI chat applications: small headings, 8px between blocks, code in cards. It leaves the page to the host: the viewer keeps a transparent background and inherits the font, size and text colour of the bubble around it, and its tints are translucent, so they follow the bubble's colour. See [Themes and presets](themes.md#presets).

## Streaming an answer

`Append` can be called from any thread, for every token: the viewer renders what arrived at most once per interval (32 ms by default), and each update only renders the end of the document again. Until the text ends, the syntax left open at its end is closed for display, so `**bold` renders bold rather than with two asterisks, and links, images, diagrams and formula blocks that may still change wait until the text has moved past them.

```csharp
viewer.AutoScrollToEnd = true;   // follow the end; scrolling up pauses it
```

`IsStreaming` is true while a session is open, for a "typing" indicator. Setting `Markdown` ends the session, which is how to drop an answer that is no longer wanted. See [Streaming](streaming.md) for completing a session from another thread, what happens to the selection, find and folded blocks during a session, and what healing closes.

## Thinking blocks

Reasoning models (DeepSeek R1, Qwen, GLM and most open models) write their reasoning before the answer, between `<think>` and `</think>`. `UseThinkingBlocks()` shows it in a block that folds away instead of as part of the answer:

```csharp
viewer.UseThinkingBlocks();   // default pipeline + thinking blocks

// or in your own pipeline, with options
viewer.Pipeline = new MarkdownPipelineBuilder()
    .UseGitHubExtensions()
    .UseThinkingBlocks(new ThinkingBlockOptions { Title = "Reasoning", ExpandWhenDone = true })
    .Build();
```

```markdown
<think>
The user asks for **two** things:

- a summary
- an example
</think>

Here is the summary…
```

- The reasoning is Markdown, read like the rest of the document, even without blank lines around the tags. Each tag goes at the start of a line; the closing tag can also end the reasoning's last line (`…so the answer is 4.</think>`). A closing tag inside a fenced code block of the reasoning is code.
- The tags read by default are `think`, `thinking`, `reasoning`, `reason` and `thought`, in any case.
- Click the header, or focus it and press Enter or Space, to show or hide the reasoning. In a document that is not streamed, it starts collapsed under "Thoughts".
- In a [streaming session](streaming.md), the header reads "Thinking…" and the reasoning shows while it arrives. When the closing tag arrives, the block collapses and its header gives the time the reasoning took: "Thought for 12 seconds". A block the reader expanded or collapsed meanwhile keeps the state they chose. Only the reasoning's new paragraphs are rendered on each update, however long it gets.
- Reasoning without its closing tag runs to the end of the document: the model is still thinking, or its output was cut off. A complete block with no content (some models write `<think>\n\n</think>` when thinking is off) renders nothing.
- Collapsed reasoning behaves like every [folded block](folding.md): it cannot be selected or copied, and [find](find-in-document.md) expands it to show a match.

| `ThinkingBlockOptions` | Default | |
|---|---|---|
| `TagNames` | `think`, `thinking`, `reasoning`, `reason`, `thought` | The tags around the reasoning |
| `ThinkingTitle` | `"Thinking…"` | The header while a session receives the reasoning |
| `Title` | `"Thoughts"` | The header once the reasoning is complete, when no duration is shown |
| `DurationTitle` | `ThinkingBlockOptions.FormatDuration` | Formats the header from the time the reasoning took, when a session saw it start and end ("Thought for 12 seconds", "Thought for 2m 5s"); `null` to always show `Title` |
| `ExpandWhileThinking` | `true` | Whether the reasoning shows while it arrives |
| `ExpandWhenDone` | `false` | Whether the reasoning shows once complete, and in a document that is not streamed |

To translate the headers, set `ThinkingTitle`, `Title` and `DurationTitle`. Reasoning that a model API returns apart from the answer (a `reasoning` or `thinking` field) can be shown the same way by wrapping it in the tags. See [Style classes](../reference/style-classes.md#thinking-blocks) to restyle the block.

## Math and code

Models write math with `\(…\)` and `\[…\]` as often as with dollars. `UseMath()` (the [Math](math.md) package) reads both, and a formula block still being written shows as its source until it is complete.

Code blocks have a copy button; `ShowCodeBlockCopyButtonOnHover` shows it only while the pointer is over the block, as many chat applications do. See [Code blocks](code-blocks.md).

## Copying an answer

`CopyAsMarkdownAsync()` copies the selection as Markdown, and **Copy as Markdown** in the context menu does the same; with `SelectAll()` first, it copies the whole answer. On the desktop, `Ctrl+C` also puts the selection on the clipboard as HTML, so pasting into a word processor or another chat keeps the formatting. See [Selection and copy](text-selection.md).
