---
title: "Task lists"
---

# Task lists

Task-list items (`- [ ]`, `- [x]`) show a static ☐ or ☑ by default. Set `AreTaskListsInteractive` to show checkboxes the reader can tick, for note-taking or to-do applications, and write each change back to your Markdown:

```xml
<mv:MarkdownViewer Markdown="{Binding Notes}" AreTaskListsInteractive="True" />
```

```csharp
viewer.TaskToggled += (_, e) =>
{
    var source = viewer.Markdown!;
    viewer.Markdown = string.Concat(
        source.AsSpan(0, e.SourceStart),
        e.IsChecked ? "[x]" : "[ ]",
        source.AsSpan(e.SourceStart + e.SourceLength));
    // ...and save it to wherever the Markdown came from.
};
```

Clicking a checkbox, or pressing Space or Enter while it has the focus, toggles it and raises `TaskToggled`, a routed event that bubbles like `LinkClicked`. **The viewer never edits its own Markdown**: the checkbox shows the new state, but `Markdown` is unchanged until the application writes the change back.

| `TaskToggledEventArgs` | |
|----------|-------------|
| `IsChecked` | The item's new state. |
| `SourceStart` | Zero-based index of the marker's `[` in the rendered Markdown. |
| `SourceLength` | Length of the marker, always `3` (`[ ]`, `[x]` or `[X]`). |
| `Line` | Zero-based line of the marker in the rendered Markdown. |

Positions refer to the Markdown the viewer rendered: the `Markdown` string exactly as set (the viewer's own rewriting of `=WxH` image size hints is accounted for), or the text loaded from `Source`.

The viewer applies each toggle to its own copy of the text the same way. When the text assigned back equals that copy, as in the handler above, the document is not rendered again, so the reader keeps their scroll position and focus. Any other text renders again and scrolls back to the top. A toggle that is never written back is lost at the next render, for example after setting another text or `Pipeline`.

- Pressing a checkbox and dragging selects text instead of toggling it, and a selection started on text can still be dragged across items.
- Copied text contains each checkbox's current ☐ or ☑.
- During a [streaming session](streaming.md), a task the reader toggled stays toggled across updates.

The checkbox (`ToggleButton.markdown-task-toggle`) has its own minimal control theme, so it looks like the static glyph rather than your application's `ToggleButton`: no padding, chrome or accent fill. The box is drawn rather than taken from a font, because browser builds only have the fonts the application embeds. Application-wide `ToggleButton` styles still apply to it; scope them with a class, or reset them on `ToggleButton.markdown-task-toggle`.
