---
title: "Theme presets"
---

# Theme presets

A theme preset is a style file included after `MarkdownTheme.axaml` that gives the whole document another look. It builds on the default theme rather than replacing it: it redefines the [colour keys](resource-keys.md#colours) and the [size and spacing keys](resource-keys.md#sizes-and-spacing), redefines a few control themes (headings, inline code in paragraphs, blockquotes) and adds styles only for the viewer, buttons, rules, links and images, so the default theme, which holds the viewer's template, must still be included before it. See [Themes and presets](../guides/themes.md#presets) to choose one, switch presets at run time or override a preset's values.

## GitHub

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
| Document | GitHub's page background and text colour (`#FFFFFF` / `#1F2328` light, `#0D1117` / `#F0F6FC` dark) set on `MarkdownViewer`, its system font stack (Segoe UI, SF Pro Text, Helvetica Neue, Noto Sans, Helvetica, Arial, then the Inter the application embeds with `WithInterFont()`, for platforms without those fonts such as the browser) at 16px, and 16px between paragraphs with a little extra line spacing |
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

**In high contrast.** When the viewer has the `:high-contrast` pseudo-class (see [High contrast](../guides/accessibility.md#high-contrast)), the preset uses the colours of Primer's "dark high contrast" and "light high contrast" themes for the `MarkdownHighContrast*` keys above, every pair 7:1 or more, and gives up its muted text, striped table rows and grey rules and borders for them, as the default theme does. Find highlights keep the preset's colours, on which its high-contrast text stays above 8:1 but links drop to 4.3:1 (dark) and 4.7:1 (light).

**Next to the extension packages.** Their themes stay as they are. Syntax highlighting (`MarkView.Avalonia.SyntaxHighlighting`) keeps its TextMate colours, Dark+ and Light+ by default, drawn on the preset's code background: GitHub's own highlighting colours differ, but Dark+ and Light+ have at least the same contrast on it as on the default theme's (the light background is a little lighter, the dark one a little darker). Mermaid diagrams keep the colours of `MermaidTheme.axaml`: in the light variant their white background matches the page, while in the dark one their `#18181B` background shows as a slightly lighter card on GitHub's `#0D1117`; redefine `MarkdownMermaidBackground` in the `Dark` dictionary to blend it in. Math formulas follow the theme variant on their own.

## Chat

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

## Docs

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
| Document | VitePress' page background and text colour (`#FFFFFF` / `#3C3C43` light, `#1B1B1F` / `#DFDFD6` dark) set on `MarkdownViewer`, Inter (the one the application embeds with `WithInterFont()`, else an installed one, then Segoe UI, SF Pro Text, Helvetica Neue, Noto Sans, Helvetica, Arial) at 16px, paragraphs 16px apart with 6px of extra line spacing. The document is at most 768px wide, centred in a wider viewer (the `markdown-document` style class) |
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

The current find match is a paler orange than the default theme's in the light variant, so inline code, drawn in the link colour, keeps 4.5:1 on it. The other find highlight, the image lightbox and the `MarkdownHighContrast*` palette keep the default theme's colours. In high contrast the preset gives up its own page colours, muted text, soft table headers and striped rows for that palette, the divider above h2 turns solid, and alerts, which have no bar, get a 1px outline (`MarkdownAlertBorderThickness`, which `MarkdownHighContrastPalette` takes from `MarkdownHighContrastAlertBorderThickness` in high contrast).
