# Accessibility

`MarkdownViewer` exposes the structure of the rendered document to screen readers, ships default colours that meet WCAG 2.2 AA contrast, and switches to a high-contrast palette when the operating system asks for one.

## Screen readers

Avalonia forwards automation peers to each platform's accessibility API: UI Automation on Windows, NSAccessibility on macOS, and AT-SPI on Linux. Each rendered element reports the following role:

| Markdown | Reported as |
|---|---|
| The viewer itself | `Document`, named by `AutomationProperties.Name` or, if that isn't set, by the first heading |
| `#` … `######` | `Text` with `AutomationProperties.HeadingLevel` 1–6 |
| Paragraph | `Text` |
| List / list item | `List` / `ListItem`. Bullets are hidden; numbers stay readable, since an ordered list can start at any number. |
| Table / header cell / body cell | `Table` / `HeaderItem` / `DataItem` |
| Blockquote, figure | `Group`. A figure is named by its caption. |
| Alert (`> [!NOTE]`) | `Group`, named by its title ("NOTE", "TIP", …), so the alert kind is announced as text and not only shown by colour |
| Thematic break | `Separator` |
| Image | `Image`, named by its alt text. An image with empty alt text (`![](…)`) is decorative and hidden, as browsers do with `alt=""`. |
| YouTube embed | `Hyperlink`, named by its alt text, or by its URL when there is none |
| Math formula (`MarkView.Avalonia.Math`) | `Image`, named by its TeX source |
| Mermaid diagram (`MarkView.Avalonia.Mermaid`) | `Image`, named by its diagram source |

To give the document a name of your own, set `AutomationProperties.Name`:

```xml
<mv:MarkdownViewer AutomationProperties.Name="Release notes" Source="avares://MyApp/notes.md" />
```

### Platform caveats

Avalonia 12.1.3 has the following gaps:

- **Linux.** The AT-SPI bridge does not report heading levels, so headings read as plain text.
- **Browser (WebAssembly).** Avalonia ships no screen reader support.

## Colour contrast

The default palette in `MarkdownTheme.axaml` meets WCAG 2.2 AA on FluentTheme's page colours (white with black text, and black with white text):

- **4.5:1** for text: code, inline code, `==marked==` text, alert titles and alert bodies.
- **3:1** for borders that carry meaning (1.4.11): blockquote bars and table cell borders.

`ThemeContrastTests` checks every pair against the colours in the theme file, so a brush change that drops below these ratios fails the build.

Links use `MarkdownLinkForeground`, a shade of the OS accent colour rather than the accent itself:
- `SystemAccentColorDark2` in Light;
- `SystemAccentColorLight2` in Dark.

This follows WinUI, and keeps links above 4.5:1 for typical accent colours. Very light accents, such as gold or yellow, can still fall slightly short in Light. Links are always underlined, so they never rely on colour alone. These shades come from FluentTheme; with another base theme, define `MarkdownLinkForeground` yourself.

## High contrast

Avalonia reports whether the operating system asks for high contrast. It detects this on Windows, on macOS ("Increase contrast") and in the browser (`prefers-contrast: more`), but not on Linux. It ships neither a high-contrast theme nor the OS contrast colours, so `MarkdownViewer` brings its own palette. When high contrast is on, the viewer gets the `:high-contrast` pseudo-class, and `MarkdownTheme.axaml` then:

- paints the viewer in solid `MarkdownHighContrastBackground` / `MarkdownHighContrastForeground`;
- draws links in `MarkdownHighContrastLink`, and `==marked==` text in `MarkdownHighContrastHighlightForeground` on `MarkdownHighContrastHighlightBackground`;
- gives code blocks, tables, blockquotes, figures, separators and alerts solid `MarkdownHighContrastBorder` borders;
- drops the per-kind alert colours (the titles carry the kind) and all opacity dimming.

| Resource key | Dark | Light |
|---|---|---|
| `MarkdownHighContrastBackground` | `#000000` | `#FFFFFF` |
| `MarkdownHighContrastForeground` | `#FFFFFF` | `#000000` |
| `MarkdownHighContrastLink` | `#FFFF00` | `#00009F` |
| `MarkdownHighContrastHighlightBackground` | `#1AEBFF` | `#37006E` |
| `MarkdownHighContrastHighlightForeground` | `#000000` | `#FFFFFF` |
| `MarkdownHighContrastBorder` | `#FFFFFF` | `#000000` |

Every pair is at least 7:1, as Windows' contrast themes are. The column used follows the theme variant Avalonia reports along with the contrast setting. On Windows, Avalonia only reports Light for contrast themes whose name contains "White". Override any of these keys in your own resources to match your app's contrast palette.

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

Syntax-highlighted code keeps its token colours in high contrast. The Dark+ and Light+ themes already contrast with black and white backgrounds. Mermaid diagrams keep their regular palette.
