# Accessibility

`MarkdownViewer` exposes the structure of the rendered document to screen readers, and lets keyboard users reach and open every link and interactive element. Neither changes how the document looks or behaves with the mouse.

## Screen readers

Avalonia forwards automation peers to each platform's accessibility API: UI Automation on Windows, NSAccessibility on macOS, and AT-SPI on Linux. Each rendered element reports the following role:

| Markdown | Reported as |
|---|---|
| The viewer itself | `Document`, named by `AutomationProperties.Name` or, if that isn't set, by the first heading |
| `#` … `######` | `Text` with `AutomationProperties.HeadingLevel` 1–6 |
| Paragraph | `Text` |
| Link | `Hyperlink`, a child of its paragraph, named by its text and invokable. An image inside a link, such as a badge, is read as its alt text. A link title (`[text](url "title")`) is its help text. |
| List / list item | `List` / `ListItem`. Bullets are hidden; numbers stay readable, since an ordered list can start at any number. |
| Interactive task (`AreTaskListsInteractive`) | `CheckBox`, named by the task's text, with its checked state |
| Table / header cell / body cell | `Table` / `HeaderItem` / `DataItem` |
| Blockquote, figure | `Group`. A figure is named by its caption. |
| Alert (`> [!NOTE]`) | `Group`, named by its title ("NOTE", "TIP", …), so the alert kind is announced as text and not only shown by colour |
| `<details>` (`UseHtmlDetails()`) | `Group`, named by its summary |
| Code block line numbers | Hidden, so only the code is read |
| Thematic break | `Separator` |
| Image | `Image`, named by its alt text. An image with empty alt text (`![](…)`) is decorative and hidden, as browsers do with `alt=""`, unless it can be zoomed. |
| Zoomable image (`IsImageZoomEnabled`) | `Image`, named by its alt text, with the help text "Zoom". A Tab stop, so never hidden, even without alt text. |
| Image lightbox | `Image`, named by the image's alt text, or "Image" without one |
| Image that could not be loaded | `Image`, named by its alt text, with the help text "Image could not be loaded"; hidden without alt text |
| `<kbd>` (`UseHtmlSubset()`) | `Text`, read as the key's text |
| YouTube embed | `Hyperlink`, named by its alt text, or by its URL when there is none |
| Math formula (`MarkView.Avalonia.Math`) | `Image`, named by its TeX source |
| Mermaid diagram (`MarkView.Avalonia.Mermaid`) | `Image`, named by its diagram source |

The help texts and the lightbox's fallback name are set by `MarkdownTheme.axaml` styles (`Image.markdown-image-zoomable`, `Border.markdown-image-failed`, `.markdown-image-lightbox`), as is the copy button's name, so they can be translated.

To give the document a name of your own, set `AutomationProperties.Name`:

```xml
<mv:MarkdownViewer AutomationProperties.Name="Release notes" Source="avares://MyApp/notes.md" />
```

The paragraph itself is still read as one string, links included.

### Platform caveats

Avalonia 12.1.3 has the following gaps:

- **Linux.** The AT-SPI bridge does not report heading levels, so headings read as plain text.
- **Browser (WebAssembly).** Avalonia ships no screen reader support.

## Keyboard

The viewer is one Tab stop, followed by each link and each focusable control in the document, in reading order: a foldable alert's header, a `<details>` summary, a code block's copy button, an interactive task's check box, a zoomable image, a video embed. `Enter` or `Space` folds or unfolds a focused alert or `<details>` block and ticks a focused task; `Enter` or `Space` on the copy button copies the code, and on an image opens it in the lightbox. In the lightbox, `Tab` moves between the lightbox and its close button only, and `Escape` closes it and gives the focus back to the image.

| Key | Action |
|---|---|
| `Tab` | Move to the next link. Past the last one, focus leaves the viewer. |
| `Shift+Tab` | Move to the previous link, then back to the viewer. Entering the viewer this way lands on its last link. |
| `Enter` | Open the focused link, as a click does: `#anchor` links scroll to their target, and other links raise `LinkClicked`. |

The focused link scrolls into view, and a 2px ring outlines each line of its text. The ring uses FluentTheme's `SystemControlFocusVisualPrimaryBrush`, or black/white without FluentTheme. Links in hidden content, such as a collapsed block, are skipped.

Links stay inline text, so they wrap and select as before. An invisible stand-in over each link takes the focus instead.
