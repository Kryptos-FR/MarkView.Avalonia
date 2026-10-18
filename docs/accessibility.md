# Accessibility

`MarkdownViewer` exposes the structure of the rendered document to screen readers, lets keyboard users reach and open every link and interactive element, ships default colours that meet WCAG 2.2 AA contrast, and switches to a high-contrast palette when the operating system asks for one. Screen reader and keyboard support don't change how the document looks or behaves with the mouse.

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
| Fold button of a heading (`AreHeadingsFoldable`) | `Button`, named by the heading's text, that expands and collapses (whether the section is expanded is reported) |
| Table / header cell / body cell | `Table` / `HeaderItem` / `DataItem` |
| Blockquote, figure | `Group`. A figure is named by its caption. |
| Alert (`> [!NOTE]`) | `Group`, named by its title ("NOTE", "TIP", …), so the alert kind is announced as text and not only shown by colour |
| `<details>` (`UseHtmlDetails()`) | `Group`, named by its summary |
| Code block line numbers | Hidden, so only the code is read |
| Thematic break | `Separator` |
| Image | `Image`, named by its alt text. An image with empty alt text (`![](…)`) is decorative and hidden, as browsers do with `alt=""`, unless it can be zoomed. |
| Zoomable image (`IsImageZoomEnabled`) | `Image`, named by its alt text, with the help text "Zoom". A Tab stop, so never hidden, even without alt text. |
| Zoomable Mermaid diagram | As a zoomable image, named by its diagram source |
| Image lightbox | `Image`, named by the image's alt text (a diagram's source), or "Image" without one. Its buttons are named "Close", "Zoom out", "Zoom in" and "Fit to view". |
| Image that could not be loaded | `Image`, named by its alt text, with the help text "Image could not be loaded"; hidden without alt text |
| `<kbd>` (`UseHtmlSubset()`) | `Text`, read as the key's text |
| YouTube embed | `Hyperlink`, named by its alt text, or by its URL when there is none |
| Math formula (`MarkView.Avalonia.Math`) | `Image`, named by its TeX source |
| Mermaid diagram (`MarkView.Avalonia.Mermaid`) | `Image`, named by its diagram source |

The help texts and the lightbox's fallback name are set by `MarkdownTheme.axaml` styles (`Image.markdown-image-zoomable`, `Border.markdown-image-failed`, `.markdown-image-lightbox`), as are the copy buttons' names ("Copy code", "Copy formula"), so they can be translated.

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

The viewer is one Tab stop, followed by each link and each focusable control in the document, in reading order: a foldable alert's header, a `<details>` summary, a foldable heading's fold button, a code block's or a display formula's copy button, an interactive task's check box, a zoomable image or Mermaid diagram, a video embed. `Enter` or `Space` folds or unfolds a focused alert, `<details>` block or heading section and ticks a focused task; `Enter` or `Space` on a copy button copies the code or the formula's TeX source, and on an image opens it in the lightbox. In the lightbox, `Tab` moves between the lightbox, its close button and its zoom buttons only; `+` and `-` zoom, `0` fits the picture again, the arrow keys pan it, and `Escape` closes the lightbox and gives the focus back to the image.

| Key | Action |
|---|---|
| `Tab` | Move to the next link. Past the last one, focus leaves the viewer. |
| `Shift+Tab` | Move to the previous link, then back to the viewer. Entering the viewer this way lands on its last link. |
| `Enter` | Open the focused link, as a click does: `#anchor` links scroll to their target, and other links raise `LinkClicked`. |
| Menu key | Open the viewer's [context menu](text-selection.md#context-menu); on a focused link, it offers to copy the link's address. |

The focused link scrolls into view, and a 2px ring outlines each line of its text. The ring uses FluentTheme's `SystemControlFocusVisualPrimaryBrush`, or black/white without FluentTheme. Links in hidden content, such as a collapsed block, are skipped.

Links stay inline text, so they wrap and select as before. An invisible stand-in over each link takes the focus instead.

## Colour contrast

The default palette in `MarkdownTheme.axaml` meets WCAG 2.2 AA on FluentTheme's page colours (white with black text, and black with white text):

- **4.5:1** for text: code, code block line numbers, inline code, the code block header (language label and copy icon), `==marked==` text, alert titles and alert bodies, `<kbd>` keys, the text of an image that could not be loaded, and body text, code and inline code on [find](find-in-document.md#highlight-colours) highlights.
- **3:1** for borders and icons that carry meaning (1.4.11): blockquote bars, table cell borders, the outlines of `<kbd>` keys and of an image that could not be loaded, the image lightbox's close button, and a display formula's copy icon, which sits on the page rather than on the code block header.

`ThemeContrastTests` checks every pair against the colours in the theme file, so a brush change that drops below these ratios fails the build.

Links use `MarkdownLinkForeground`, a shade of the OS accent colour rather than the accent itself:
- `SystemAccentColorDark2` in Light;
- `SystemAccentColorLight2` in Dark.

This follows WinUI, and keeps links above 4.5:1 for typical accent colours. Very light accents, such as gold or yellow, can still fall slightly short in Light. Links are always underlined, so they never rely on colour alone. These shades come from FluentTheme; with another base theme, define `MarkdownLinkForeground` yourself.

## High contrast

Avalonia reports whether the operating system asks for high contrast. It detects this on Windows, on macOS ("Increase contrast") and in the browser (`prefers-contrast: more`), but not on Linux. It ships neither a high-contrast theme nor the OS contrast colours, so `MarkdownViewer` brings its own palette. When high contrast is on, the viewer gets the `:high-contrast` pseudo-class, and `MarkdownTheme.axaml` then:

- paints the viewer in solid `MarkdownHighContrastBackground` / `MarkdownHighContrastForeground`;
- draws links in `MarkdownHighContrastLink`, and `==marked==` text in `MarkdownHighContrastHighlightForeground` on `MarkdownHighContrastHighlightBackground`;
- gives code blocks (their header included), tables, blockquotes, figures, separators and alerts solid `MarkdownHighContrastBorder` borders, and draws the code, its line numbers and the header's label and copy icon in `MarkdownHighContrastForeground`;
- draws the placeholder of an image that could not be loaded in `MarkdownHighContrastForeground` on `MarkdownHighContrastBackground`, with a `MarkdownHighContrastBorder` outline;
- drops the per-kind alert colours (the titles carry the kind) and all opacity dimming.

| Resource key | Dark | Light |
|---|---|---|
| `MarkdownHighContrastBackground` | `#000000` | `#FFFFFF` |
| `MarkdownHighContrastForeground` | `#FFFFFF` | `#000000` |
| `MarkdownHighContrastLink` | `#FFFF00` | `#00009F` |
| `MarkdownHighContrastHighlightBackground` | `#1AEBFF` | `#37006E` |
| `MarkdownHighContrastHighlightForeground` | `#000000` | `#FFFFFF` |
| `MarkdownHighContrastBorder` | `#FFFFFF` | `#000000` |

Every pair is at least 7:1, as Windows' contrast themes are, and `ThemeContrastTests` checks it. The [GitHub preset](theming.md#github) redefines these keys with Primer's high-contrast colours. The column used follows the theme variant Avalonia reports along with the contrast setting. On Windows, Avalonia only reports Light for contrast themes whose name contains "White". Override any of these keys in your own resources to match your app's contrast palette.

`HighContrastMode` controls when the palette is used:

| Value | Behaviour |
|---|---|
| `System` (default) | Follow the operating system, and update live when the user turns high contrast on or off. |
| `Enabled` | Always use the high-contrast palette, e.g. for an in-app "high contrast" toggle. |
| `Disabled` | Never use it. |

`IsHighContrast` reports whether the palette is currently in use. Style your own controls against the same pseudo-class:

```xml
<Style Selector="mv|MarkdownViewer:high-contrast TextBlock.my-caption">
  <Setter Property="Opacity" Value="1" />
</Style>
```

Syntax-highlighted code keeps its token colours in high contrast. The Dark+ and Light+ themes already contrast with black and white backgrounds. Mermaid diagrams keep their regular palette. [Find](find-in-document.md) highlights keep their colours too; the default palette's high-contrast text and links stay above 7:1 on them.
