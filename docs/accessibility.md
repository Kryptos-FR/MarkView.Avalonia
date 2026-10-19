# Accessibility

`MarkdownViewer` exposes the structure of the rendered document to screen readers, lets keyboard users reach and open every link and interactive element, follows the text size chosen in the system settings, ships default colours that meet WCAG 2.2 AA contrast, and switches to a high-contrast palette when the operating system asks for one. Screen reader and keyboard support don't change how the document looks or behaves with the mouse.

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
| Zoomable Mermaid diagram | As a zoomable image, named by its diagram source |
| Image lightbox | `Image`, named by the image's alt text (a diagram's source), or "Image" without one. Its buttons are named "Close", "Zoom out", "Zoom in" and "Fit to view". |
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

The viewer is one Tab stop, followed by each link and each focusable control in the document, in reading order: a foldable alert's header, a `<details>` summary, a code block's copy button, an interactive task's check box, a zoomable image or Mermaid diagram, a video embed. `Enter` or `Space` folds or unfolds a focused alert or `<details>` block and ticks a focused task; `Enter` or `Space` on the copy button copies the code, and on an image opens it in the lightbox. In the lightbox, `Tab` moves between the lightbox, its close button and its zoom buttons only; `+` and `-` zoom, `0` fits the picture again, the arrow keys pan it, and `Escape` closes the lightbox and gives the focus back to the image.

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
- **3:1** for borders that carry meaning (1.4.11): blockquote bars, table cell borders, the outlines of `<kbd>` keys and of an image that could not be loaded, and the image lightbox's close button.

`ThemeContrastTests` checks every pair against the colours in the theme file, so a brush change that drops below these ratios fails the build.

Links use `MarkdownLinkForeground`, a shade of the OS accent colour rather than the accent itself:
- `SystemAccentColorDark2` in Light;
- `SystemAccentColorLight2` in Dark.

This follows WinUI, and keeps links above 4.5:1 for typical accent colours. Very light accents, such as gold or yellow, can still fall slightly short in Light. Links are always underlined, so they never rely on colour alone. These shades come from FluentTheme; with another base theme, define `MarkdownLinkForeground` yourself.

## Text size

Users who need larger text set it once for the whole system, and `MarkdownViewer` follows that setting: every font size of the document is multiplied by `SystemTextScale.Factor`. Avalonia doesn't expose this setting, so MarkView reads it where the platform allows:

| Platform | Setting | Read |
|---|---|---|
| Windows | Settings → Accessibility → Text size (100–225%) | At startup, and again each time a window showing a viewer is activated (the user comes back from Settings) |
| Browser | The browser's default font size (Chrome: Settings → Appearance → Font size), relative to 16px. Page zoom (Ctrl +) is already applied by Avalonia as render scaling. | At startup |
| Android, iOS, Linux | Font size / Dynamic Type / GNOME's text scaling factor | Not read: set `SystemTextScale.Factor` from the platform's API (below) |
| macOS | No system-wide text size | — |

On top of it, `TextScale` scales one viewer, for instance for an in-app text size setting or a zoom control; `EffectiveTextScale` is the product of both. Set `FollowSystemTextScale="False"` for a viewer whose text must keep its designed size.

```xml
<mv:MarkdownViewer TextScale="1.25" Source="avares://MyApp/notes.md" />
```

What scales:

- every font size in the document, whoever set it: the viewer's own (the body text), the theme's heading, code and alert sizes, theme presets, and your own styles. Text that inherits a size is never scaled twice;
- what is drawn at the text's size: alert and fold chevrons, task check boxes, the broken-image glyph;
- math formulas, drawn again at the new size rather than stretched.

What doesn't: images, Mermaid diagrams, spacing, borders and other sizes in pixels, as with the system setting in native apps. The viewer's own `FontSize` property keeps its value, and only the rendered document scales.

On platforms MarkView doesn't read, pass the platform's value to `SystemTextScale.Factor`, which updates every viewer that follows it. Once set by the application, the value is no longer read from the platform.

```csharp
// Android (MainActivity): Configuration.FontScale is 1 at the default size.
SystemTextScale.Factor = Resources!.Configuration!.FontScale;
// …and again in OnConfigurationChanged(Configuration newConfig).

// iOS (AppDelegate): how much Dynamic Type enlarges 17pt body text.
SystemTextScale.Factor = UIFontMetrics.DefaultMetrics.GetScaledValue(17) / 17;
// …and again on UIApplication.ContentSizeCategoryChangedNotification.
```

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
