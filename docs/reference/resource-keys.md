---
title: "Resource keys"
---

# Resource keys

The themes read every colour, size and spacing of the document from resource keys, through `DynamicResource`, so redefining a key changes the open document at once. See [Themes and presets](../guides/themes.md#changing-colours) for where to redefine them, and [Theme presets](theme-presets.md) for the values each preset sets.

## Colours

`MarkdownTheme.axaml` defines its colours as `SolidColorBrush`es in its `Light` and `Dark` theme dictionaries. Colours are `#AARRGGBB` when translucent. The defaults meet WCAG 2.2 AA contrast; see [Accessibility](../guides/accessibility.md#colour-contrast).

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
| `MarkdownKbdBackground` | `<kbd>` key background | `#151B23` | `#F6F8FA` |
| `MarkdownKbdForeground` | `<kbd>` key text | `#F0F6FC` | `#1F2328` |
| `MarkdownKbdBorder` | `<kbd>` key outline (thicker at the bottom) | `#6E7681` | `#818B98` |

The theme also defines two `StreamGeometry` resources, the same in both variants: `MarkdownCodeCopyIcon` and `MarkdownCodeCopiedIcon`, the copy button's icons. A few colours are the same in both variants and sit outside the `Light` and `Dark` dictionaries: `MarkdownTableHeaderBackground` (`#20808080`), `MarkdownFigureBorder` and `MarkdownThinkingBorder` (`#40808080`), and `MarkdownCodeBlockBorder`, the outline of code blocks and their toolbar (`Transparent`, drawn only in high contrast unless a preset gives it a width). The dimming and outline widths that high contrast changes are keys too: `MarkdownBlockquoteOpacity` and `MarkdownFootnoteOpacity` (0.85), `MarkdownFigureCaptionOpacity` and `MarkdownThinkingHeaderOpacity` (0.75), `MarkdownThinkingContentOpacity` (0.8), `MarkdownHeadingFoldOpacity` (0.7), `MarkdownCodeBlockBorderThickness` and `MarkdownCodeHeaderBorderThickness` (0).

## Images

| Resource key | Dark | Light | Use |
|---|---|---|---|
| `MarkdownImageFailedForeground` | `#A1A1AA` | `#52525B` | Glyph and alt text of a failed image (AA on the page and on its background) |
| `MarkdownImageFailedBackground` | `#27272A` | `#F4F4F5` | Background of a failed image |
| `MarkdownImageFailedBorder` | `#7A7A83` | `#82828B` | Border of a failed image (3:1 on the page and on its background) |
| `MarkdownImageLightboxBackdrop` | `#E0000000` | `#CC000000` | Dimmed backdrop behind the zoomed image (`#CC000000` without the theme) |
| `MarkdownImageLightboxCloseForeground` | `#FFFFFF` | `#FFFFFF` | Cross of the close button |
| `MarkdownImageLightboxCloseBackground` | `#66000000` | `#66000000` | Close button |
| `MarkdownImageLightboxCloseHoverBackground` | `#33FFFFFF` | `#33FFFFFF` | Close button under the pointer |

## Find highlights

[Find matches](../guides/find-in-document.md) are drawn over the document by the selection overlay, not by a style class, so their colours come from these keys only. The highlight is opaque, and the text it covers is drawn again over it in its own colours; see [Highlight colours](../guides/find-in-document.md#highlight-colours) for their contrast.

| Resource key | Dark | Light | Use |
|---|---|---|---|
| `MarkdownFindMatchBackground` | `#453A00` | `#FFF176` | Every find match but the current one |
| `MarkdownFindCurrentMatchBackground` | `#652A00` | `#FFC584` | The current find match |

## Mermaid diagrams

Defined by the Mermaid package's `MermaidTheme.axaml`. The colours are baked into each diagram when it is drawn, and read again when the theme variant changes. Without the theme file, the same values are built in.

| Resource key | Dark | Light |
|---|---|---|
| `MarkdownMermaidBackground` | `#18181B` | `#FFFFFF` |
| `MarkdownMermaidForeground` | `#FAFAFA` | `#27272A` |
| `MarkdownMermaidAccent` | `#60A5FA` | `#3B82F6` |

## High contrast

The `MarkdownHighContrast*` keys are the palette of the high-contrast mode; see [High contrast](../guides/accessibility.md#high-contrast).

## Sizes and spacing

The [control themes](style-classes.md#control-themes) read their sizes and spacing from these keys, the same in both variants:

| Resource key | Default | Used for |
|---|---|---|
| `MarkdownCodeFontFamily`, `MarkdownCodeFontSize`, `MarkdownCodeLineHeight` | Cascadia Code, Consolas, Courier New, monospace; 13; `NaN` (the font's own) | Code blocks (the font family also for inline code) |
| `MarkdownCodeBlockCornerRadius`, `MarkdownCodeBlockPadding`, `MarkdownCodeBlockMargin` | 4; 12,8; 0,4 | A code block without a toolbar |
| `MarkdownToolbarCodeBlockCornerRadius`, `MarkdownToolbarCodeBlockPadding`, `MarkdownToolbarCodeBlockBorderThickness` | 0,0,4,4; 12,8; 0 | A code block under its [toolbar](../guides/code-blocks.md#toolbar-and-copy-button), which takes the top corners |
| `MarkdownCodeHeaderCornerRadius`, `MarkdownCodeHeaderPadding` | 4,4,0,0; 12,2,4,2 | The toolbar |
| `MarkdownCodeContainerMargin`, `MarkdownCustomFenceMargin` | 0,4 | A code block with its toolbar; a [custom fenced block](../guides/code-blocks.md#custom-fenced-blocks)'s control |
| `MarkdownMathContentMargin` | 32,0 | Room for a display formula's copy button on both sides |
| `MarkdownParagraphMargin`, `MarkdownParagraphLineSpacing` | 0,2; 0 | Paragraphs |
| `MarkdownListParagraphMargin`, `MarkdownTableParagraphMargin`, `MarkdownQuoteParagraphMargin`, `MarkdownAlertParagraphMargin` | 0,2 | Paragraphs in a list item, a table cell, a quote or an alert |
| `MarkdownTableParagraphLineSpacing`, `MarkdownAlertParagraphLineSpacing` | 0 | Line spacing in a table cell or an alert |
| `MarkdownListMargin`, `MarkdownNestedListMargin` | 16,4,0,4 | Lists, and lists inside a list item |
| `MarkdownTableMargin`, `MarkdownTableCellPadding` | 0,4; 8,4 | Tables |
| `MarkdownTableCellBorderThickness`, `MarkdownTableFirstRowCellBorderThickness`, `MarkdownTableFirstColumnCellBorderThickness`, `MarkdownTableCornerCellBorderThickness` | 0,0,1,1 | Cell borders: each cell draws its right and bottom ones, and those of the first row and column can add the top and left ones |
| `MarkdownTableHeaderFontWeight` | Bold | The header row |
| `MarkdownAlertCornerRadius`, `MarkdownAlertPadding`, `MarkdownAlertMargin` | 0,4,4,0; 12,8; 0,4 | Alerts (their bar is `MarkdownAlertBorderThickness`, 4,0,0,0) |
| `MarkdownAlertHeaderFontWeight`, `MarkdownAlertHeaderFontSize`, `MarkdownAlertHeaderMargin` | SemiBold; 13; 0,0,0,4 | Alert titles |
| `MarkdownDetailsMargin`, `MarkdownDefinitionMargin` | 0,4; 24,0,0,0 | `<details>` blocks; definitions |
| `MarkdownCustomContainerCornerRadius`, `MarkdownCustomContainerMargin` | 4; 0,4 | Custom containers |

## Text

The labels of the context menu are string resources, listed under [Context menu](../guides/text-selection.md#context-menu), and so is the text of the expand button, under [Previews](../guides/displaying-documents.md#previews). The tooltips and accessible names of buttons (copy, lightbox, fold) come from the theme's styles; see [Code blocks](../guides/code-blocks.md#styling-code-blocks) and [Images and diagrams](../guides/images.md#styling-images).
