# Theming

## Including the default theme

`MarkdownTheme.axaml` is an embedded resource in the `MarkView.Avalonia` package. Include it in `App.axaml`:

```xml
<Application.Styles>
  <FluentTheme />
  <StyleInclude Source="avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml" />
</Application.Styles>
```

The theme uses `DynamicResource` throughout so it responds to Avalonia's `RequestedThemeVariant` changes automatically. This include is not purely cosmetic: it also supplies `MarkdownViewer`'s default `ControlTemplate`, which is what provides the `PART_ScrollViewer` that makes the control scrollable — without it, content renders but cannot scroll and `ScrollToAnchor()` silently no-ops.

Extension packages that need their own overridable colours ship a standalone theme file the same way — e.g. `MarkView.Avalonia.Mermaid` includes `MermaidTheme.axaml`:

```xml
<StyleInclude Source="avares://MarkView.Avalonia.Mermaid/Themes/MermaidTheme.axaml" />
```

Each extension theme is independent — it does not import the core `MarkdownTheme.axaml` — so including one, none, or several has no ordering requirement. If an extension theme isn't included, the extension falls back to built-in default colours rather than failing.

## Colour resources

`MarkdownTheme.axaml` defines its colours as `SolidColorBrush` resources in `Light` and `Dark` theme dictionaries, used through `DynamicResource`, so you can retint the document without restyling it. Redefine a key for the variants you want (see [Overriding a preset](#overriding-a-preset) for where to put it):

| Resource key | Colours | Dark | Light |
|---|---|---|---|
| `MarkdownCodeBlockBackground` | Fenced and indented code blocks | `#1E1E1E` | `#F3F3F3` |
| `MarkdownCodeBlockForeground` | Code block text (before syntax highlighting); the copy icon under the pointer | `#D4D4D4` | `#1E1E1E` |
| `MarkdownCodeInlineBackground` | Inline code | `#1AFFFFFF` | `#0A000000` |
| `MarkdownCodeInlineForeground` | Inline code text | `#F4B49A` | `#A31515` |
| `MarkdownCodeHeaderBackground` | Code block toolbar | `#2D2D2D` | `#E4E4E4` |
| `MarkdownCodeHeaderForeground` | Toolbar language label and copy icon; a display formula's copy icon | `#A0A0A0` | `#5F5F5F` |
| `MarkdownCodeCopyButtonHoverBackground` | Copy button under the pointer | `#1AFFFFFF` | `#14000000` |
| `MarkdownCodeLineNumberForeground` | Code block line numbers | `#8A8A8A` | `#6A6A6A` |
| `MarkdownLinkForeground` | Links and footnote references | FluentTheme's `SystemAccentColorLight2` | FluentTheme's `SystemAccentColorDark2` |
| `MarkdownBlockquoteBorder` | Blockquote bar | `#4A9ECC` | `#2F7FB3` |
| `MarkdownTableBorder` | Table cell borders | `#6B6B6B` | `#8A8A8A` |
| `MarkdownAlertDefaultBorder` | Bar and title of alert kinds without their own colours (e.g. `[!QUESTION]`) | `#A1A1AA` | `#52525B` |
| `MarkdownAlertDefaultBackground` | Background of those alerts | `#1A71717A` | `#F4F4F5` |
| `MarkdownAlertNoteBorder` | `[!NOTE]` bar and title | `#3B82F6` | `#2563EB` |
| `MarkdownAlertNoteBackground` | `[!NOTE]` background | `#1A3B82F6` | `#EFF6FF` |
| `MarkdownAlertTipBorder` | `[!TIP]` bar and title | `#22C55E` | `#15803D` |
| `MarkdownAlertTipBackground` | `[!TIP]` background | `#1A22C55E` | `#F0FDF4` |
| `MarkdownAlertImportantBorder` | `[!IMPORTANT]` bar and title | `#A855F7` | `#9333EA` |
| `MarkdownAlertImportantBackground` | `[!IMPORTANT]` background | `#1AA855F7` | `#FAF5FF` |
| `MarkdownAlertWarningBorder` | `[!WARNING]` bar and title | `#EAB308` | `#A16207` |
| `MarkdownAlertWarningBackground` | `[!WARNING]` background | `#1AEAB308` | `#FEFCE8` |
| `MarkdownAlertCautionBorder` | `[!CAUTION]` bar and title | `#EF4444` | `#B91C1C` |
| `MarkdownAlertCautionBackground` | `[!CAUTION]` background | `#1AEF4444` | `#FEF2F2` |
| `MarkdownMarkedBackground` | Highlighted text (`==text==`) | `#5C4B00` | `#FFFF00` |

Colours are `#AARRGGBB` when translucent. The theme also defines two `StreamGeometry` resources, the same in both variants: `MarkdownCodeCopyIcon` and `MarkdownCodeCopiedIcon`, the copy button's icons (see [Code block toolbar](#code-block-toolbar)). The default theme sets a few colours directly in its styles rather than through a key: the figure border (`#40808080`) and the table header background (`#20808080`); restyle those selectors to change them. The defaults meet WCAG 2.2 AA contrast; see [Accessibility](accessibility.md#colour-contrast). The `MarkdownHighContrast*` keys, the palette of the high-contrast mode, are listed under [High contrast](accessibility.md#high-contrast).

## Theme presets

A theme preset is a style file included after `MarkdownTheme.axaml` that gives the whole document another look. It builds on the default theme rather than replacing it: it redefines the [colour resources](#colour-resources) and adds styles only where its look differs, so the default theme (and its `ControlTemplate`) must still be included before it.

### GitHub

`GitHubTheme.axaml` makes the document look like Markdown rendered on github.com, with GitHub's [Primer](https://primer.style/) colours in both the `Light` and `Dark` variants. Like the default theme, it follows the application's theme variant live.

```xml
<Application.Styles>
  <FluentTheme />
  <StyleInclude Source="avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml" />
  <StyleInclude Source="avares://MarkView.Avalonia/Themes/GitHubTheme.axaml" />
</Application.Styles>
```

What it changes:

| Element | GitHub look |
|---|---|
| Document | GitHub's page background and text colour (`#FFFFFF` / `#1F2328` light, `#0D1117` / `#F0F6FC` dark) set on `MarkdownViewer`, its system font stack (Segoe UI, SF Pro Text, Helvetica Neue, Noto Sans, Helvetica, Arial) at 16px, and 16px between paragraphs with a little extra line spacing |
| Headings | 2em / 1.5em / 1.25em / 1em / 0.875em / 0.85em (32 to 13.6px), semibold, 24px above and 16px below (none above the first block of the document, a quote or an alert); a 1px rule under h1 and h2; h6 in the secondary text colour |
| Links | GitHub's blue (`#0969DA` / `#4493F8`), underlined |
| Code blocks | GitHub's code background, 16px padding, 6px corners, 13.6px monospace (SF Mono, Menlo, Consolas, Liberation Mono). The toolbar takes the code's background, so the language label and the outlined copy button sit inside the block's top edge |
| Inline code | Monospace on a translucent grey, in the text colour, at 85% of the paragraph's size (full size in a heading) |
| Blockquotes | A 4px grey bar, secondary text colour, no dimming |
| Tables | A 1px grid on every side of every cell, 6×13px cell padding, a semibold header row without background, centred unless the column has its own alignment (`:--`, `:-:`, `--:`), and every second body row striped |
| Horizontal rules | A 4px grey bar, 24px above and below |
| Alerts | GitHub's note, tip, important, warning and caution colours for the 4px bar and the title (at body size), and no background |
| Task lists | Check boxes in the secondary text colour, like GitHub's read-only ones |
| Marked text | GitHub's pale yellow (dark: translucent amber) |
| Footnotes | Below a 1px line, in 12px secondary text |
| Definition lists | Italic terms, definitions indented by 16px |

Where it departs from GitHub:

- **Contrast.** Every text colour meets the WCAG 2.2 AA 4.5:1 minimum and every border that carries meaning (blockquote bar, table grid, horizontal rule, alert bars) meets 3:1, against the page and against the background it sits on. GitHub draws the blockquote bar, table grid and horizontal rule in `#D1D9E0` (dark `#3D444D`), below 3:1, so the preset uses Primer's emphasis border colour, `#818B98` (dark `#656C76`), for them; they look a little darker than on github.com. Decorative lines keep GitHub's colours: the rule under h1 and h2, the line above the footnotes and the copy button's outline.
- **Alert colours.** GitHub uses a slightly different colour for the bar and the title in dark mode; one key (`MarkdownAlert<Kind>Border`) drives both here, so the preset uses the title colour, which also passes 4.5:1 as text. Alert titles keep the renderer's text (`NOTE`) and have no icon.
- **What styles can't express.** Inline code has no padding or rounded corners (it is a `Run`, so its text stays selectable), and tables fill the width of the document.

The preset uses three renderer hooks that the default theme leaves unstyled, and that any theme can use: the text blocks of headings and paragraphs (`MarkdownSelectableTextBlock`) draw a border from their `BorderBrush` and `BorderThickness` properties, over the edges of their `Padding` (none by default); every second body row of a table has the `markdown-table-row-even` class; and the line above the footnotes has the `markdown-footnote-separator` class next to `markdown-thematic-break`.

It adds these resource keys to the ones it redefines from the default theme:

| Resource key | Colours | Dark | Light |
|---|---|---|---|
| `MarkdownBackground` | `MarkdownViewer`'s background | `#0D1117` | `#FFFFFF` |
| `MarkdownForeground` | Body text (`MarkdownViewer.Foreground`) | `#F0F6FC` | `#1F2328` |
| `MarkdownMutedForeground` | Blockquotes, h6, footnotes, task check boxes | `#9198A1` | `#59636E` |
| `MarkdownHeadingBorder` | Rule under h1 and h2 | `#B33D444D` | `#B3D1D9E0` |
| `MarkdownTableRowAlternateBackground` | Striped table rows | `#151B23` | `#F6F8FA` |
| `MarkdownThematicBreakBackground` | Horizontal rule | `#656C76` | `#818B98` |
| `MarkdownFootnoteSeparatorBackground` | Line above the footnotes | `#3D444D` | `#D1D9E0` |
| `MarkdownCodeCopyButtonBorder` | Copy button outline | `#3D444D` | `#D1D9E0` |

and redefines the default theme's keys as follows:

| Resource key | Dark | Light |
|---|---|---|
| `MarkdownLinkForeground` | `#4493F8` | `#0969DA` |
| `MarkdownBlockquoteBorder`, `MarkdownTableBorder` | `#656C76` | `#818B98` |
| `MarkdownCodeBlockBackground`, `MarkdownCodeHeaderBackground` | `#151B23` | `#F6F8FA` |
| `MarkdownCodeBlockForeground`, `MarkdownCodeInlineForeground` | `#F0F6FC` | `#1F2328` |
| `MarkdownCodeInlineBackground` | `#33656C76` | `#1F818B98` |
| `MarkdownCodeHeaderForeground`, `MarkdownCodeLineNumberForeground` | `#9198A1` | `#59636E` |
| `MarkdownCodeCopyButtonHoverBackground` | `#33656C76` | `#1A818B98` |
| `MarkdownAlertDefaultBorder` | `#9198A1` | `#59636E` |
| `MarkdownAlertNoteBorder` | `#4493F8` | `#0969DA` |
| `MarkdownAlertTipBorder` | `#3FB950` | `#1A7F37` |
| `MarkdownAlertImportantBorder` | `#AB7DF8` | `#8250DF` |
| `MarkdownAlertWarningBorder` | `#D29922` | `#9A6700` |
| `MarkdownAlertCautionBorder` | `#F85149` | `#D1242F` |
| `MarkdownAlert<Kind>Background` (all six) | `#00000000` | `#00000000` |
| `MarkdownMarkedBackground` | `#26BB8009` | `#FFF8C5` |
| `MarkdownHighContrastBackground` | `#0A0C10` | `#FFFFFF` |
| `MarkdownHighContrastForeground` | `#F0F3F6` | `#0E1116` |
| `MarkdownHighContrastLink` | `#71B7FF` | `#0349B4` |
| `MarkdownHighContrastHighlightBackground` | `#F0B72F` | `#744500` |
| `MarkdownHighContrastHighlightForeground` | `#0A0C10` | `#FFFFFF` |
| `MarkdownHighContrastBorder` | `#B7BDC8` | `#20252C` |

**In high contrast.** When the viewer has the `:high-contrast` pseudo-class (see [High contrast](accessibility.md#high-contrast)), the preset uses the colours of Primer's "dark high contrast" and "light high contrast" themes for the `MarkdownHighContrast*` keys above, every pair 7:1 or more, and gives up its muted text, striped table rows and grey rules and borders for them, as the default theme does. Find highlights keep the preset's colours, on which its high-contrast text stays above 8:1 but links drop to 4.3:1 (dark) and 4.7:1 (light).

**Next to the extension packages.** Their themes stay as they are. Syntax highlighting (`MarkView.Avalonia.SyntaxHighlighting`) keeps its TextMate colours, Dark+ and Light+ by default, drawn on the preset's code background: GitHub's own highlighting colours differ, but Dark+ and Light+ have at least the same contrast on it as on the default theme's (the light background is a little lighter, the dark one a little darker). Mermaid diagrams keep the colours of `MermaidTheme.axaml`: in the light variant their white background matches the page, while in the dark one their `#18181B` background shows as a slightly lighter card on GitHub's `#0D1117`; redefine `MarkdownMermaidBackground` in the `Dark` dictionary to blend it in. Math formulas follow the theme variant on their own.

### Chat

`ChatTheme.axaml` makes the document compact, for chat messages and other narrow surfaces, in the style of AI chat applications. Unlike the GitHub preset, it leaves the page to the host: the viewer keeps a transparent background and inherits the font family, font size and text colour of the bubble or window around it.

```xml
<Application.Styles>
  <FluentTheme />
  <StyleInclude Source="avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml" />
  <StyleInclude Source="avares://MarkView.Avalonia/Themes/ChatTheme.axaml" />
</Application.Styles>
```

What it changes:

| Element | Chat look |
|---|---|
| Document | Nothing on `MarkdownViewer` itself. Blocks are only 8px apart (the document's block spacing), with no extra margin around paragraphs, lists, quotes, alerts, tables, images or code; paragraphs get 3px of extra line spacing |
| Headings | 20 / 18 / 16 / 15 / 14 / 14px, semibold, 8px above them (none at the start of a message, quote or alert); h6 in the secondary text colour |
| Links | Blue (`#1D4ED8` / `#6EADFB`), underlined |
| Code blocks | A card with 8px corners and a faint 1px outline, on a translucent tint of the page, 12×10px padding, 13px monospace (Cascadia Code, SF Mono, Menlo, Consolas, Liberation Mono). The toolbar takes the code's background, so the language label and copy button sit inside the card's top edge |
| Inline code | Monospace on a faint translucent chip, in a text colour close to the body's, at 13px in paragraphs |
| Blockquotes | A 3px grey bar, secondary text colour, no dimming |
| Tables | A line under every row and none between columns, a semibold header on a faint tint, 10×6px cell padding |
| Horizontal rules | A 1px grey line |
| Alerts | A 3px bar and the title in the kind's colour, on a faint tint of it, 8px corners on the right, the title at 14px |
| Footnotes | 12px secondary text, no dimming |

The colours that sit on the page (code cards, chips, tints, the table header) are translucent, so they follow the bubble's colour. Every pair meets WCAG 2.2 AA (4.5:1 for text, 3:1 for the quote bar, table lines and rules) on white and on a `#F4F4F5` bubble in the light variant, and on black, `#18181B` and `#2F2F2F` in the dark one, with Fluent's text colour as the body text. On a coloured bubble (an accent-coloured message from the user, say), check the contrast yourself or redefine the keys below for that bubble.

The sizes are absolute, tuned for 14px body text (Fluent's default): with a larger body size, headings sit closer to it. Set `FontSize` on the viewer or its bubble as usual, and restyle `:is(TextBlock).markdown-h1` to `markdown-h6` if you need other heading sizes.

It adds these resource keys to the ones it redefines from the default theme:

| Resource key | Colours | Dark | Light |
|---|---|---|---|
| `MarkdownMutedForeground` | Blockquotes, h6, footnotes | `#AEAEB6` | `#52525B` |
| `MarkdownCodeBlockBorder` | Outline of code blocks | `#1FFFFFFF` | `#14000000` |
| `MarkdownTableHeaderBackground` | Table header row | `#0FFFFFFF` | `#0A000000` |
| `MarkdownThematicBreakBackground` | Horizontal rule | `#8B8B94` | `#7A7A83` |

and redefines the default theme's keys as follows:

| Resource key | Dark | Light |
|---|---|---|
| `MarkdownLinkForeground`, `MarkdownAlertNoteBorder` | `#6EADFB` | `#1D4ED8` |
| `MarkdownBlockquoteBorder`, `MarkdownTableBorder`, `MarkdownKbdBorder`, `MarkdownImageFailedBorder` | `#8B8B94` | `#7A7A83` |
| `MarkdownCodeBlockBackground`, `MarkdownCodeHeaderBackground`, `MarkdownImageFailedBackground` | `#0FFFFFFF` | `#0A000000` |
| `MarkdownCodeBlockForeground`, `MarkdownCodeInlineForeground` | `#E4E4E7` | `#18181B` |
| `MarkdownCodeInlineBackground` | `#1AFFFFFF` | `#0F000000` |
| `MarkdownCodeHeaderForeground`, `MarkdownCodeLineNumberForeground`, `MarkdownImageFailedForeground`, `MarkdownAlertDefaultBorder` | `#AEAEB6` | `#52525B` |
| `MarkdownCodeCopyButtonHoverBackground` | `#1AFFFFFF` | `#14000000` |
| `MarkdownAlertTipBorder` | `#4ADE80` | `#166534` |
| `MarkdownAlertImportantBorder` | `#C79AFD` | `#7E22CE` |
| `MarkdownAlertWarningBorder` | `#FACC15` | `#854D0E` |
| `MarkdownAlertCautionBorder` | `#FA8C8C` | `#B91C1C` |
| `MarkdownAlert<Kind>Background` | The kind's colour at 8% (`#14…`) | The kind's colour at 6% (`#0F…`) |
| `MarkdownMarkedBackground` | `#5C4B00` | `#FEF08A` |
| `MarkdownKbdBackground`, `MarkdownKbdForeground` | `#27272A`, `#F4F4F5` | `#FAFAFA`, `#18181B` |

The find highlights, the image lightbox and the `MarkdownHighContrast*` palette keep the default theme's colours: the preset has no page colours of its own, so in high contrast the document takes the default theme's black or white page, and the code card's outline, quote bar, table lines and rules turn solid, as the default theme's do.

### Docs

`DocsTheme.axaml` gives the document the look of a documentation site, modelled on the default theme of [VitePress](https://vitepress.dev/): Inter at 16px with airy line spacing, a reading column at most 768px wide, a divider above each section heading, code on a soft background and alerts as tinted boxes, in VitePress' light and dark colours. Like the GitHub preset, it sets its own page colours.

```xml
<Application.Styles>
  <FluentTheme />
  <StyleInclude Source="avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml" />
  <StyleInclude Source="avares://MarkView.Avalonia/Themes/DocsTheme.axaml" />
</Application.Styles>
```

What it changes:

| Element | Docs look |
|---|---|
| Document | VitePress' page background and text colour (`#FFFFFF` / `#3C3C43` light, `#1B1B1F` / `#DFDFD6` dark) set on `MarkdownViewer`, Inter (then Segoe UI, SF Pro Text, Helvetica Neue, Noto Sans, Helvetica, Arial) at 16px, paragraphs 16px apart with 6px of extra line spacing. The document is at most 768px wide, centred in a wider viewer (the `markdown-document` style class) |
| Headings | 32 / 24 / 20 / 18 / 16 / 16px, semibold, with space above them (none at the start of the document, a quote or an alert); h2 opens a section with a 1px divider 24px above its text; h6 in the secondary text colour |
| Links | VitePress' indigo (`#3451B2` / `#A8B1FF`), medium weight, underlined |
| Code blocks | VitePress' code background, 20×24px padding, 8px corners, 14px monospace (SF Mono, Menlo, Monaco, Consolas, Liberation Mono) on 24px lines. The toolbar takes the code's background, with an outlined copy button |
| Inline code | Monospace in the link colour on a translucent grey chip, at 14px in body text |
| Blockquotes | A 2px grey bar, secondary text colour, no dimming |
| Tables | A 1px grid, 14px text, 8×16px cell padding, a header row in semibold secondary text on a soft background, and every second body row striped |
| Horizontal rules | A 1px grey line |
| Alerts | Tinted boxes with 8px corners and no bar, 14px text, the title in semibold and the kind's colour: note in indigo, tip in green, important in purple, warning in amber, caution in red |
| Footnotes | Below a 1px line, in 14px secondary text |

Where it departs from VitePress:

- **Contrast.** As with the GitHub preset, every text colour meets 4.5:1 and every border that carries meaning (blockquote bar, table grid, horizontal rule) meets 3:1. VitePress draws those in its divider colour, `#E2E2E3` (dark `#2E2E32`), below 3:1, so the preset uses `#87878C` (dark `#75757C`). Its language label (`#929295`) is below 4.5:1 on the code background, so the label uses the secondary text colour. Decorative lines keep VitePress' divider: the one above h2, the line above the footnotes and the copy button's outline.
- **Alert colours.** VitePress shows tips in its brand colour; the preset keeps GitHub's meaning of each kind (tips in green, notes in the brand indigo).
- **What styles can't express.** As with the GitHub preset, inline code has no padding or rounded corners, and tables fill the width of the column.

To give the document the viewer's whole width, lift the cap in a style after the preset:

```xml
<Style Selector="Grid.markdown-document">
  <Setter Property="MaxWidth" Value="Infinity" />
</Style>
```

It adds these resource keys to the ones it redefines from the default theme (the GitHub and Chat presets use the same names):

| Resource key | Colours | Dark | Light |
|---|---|---|---|
| `MarkdownBackground` | `MarkdownViewer`'s background | `#1B1B1F` | `#FFFFFF` |
| `MarkdownForeground` | Body text (`MarkdownViewer.Foreground`) | `#DFDFD6` | `#3C3C43` |
| `MarkdownMutedForeground` | Blockquotes, h6, table headers, footnotes | `#98989F` | `#67676C` |
| `MarkdownHeadingBorder` | Divider above h2 | `#2E2E32` | `#E2E2E3` |
| `MarkdownTableHeaderBackground`, `MarkdownTableRowAlternateBackground` | Table header row, striped rows | `#202127` | `#F6F6F7` |
| `MarkdownThematicBreakBackground` | Horizontal rule | `#75757C` | `#87878C` |
| `MarkdownFootnoteSeparatorBackground` | Line above the footnotes | `#2E2E32` | `#E2E2E3` |
| `MarkdownCodeCopyButtonBorder` | Copy button outline | `#2E2E32` | `#E2E2E3` |

and redefines the default theme's keys as follows:

| Resource key | Dark | Light |
|---|---|---|
| `MarkdownLinkForeground`, `MarkdownCodeInlineForeground`, `MarkdownAlertNoteBorder` | `#A8B1FF` | `#3451B2` |
| `MarkdownBlockquoteBorder`, `MarkdownTableBorder`, `MarkdownKbdBorder`, `MarkdownImageFailedBorder` | `#75757C` | `#87878C` |
| `MarkdownCodeBlockBackground`, `MarkdownCodeHeaderBackground` | `#161618` | `#F6F6F7` |
| `MarkdownCodeBlockForeground`, `MarkdownKbdForeground` | `#DFDFD6` | `#3C3C43` |
| `MarkdownCodeInlineBackground`, `MarkdownAlertDefaultBackground` | `#29657585` | `#248E96AA` |
| `MarkdownCodeHeaderForeground`, `MarkdownCodeLineNumberForeground`, `MarkdownImageFailedForeground`, `MarkdownAlertDefaultBorder` | `#98989F` | `#67676C` |
| `MarkdownCodeCopyButtonHoverBackground`, `MarkdownKbdBackground`, `MarkdownImageFailedBackground` | `#202127` | `#FFFFFF`, `#F6F6F7`, `#F6F6F7` |
| `MarkdownAlertTipBorder` | `#3DD68C` | `#18794E` |
| `MarkdownAlertImportantBorder` | `#C8ABFA` | `#6F42C1` |
| `MarkdownAlertWarningBorder` | `#F9B44E` | `#915930` |
| `MarkdownAlertCautionBorder` | `#F66F81` | `#B8272C` |
| `MarkdownAlert<Kind>Background` | VitePress' soft tints, 16% (`#29…`) | 14% (`#24…`) |
| `MarkdownMarkedBackground` | `#5C4B00` | `#FFF3C4` |
| `MarkdownFindCurrentMatchBackground` | `#652A00` | `#FFD3A3` |

The current find match is a paler orange than the default theme's in the light variant, so inline code, drawn in the link colour, keeps 4.5:1 on it. The other find highlight, the image lightbox and the `MarkdownHighContrast*` palette keep the default theme's colours. In high contrast the preset gives up its own page colours, muted text, soft table headers and striped rows for that palette, the divider above h2 turns solid, and alerts, which have no bar, get a 1px outline.

### Overriding a preset

The preset's colours are resources, so the usual Avalonia lookup order applies: a key defined closer to the viewer wins. Redefine keys in `Application.Resources` (which Avalonia searches before `Application.Styles`), in a `Styles` block included after the preset, or in the `Resources` of a window or of the viewer itself:

```xml
<Application.Resources>
  <ResourceDictionary>
    <ResourceDictionary.ThemeDictionaries>
      <ResourceDictionary x:Key="Light">
        <!-- Keep the host's own background behind the document -->
        <SolidColorBrush x:Key="MarkdownBackground">Transparent</SolidColorBrush>
      </ResourceDictionary>
      <ResourceDictionary x:Key="Dark">
        <SolidColorBrush x:Key="MarkdownBackground">Transparent</SolidColorBrush>
      </ResourceDictionary>
    </ResourceDictionary.ThemeDictionaries>
  </ResourceDictionary>
</Application.Resources>
```

Styles work as with the default theme: a style declared after the preset's include wins over the preset's for the same property, whatever its selector. A value set on the viewer itself always wins over both themes, e.g. `<mv:MarkdownViewer Background="Transparent" FontSize="14" />`. The GitHub and Docs presets set `Background`, `Foreground`, `FontFamily` and `FontSize` on `MarkdownViewer`, so local values of these replace the preset's for the whole document.

To switch presets at run time, add or remove the preset's `StyleInclude`; every open document restyles in place, without being rebuilt:

```csharp
var gitHub = new StyleInclude((Uri?)null)
{
    Source = new Uri("avares://MarkView.Avalonia/Themes/GitHubTheme.axaml"),
};
var styles = Application.Current!.Styles;
// A StyleInclude written in App.axaml is compiled into the theme's own Styles, so look the default
// theme up by one of its resources rather than by type or Source.
var markdownTheme = styles.First(s => s.TryGetResource("MarkdownCodeBlockBackground", ThemeVariant.Dark, out _));
// Right after MarkdownTheme.axaml, so the application's own styles keep winning
styles.Insert(styles.IndexOf(markdownTheme) + 1, gitHub);
// ... and back to the default look:
styles.Remove(gitHub);
```

## Style class reference

Every element rendered by `MarkdownViewer` is tagged with a CSS-style class name. Override any selector in your own `Styles` block to customise appearance.

### Core

| Class | Control | Element |
|-------|---------|---------|
| `markdown-document` | `Grid` | The whole rendered document (`MarkdownViewer.Content`): its root panel with the selection and link focus layers. Style it for changes to the whole document, such as its margins or reading width (see [The whole document](#the-whole-document)) |
| `markdown-h1` … `markdown-h6` | `TextBlock` | Headings |
| `markdown-heading-foldable` | `TextBlock` | A heading with a fold button, when `AreHeadingsFoldable` is set; has the `:expanded` pseudo-class while its section is shown (see [Foldable headings](#foldable-headings)) |
| `markdown-heading-fold` | `Control` | The fold button at the start of a foldable heading |
| `markdown-paragraph` | `TextBlock` | Paragraphs |
| `markdown-code-block` | `Border` | Fenced code block container |
| `markdown-code-container` | `StackPanel` | Wraps a code block and its toolbar (only when the toolbar is shown) |
| `markdown-code-header` | `Border` | Code block toolbar |
| `markdown-code-language` | `TextBlock` | Language label in the toolbar |
| `markdown-code-copy` | `Button` | Copy button in the toolbar, and in the corner of a display formula; also has the `copied` class for two seconds after a copy |
| `markdown-code-line-numbers` | `TextBlock` | Line-number gutter of a code block, when `ShowCodeBlockLineNumbers` is set |
| `markdown-code-scroll` | `ScrollViewer` | Scrolls the code of a block vertically, when `CodeBlockMaxHeight` is set |
| `markdown-custom-fence` | `Border` | Wraps the control a [fence renderer](custom-extensions.md#rendering-a-fence-language-as-your-own-control) returned for a fenced block |
| `markdown-fence-<language>` | `Border` | The same `Border`, by fence language in lower case (e.g. `markdown-fence-chart`) |
| `markdown-code-inline` | `Border` | Inline code container |
| `markdown-blockquote` | `Border` | Blockquote container |
| `markdown-list` | `StackPanel` | Ordered / unordered list |
| `markdown-list-marker` | `TextBlock` | List item bullet, number or task-list check glyph |
| `markdown-task-list` | `TextBlock` | Task-list check glyph: a box, ticked when checked, drawn in its `Foreground` and sized by its `FontSize` (its text, ☐ or ☑, is what selection copies) |
| `markdown-task-toggle` | `ToggleButton` | Clickable task-list checkbox wrapping the glyph, when `AreTaskListsInteractive` is set; `:checked` when ticked |
| `markdown-thematic-break` | `Separator` | Horizontal rule |
| `markdown-image` | `Image` | Rendered image |
| `markdown-image-failed` | `Border` | Placeholder of an image that could not be loaded: a broken-image glyph and the alt text (see [Images](#images)) |
| `markdown-image-lightbox` | `Panel` | Lightbox a clicked image opens in, over the viewer (see [Images](#images)) |
| `markdown-image-lightbox-image` | `Image` | The image in the lightbox |
| `markdown-image-lightbox-close` | `Button` | The lightbox's close button |
| `markdown-image-lightbox-toolbar` | `Border` | The lightbox's zoom toolbar, at the bottom |
| `markdown-image-lightbox-zoom` | `Button` | Each button of the zoom toolbar, along with `markdown-image-lightbox-zoom-out`, `-zoom-in` or `-zoom-fit` |
| `markdown-image-lightbox-zoom-level` | `TextBlock` | The zoom level in the toolbar, as a percentage of the natural size |
| `markdown-link` | `HyperlinkButton` | Hyperlink |
| `markdown-context-copy`, `markdown-context-copy-markdown`, `markdown-context-copy-link`, `markdown-context-select-all` | `MenuItem` | Items of the viewer's default [context menu](text-selection.md#context-menu) |
| `markdown-table` | `Grid` | Table |
| `markdown-table-cell` | `Border` | Table body cell |
| `markdown-table-header` | `Border` | Table header cell |
| `markdown-table-row-even` | `Border` | Cell of every second body row (the 2nd, 4th, … below the header), for striped rows; unstyled by the default theme |

The clickable task-list checkbox (`ToggleButton.markdown-task-toggle`) uses its own minimal control
theme rather than your application's `ToggleButton` theme, so it looks like the static glyph: no
padding, chrome or accent fill. Its glyph is still a `TextBlock.markdown-task-list`, styled like
the static one. The box is drawn rather than taken from a font, because browser builds only have
the fonts the application embeds, which usually lack ☐ and ☑. App-wide `ToggleButton` *styles* (as opposed to the control theme) do apply to it;
scope them with a class, or reset them on `ToggleButton.markdown-task-toggle`.

### EmphasisExtras

| Class | Control | Element |
|-------|---------|---------|
| `markdown-marked` | `Span` | Highlighted text (`==text==`) |
| `markdown-subscript` | `Span` | Subscript (`~text~`, `<sub>`): the font's `subs` glyphs on the subscript baseline |
| `markdown-superscript` | `Span` | Superscript (`^text^`, `<sup>`): the font's `sups` glyphs on the superscript baseline |

Underline (inserted), bold, italic, and strikethrough use standard Avalonia inlines and properties (`Underline`, `Bold`, `Italic`, `TextDecorations`) and do not have dedicated style classes.

### Citations

| Class | Control | Element |
|-------|---------|---------|
| `markdown-citation` | `Span` | Citation text (`""text""`), requires `UseCitations()` |

### Alert blocks

| Class | Control | Element |
|-------|---------|---------|
| `markdown-alert` | `Border` | Alert container; its colours (`MarkdownAlertDefaultBorder` / `MarkdownAlertDefaultBackground`) apply to kinds without a variant below, e.g. `[!QUESTION]` |
| `markdown-alert-note` | `Border` | NOTE variant |
| `markdown-alert-tip` | `Border` | TIP variant |
| `markdown-alert-important` | `Border` | IMPORTANT variant |
| `markdown-alert-warning` | `Border` | WARNING variant |
| `markdown-alert-caution` | `Border` | CAUTION variant |
| `markdown-alert-<kind>` | `Border` | Any other kind, lower-cased (e.g. `markdown-alert-question`) |
| `markdown-alert-header` | `TextBlock` | Alert kind label (e.g. "NOTE"), or the custom title (`> [!TIP] Title`) |
| `markdown-alert-content` | `StackPanel` | Alert body |
| `markdown-alert-collapsible` | `Border` | Foldable alert (`> [!NOTE]-` / `> [!NOTE]+`); has the `:expanded` pseudo-class while its body is shown |
| `markdown-alert-chevron` | `Control` | Fold indicator at the start of a foldable alert's header: a triangle pointing right when collapsed and down when expanded, drawn in the header's `Foreground` and sized by its `FontSize` |

A foldable alert's header is focusable and toggles the body on click, Enter or Space; the body's visibility is set by the control, not by a style. The default theme gives the header a transparent background (so the whole row is clickable), a hand cursor, and an underline when focused from the keyboard:

```xml
<Style Selector="Border.markdown-alert-collapsible TextBlock.markdown-alert-header">
  <Setter Property="Background" Value="Transparent" />
  <Setter Property="Cursor" Value="Hand" />
</Style>
<Style Selector="Border.markdown-alert-collapsible:not(:expanded) TextBlock.markdown-alert-header">
  <Setter Property="Margin" Value="0" />
</Style>
```

Example — colour the NOTE variant:

```xml
<Style Selector="Border.markdown-alert-note">
  <Setter Property="BorderBrush" Value="#3b82f6" />
  <Setter Property="Background" Value="#eff6ff" />
</Style>
```

### Details blocks

HTML `<details>` / `<summary>` (see [HTML details blocks](configuration.md#html-details-blocks)).

| Class | Control | Element |
|-------|---------|---------|
| `markdown-details` | `Border` | The whole block; has the `:expanded` pseudo-class while its body is shown |
| `markdown-details-summary` | `TextBlock` | The summary line, which toggles the body |
| `markdown-details-chevron` | `Control` | Fold indicator at the start of the summary, like `markdown-alert-chevron`: drawn in the summary's `Foreground` and sized by its `FontSize` |
| `markdown-details-content` | `StackPanel` | The body |

The summary is focusable and toggles the body on click, Enter or Space; the body's visibility is set by the control, not by a style. The default theme only gives the summary a transparent background (so the whole row is clickable), a hand cursor and an underline when focused from the keyboard. For example, to frame the block:

```xml
<Style Selector="Border.markdown-details">
  <Setter Property="BorderBrush" Value="#40808080" />
  <Setter Property="BorderThickness" Value="1" />
  <Setter Property="CornerRadius" Value="4" />
  <Setter Property="Padding" Value="12,8" />
</Style>
<Style Selector="TextBlock.markdown-details-summary">
  <Setter Property="FontWeight" Value="SemiBold" />
</Style>
```

### Foldable headings

Headings with a fold button (see [Foldable headings](configuration.md#foldable-headings)), when
`AreHeadingsFoldable` is set.

| Class | Control | Element |
|-------|---------|---------|
| `markdown-heading-foldable` | `TextBlock` | The heading, besides its `markdown-heading` and `markdown-h1`…`markdown-h6` classes; has the `:expanded` pseudo-class while its section is shown |
| `markdown-heading-fold` | `Control` | The fold button at the start of the heading: a triangle pointing right when collapsed and down when expanded, like `markdown-alert-chevron`, drawn in the heading's `Foreground` and sized by its `FontSize`, with a focus ring when focused from the keyboard |

The sections' visibility is set by the control, not by a style. The default theme only gives the button
a right margin. For example, to dim collapsed headings and show their button more discreetly:

```xml
<Style Selector=":is(TextBlock).markdown-heading-foldable:not(:expanded)">
  <Setter Property="Opacity" Value="0.8" />
</Style>
<Style Selector=":is(Control).markdown-heading-fold">
  <Setter Property="Opacity" Value="0.6" />
</Style>
<Style Selector=":is(Control).markdown-heading-fold:pointerover">
  <Setter Property="Opacity" Value="1" />
</Style>
```

### Thinking blocks

A language model's reasoning (see [Thinking blocks](configuration.md#thinking-blocks)).

| Class | Control | Element |
|-------|---------|---------|
| `markdown-thinking` | `Border` | The whole block; has the `:expanded` pseudo-class while the reasoning is shown, and `:thinking` while a streaming session is still receiving it |
| `markdown-thinking-header` | `TextBlock` | The header ("Thinking…", "Thought for 12 seconds"), which toggles the reasoning |
| `markdown-thinking-chevron` | `Control` | Fold indicator at the start of the header, like `markdown-alert-chevron` |
| `markdown-thinking-content` | `Border` | The reasoning, around a `StackPanel` of its blocks |

The header is focusable and toggles the reasoning on click, Enter or Space; the reasoning's visibility is set by
the control, not by a style. The default theme dims the header and the reasoning (both stay at 4.5:1 or more on
the page), draws a rule left of the reasoning, and gives the header a transparent background (so the whole row
is clickable), a hand cursor and an underline when focused from the keyboard. In high contrast, nothing is
dimmed. For example, to make the header italic while the model is thinking:

```xml
<Style Selector="Border.markdown-thinking:thinking TextBlock.markdown-thinking-header">
  <Setter Property="FontStyle" Value="Italic" />
</Style>
```

### HTML subset

The HTML elements of the [HTML subset](configuration.md#html-subset). The others render with the controls and classes of their markdown counterparts: `<b>`, `<i>`, `<ins>`, `<del>` as `Bold`, `Italic`, `Underline` and a struck-through `Span`; `<mark>` as `Span.markdown-marked`; `<sub>` and `<sup>` as `markdown-subscript` and `markdown-superscript`; `<code>` as `markdown-code-inline`; `<a>` as `markdown-link`; `<img>` as `markdown-image`; `<p>` and headings as `markdown-paragraph` and `markdown-h1`…`markdown-h6`; `<hr>` as `markdown-thematic-break`.

| Class | Control | Element |
|-------|---------|---------|
| `markdown-html-kbd` | `Border` | `<kbd>`: a key around a `TextBlock` of its text, which sits on the line's baseline |
| `markdown-html-align` | `StackPanel` | Blocks aligned by an `align` attribute: a `<div align>` and the blocks in it, or the paragraph or heading of a `<p align>` / `<h1 align>`. Its inherited `TextBlock.TextAlignment` aligns the text inside it, set by the control |
| `markdown-html-align-left`, `-center`, `-right`, `-justify` | `StackPanel` | The same panel, by alignment |

| Resource key | Light | Dark | Used by |
|--------------|-------|------|---------|
| `MarkdownKbdBackground` | `#F6F8FA` | `#151B23` | Key background |
| `MarkdownKbdForeground` | `#1F2328` | `#F0F6FC` | Key text |
| `MarkdownKbdBorder` | `#818B98` | `#6E7681` | Key outline (thicker at the bottom) |

For example, a flatter key:

```xml
<Style Selector="Border.markdown-html-kbd">
  <Setter Property="BorderThickness" Value="1" />
  <Setter Property="CornerRadius" Value="2" />
</Style>
<Style Selector="Border.markdown-html-kbd > TextBlock">
  <Setter Property="FontFamily" Value="Inter" />
</Style>
```

### Figures

| Class | Control | Element |
|-------|---------|---------|
| `markdown-figure` | `Border` | Figure container (centred) |
| `markdown-figure-caption` | `TextBlock` | Caption text |

### Abbreviations

| Class | Control | Element |
|-------|---------|---------|
| `markdown-abbr` | `TextBlock` | Abbreviated term with tooltip |

### Footnotes

| Class | Control | Element |
|-------|---------|---------|
| `markdown-footnote-ref` | `HyperlinkButton` | Inline footnote reference `[1]` |
| `markdown-footnote-separator` | `Separator` | Line above the definitions; also has `markdown-thematic-break`, so it looks like a horizontal rule unless styled |
| `markdown-footnote-group` | `StackPanel` | Definition list at end of document |
| `markdown-footnote-item` | `Grid` | Individual footnote row |

### Definition lists

Requires Markdig's `UseDefinitionLists()`.

| Class | Control | Element |
|-------|---------|---------|
| `markdown-definition-list` | `StackPanel` | The whole list |
| `markdown-definition-term` | `TextBlock` | A defined term |
| `markdown-definition` | `StackPanel` | One definition, indented |

### Custom containers

Requires Markdig's `UseCustomContainers()`.

| Class | Control | Element |
|-------|---------|---------|
| `markdown-custom-container` | `Border` | A `:::name` block; it also gets `name` as a class |
| `markdown-custom-container-title` | `TextBlock` | Text after the name (`:::name Title`) |
| `markdown-custom-inline` | `Span` | An inline `::text::` |

Example: style `:::warning` containers like the warning alert:

```xml
<Style Selector="Border.markdown-custom-container.warning">
  <Setter Property="BorderBrush" Value="{DynamicResource MarkdownAlertWarningBorder}" />
  <Setter Property="Background" Value="{DynamicResource MarkdownAlertWarningBackground}" />
</Style>
```

### Footers

Requires Markdig's `UseFooters()`.

| Class | Control | Element |
|-------|---------|---------|
| `markdown-footer` | `StackPanel` | The `^^` footer lines |

### Classes from generic attributes

With Markdig's `UseGenericAttributes()` (called last), `{.name}` after an element adds `name` to the style classes of the control or inline rendered for it, next to its `markdown-*` classes:

```markdown
Read the docs first. {.note}
```

```xml
<Style Selector="TextBlock.markdown-paragraph.note">
  <Setter Property="FontStyle" Value="Italic" />
</Style>
```

### Math (`MarkView.Avalonia.Math`)

| Class | Control | Element |
|-------|---------|---------|
| `markdown-math-block` | `Border` | Display formula (`$$…$$`, `\[…\]`) |
| `markdown-math-fallback` | `Border` | Container shown when a display formula fails to render |
| `markdown-math-container` | `Grid` | Wraps a display formula and its copy button (only when `ShowCodeBlockCopyButton` is on) |
| `markdown-math-copy` | `Button` | The formula's copy button, also a `markdown-code-copy` |
| `markdown-math-inline` | `Image` | Inline formula (`$…$`, `\(…\)`) |

The package ships no theme file: formulas are drawn as bitmaps in the theme variant's text colour.
The default theme puts the copy button in the formula's top right corner and gives the formula a
32px margin on both sides (`Grid.markdown-math-container > Border`; 38px in the GitHub preset,
whose button is wider), so it stays centred and clear of the button; restyle that margin if you
change the button's size.

### Mermaid (`MarkView.Avalonia.Mermaid`)

| Class | Control | Element |
|-------|---------|---------|
| `markdown-mermaid` | `Border` | Rendered diagram container |
| `markdown-mermaid-fallback` | `Border` | Container shown when a diagram fails to render |

Diagram colours are baked into the generated SVG at render time, so they aren't `DynamicResource`-driven on the `Border` itself. Instead `MermaidTheme.axaml` exposes Dark/Light `SolidColorBrush` resources that `MermaidBlockRenderer` reads when building each diagram:

| Resource key | Dark | Light |
|---|---|---|
| `MarkdownMermaidBackground` | `#18181B` | `#FFFFFF` |
| `MarkdownMermaidForeground` | `#FAFAFA` | `#27272A` |
| `MarkdownMermaidAccent` | `#60A5FA` | `#3B82F6` |

## Example customisations

### The whole document

The rendered document is a `Grid` with the `markdown-document` class, so a style on it changes the whole document at once. This one adds a margin around it and centres it in a column at most 720px wide (the [Docs preset](#docs) caps its width the same way):

```xml
<Style Selector="Grid.markdown-document">
  <Setter Property="Margin" Value="24,16" />
  <Setter Property="MaxWidth" Value="720" />
</Style>
```

Use this class rather than a `/template/` style on the viewer's `PART_ContentPresenter`. If you remove a `/template/` style while the app is running (for example to switch themes), Avalonia does not undo what it changed, so the document keeps the old margin or width.

### Larger headings

```xml
<Style Selector="TextBlock.markdown-h1">
  <Setter Property="FontSize" Value="36" />
  <Setter Property="FontWeight" Value="Bold" />
  <Setter Property="Margin" Value="0,16,0,8" />
</Style>
```

### Custom code block style

```xml
<Style Selector="Border.markdown-code-block">
  <Setter Property="Background" Value="#1e1e2e" />
  <Setter Property="BorderBrush" Value="#313244" />
  <Setter Property="BorderThickness" Value="1" />
  <Setter Property="CornerRadius" Value="6" />
  <Setter Property="Padding" Value="16" />
</Style>
```

### Monospace font override

```xml
<Style Selector="Border.markdown-code-block TextBlock">
  <Setter Property="FontFamily" Value="JetBrains Mono, Cascadia Code, Consolas, Courier New, monospace" />
</Style>
```

### Code block toolbar

When a code block shows its [toolbar](configuration.md#code-block-toolbar), the renderer
writes a `StackPanel.markdown-code-container` holding a `Border.markdown-code-header` and the
unchanged `Border.markdown-code-block`. The default theme rounds only the outer corners of the
pair, so if you give `Border.markdown-code-block` your own `CornerRadius` or `Margin`, also
style `StackPanel.markdown-code-container > Border.markdown-code-block` to keep them joined.

The copy button's icon (a `PathIcon`), tooltip and accessible name come from the theme, so
they can be replaced or translated:

```xml
<Style Selector="Button.markdown-code-copy">
  <Setter Property="ToolTip.Tip" Value="Copier le code" />
  <Setter Property="AutomationProperties.Name" Value="Copier le code" />
</Style>
<Style Selector="Button.markdown-code-copy.copied">
  <Setter Property="ToolTip.Tip" Value="Copié" />
</Style>
<!-- A display formula's button has the markdown-math-copy class too -->
<Style Selector="Button.markdown-code-copy.markdown-math-copy">
  <Setter Property="ToolTip.Tip" Value="Copier la formule" />
  <Setter Property="AutomationProperties.Name" Value="Copier la formule" />
</Style>
<Style Selector="Button.markdown-code-copy.markdown-math-copy.copied">
  <Setter Property="ToolTip.Tip" Value="Copié" />
</Style>
```

The formula's button is a `markdown-code-copy` too, so every style of the code block button
applies to it; a style for `Button.markdown-code-copy` alone renames both. To show the copy
buttons only while the pointer is over their block, set
[`ShowCodeBlockCopyButtonOnHover`](configuration.md#showing-copy-buttons-on-hover) rather than
writing the styles yourself: the theme's own keep a hidden button visible while it has keyboard
focus.

| Resource key | Dark | Light | Use |
|---|---|---|---|
| `MarkdownCodeHeaderBackground` | `#2D2D2D` | `#E4E4E4` | Toolbar background |
| `MarkdownCodeHeaderForeground` | `#A0A0A0` | `#5F5F5F` | Language label and copy icon (also on the page, for a display formula) |
| `MarkdownCodeCopyButtonHoverBackground` | `#1AFFFFFF` | `#14000000` | Copy button under the pointer |
| `MarkdownCodeCopyIcon` | | | `StreamGeometry` of the copy icon |
| `MarkdownCodeCopiedIcon` | | | `StreamGeometry` of the icon shown after a copy |

### Images

An image that could not be loaded is replaced by a `Border.markdown-image-failed` holding a
two-column `Grid`: the broken-image glyph (drawn in the border's `TextElement.Foreground` and sized by
its font size) and, when the image has alt text, a `TextBlock` with it. The lightbox of a clicked image
(`IsImageZoomEnabled`) is a `Panel.markdown-image-lightbox` — select it with `:is(Panel)`, since it is
a subclass — laid over the viewer, holding the `Image.markdown-image-lightbox-image` (48 pixels from the
viewer's edges in the default theme, when fitted), the `Button.markdown-image-lightbox-close`, whose
cross is drawn in its `Foreground` and sized by its `FontSize`, and the `Border.markdown-image-lightbox-toolbar`
at the bottom. The toolbar holds the `Button.markdown-image-lightbox-zoom` buttons (with
`markdown-image-lightbox-zoom-out`, `-zoom-in` or `-zoom-fit`), whose glyphs are drawn the same way, and
the `TextBlock.markdown-image-lightbox-zoom-level`. The default theme paints the toolbar in the close
button's colours. The buttons' tooltips and accessible names come from the theme, so they can be
translated:

```xml
<Style Selector="Button.markdown-image-lightbox-close">
  <Setter Property="ToolTip.Tip" Value="Fermer" />
  <Setter Property="AutomationProperties.Name" Value="Fermer" />
</Style>
<Style Selector="Button.markdown-image-lightbox-zoom-in">
  <Setter Property="ToolTip.Tip" Value="Agrandir" />
  <Setter Property="AutomationProperties.Name" Value="Agrandir" />
</Style>
<!-- A lighter backdrop, with more room around the image -->
<Style Selector=":is(Panel).markdown-image-lightbox">
  <Setter Property="Background" Value="#99000000" />
</Style>
<Style Selector="Image.markdown-image-lightbox-image">
  <Setter Property="Margin" Value="96" />
</Style>
```

| Resource key | Dark | Light | Use |
|---|---|---|---|
| `MarkdownImageFailedForeground` | `#A1A1AA` | `#52525B` | Glyph and alt text of a failed image (AA on the page and on its background) |
| `MarkdownImageFailedBackground` | `#27272A` | `#F4F4F5` | Background of a failed image |
| `MarkdownImageFailedBorder` | `#7A7A83` | `#82828B` | Border of a failed image (3:1 on the page and on its background) |
| `MarkdownImageLightboxBackdrop` | `#E0000000` | `#CC000000` | Dimmed backdrop behind the zoomed image (`#CC000000` without the theme) |
| `MarkdownImageLightboxCloseForeground` | `#FFFFFF` | `#FFFFFF` | Cross of the close button |
| `MarkdownImageLightboxCloseBackground` | `#66000000` | `#66000000` | Close button |
| `MarkdownImageLightboxCloseHoverBackground` | `#33FFFFFF` | `#33FFFFFF` | Close button under the pointer |

### Line numbers and scrolling code

With [line numbers](configuration.md#line-numbers-wrapping-and-height), the code's `TextBlock`
sits in the right column of a two-column `Grid`, next to a `TextBlock.markdown-code-line-numbers`
holding one number per row. With a maximum height, a `ScrollViewer.markdown-code-scroll` sits
between the `Border.markdown-code-block` and the code (or the grid):

```
Border.markdown-code-block
└── ScrollViewer.markdown-code-scroll      (CodeBlockMaxHeight)
    └── Grid                                (ShowCodeBlockLineNumbers)
        ├── TextBlock.markdown-code-line-numbers
        └── TextBlock                       (the code)
```

The code is then no longer the border's direct child, so style it with the descendant selector
`Border.markdown-code-block TextBlock`, as the default theme does: it also gives the line numbers
the code's font, which keeps each number level with its line. Change the font of both together,
or the numbers drift away from their lines.

```xml
<!-- Dimmer numbers, further from the code -->
<Style Selector="TextBlock.markdown-code-line-numbers">
  <Setter Property="Opacity" Value="0.7" />
  <Setter Property="Margin" Value="0,0,24,0" />
</Style>
<!-- Keep the scroll bar of a long block always visible -->
<Style Selector="ScrollViewer.markdown-code-scroll">
  <Setter Property="AllowAutoHide" Value="False" />
</Style>
```

| Resource key | Dark | Light | Use |
|---|---|---|---|
| `MarkdownCodeLineNumberForeground` | `#8A8A8A` | `#6A6A6A` | Line numbers; 4.8:1 (Dark) and 4.9:1 (Light) contrast on `MarkdownCodeBlockBackground`, above the 4.5:1 WCAG AA minimum for text |

### Find highlights

[Find matches](find-in-document.md) are drawn on top of the document by the selection overlay, not
by a style class, so their colours come from resources only. The highlight is opaque and the text
it covers is drawn again over it in its own colours; see
[Highlight colours](find-in-document.md#highlight-colours) for their contrast with the default text.

| Resource key | Dark | Light | Use |
|---|---|---|---|
| `MarkdownFindMatchBackground` | `#453A00` | `#FFF176` | Every find match but the current one |
| `MarkdownFindCurrentMatchBackground` | `#652A00` | `#FFC584` | The current find match |

## Template customisation

`MarkdownViewer`'s default `ControlTemplate` (in `MarkdownTheme.axaml`) wraps
the rendered document in a named `ScrollViewer`:

```
Border → ScrollViewer (Name="PART_ScrollViewer") → ContentPresenter
```

**Lightweight tweaks — no template override needed.** Scrollbar behavior is
exposed via the same attached `ScrollViewer.*` properties Avalonia's own
`ListBox` supports, and flows through to `PART_ScrollViewer` automatically:

```xml
<mv:MarkdownViewer ScrollViewer.VerticalScrollBarVisibility="Hidden" />
```

**Full override.** Set `Template` to replace the whole structure. `PART_ScrollViewer`
is optional — a template without it still works, but `ScrollToAnchor()` falls
back to a plain `BringIntoView()` instead of a precise scroll offset, and
`ImageResizeMode.Fill`/`ScaleDownToFit` lose their width-clamp guarantee
unless the replacement template provides an equivalent width-constraining
ancestor:

```xml
<Style Selector="mv|MarkdownViewer">
  <Setter Property="Template">
    <ControlTemplate>
      <ScrollViewer Name="PART_ScrollViewer">
        <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" />
      </ScrollViewer>
    </ControlTemplate>
  </Setter>
</Style>
```

## Live theme switching

When the user switches between `Light` and `Dark` theme variants:

- All `DynamicResource` references in `MarkdownTheme.axaml` (and in a [theme preset](#theme-presets) such as `GitHubTheme.axaml`) update automatically.
- `TextMateCodeBlockRenderer` rebuilds only the `TextBlock.Inlines` for each code block.
- `MermaidBlockRenderer` re-renders diagrams, reading `MarkdownMermaid*` resources for the new variant.
- The document scroll position is preserved in both cases.
