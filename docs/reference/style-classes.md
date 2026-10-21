---
title: "Style classes and control themes"
---

# Style classes and control themes

Every control of a rendered document has a `markdown-*` style class for its role, and most get a control theme, found by resource key from the viewer. Redefine a control theme for changes to the whole document, or write a style for a class; see [Themes and presets](../guides/themes.md) for how they combine and what each costs.

Headings and paragraphs are `MarkdownSelectableTextBlock`s, a subclass of `TextBlock`: select them with `:is(TextBlock).markdown-h1`, since a type selector only matches the exact type.

## Core

| Class | Control | Element |
|-------|---------|---------|
| `markdown-document` | `Grid` | The whole rendered document (`MarkdownViewer.Content`): its root panel with the selection and link focus layers. Style it for changes to the whole document, such as its margins or reading width (see [The whole document](../guides/themes.md#styles)) |
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
| `markdown-custom-fence` | `Border` | Wraps the control a [fence renderer](../guides/custom-extensions.md#rendering-a-fence-language-as-your-own-control) returned for a fenced block |
| `markdown-fence-<language>` | `Border` | The same `Border`, by fence language in lower case (e.g. `markdown-fence-chart`) |
| `markdown-code-inline` | `Border` | Inline code container |
| `markdown-blockquote` | `Border` | Blockquote container |
| `markdown-list` | `StackPanel` | Ordered / unordered list |
| `markdown-list-marker` | `TextBlock` | List item bullet, number or task-list check glyph |
| `markdown-task-list` | `TextBlock` | Task-list check glyph: a box, ticked when checked, drawn in its `Foreground` and sized by its `FontSize` (its text, ☐ or ☑, is what selection copies) |
| `markdown-task-toggle` | `ToggleButton` | Clickable task-list checkbox wrapping the glyph, when `AreTaskListsInteractive` is set; `:checked` when ticked |
| `markdown-thematic-break` | `Separator` | Horizontal rule |
| `markdown-image` | `Image` | Rendered image |
| `markdown-image-failed` | `Border` | Placeholder of an image that could not be loaded: a broken-image glyph and the alt text (see [Images](../guides/images.md#styling-images)) |
| `markdown-image-lightbox` | `Panel` | Lightbox a clicked image opens in, over the viewer (see [Images](../guides/images.md#styling-images)) |
| `markdown-image-lightbox-image` | `Image` | The image in the lightbox |
| `markdown-image-lightbox-close` | `Button` | The lightbox's close button |
| `markdown-image-lightbox-toolbar` | `Border` | The lightbox's zoom toolbar, at the bottom |
| `markdown-image-lightbox-zoom` | `Button` | Each button of the zoom toolbar, along with `markdown-image-lightbox-zoom-out`, `-zoom-in` or `-zoom-fit`, and the save button |
| `markdown-image-lightbox-save` | `Button` | The toolbar's save button, at its end (`IsImageSaveEnabled`) |
| `markdown-image-lightbox-zoom-level` | `TextBlock` | The zoom level in the toolbar, as a percentage of the natural size |
| `markdown-link` | `HyperlinkButton` | Hyperlink |
| `markdown-context-copy`, `markdown-context-copy-markdown`, `markdown-context-copy-link`, `markdown-context-copy-heading-link`, `markdown-context-copy-table`, `markdown-context-copy-table-markdown`, `markdown-context-save-image`, `markdown-context-select-all` | `MenuItem` | Items of the viewer's default [context menu](../guides/text-selection.md#context-menu) |
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

## EmphasisExtras

| Class | Control | Element |
|-------|---------|---------|
| `markdown-marked` | `Span` | Highlighted text (`==text==`) |
| `markdown-subscript` | `Span` | Subscript (`~text~`, `<sub>`): the font's `subs` glyphs on the subscript baseline |
| `markdown-superscript` | `Span` | Superscript (`^text^`, `<sup>`): the font's `sups` glyphs on the superscript baseline |

Underline (inserted), bold, italic, and strikethrough use standard Avalonia inlines and properties (`Underline`, `Bold`, `Italic`, `TextDecorations`) and do not have dedicated style classes.

## Citations

| Class | Control | Element |
|-------|---------|---------|
| `markdown-citation` | `Span` | Citation text (`""text""`), requires `UseCitations()` |

## Alert blocks

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

## Details blocks

HTML `<details>` / `<summary>` (see [HTML details blocks](../guides/markdown.md#details-blocks)).

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

## Foldable headings

Headings with a fold button (see [Foldable headings](../guides/folding.md#foldable-headings)), when
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

## Thinking blocks

A language model's reasoning (see [Thinking blocks](../guides/chat.md#thinking-blocks)).

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

## HTML subset

The HTML elements of the [HTML subset](../guides/markdown.md#html-subset). The others render with the controls and classes of their markdown counterparts: `<b>`, `<i>`, `<ins>`, `<del>` as `Bold`, `Italic`, `Underline` and a struck-through `Span`; `<mark>` as `Span.markdown-marked`; `<sub>` and `<sup>` as `markdown-subscript` and `markdown-superscript`; `<code>` as `markdown-code-inline`; `<a>` as `markdown-link`; `<img>` as `markdown-image`; `<p>` and headings as `markdown-paragraph` and `markdown-h1`…`markdown-h6`; `<hr>` as `markdown-thematic-break`.

| Class | Control | Element |
|-------|---------|---------|
| `markdown-html-kbd` | `Border` | `<kbd>`: a key around a `TextBlock` of its text, which sits on the line's baseline |
| `markdown-html-align` | `StackPanel` | Blocks aligned by an `align` attribute: a `<div align>` and the blocks in it, or the paragraph or heading of a `<p align>` / `<h1 align>`. Its inherited `TextBlock.TextAlignment` aligns the text inside it, set by the control |
| `markdown-html-align-left`, `-center`, `-right`, `-justify` | `StackPanel` | The same panel, by alignment |

The key's colours are the `MarkdownKbd*` [resource keys](resource-keys.md#colours). For example, a flatter key:

```xml
<Style Selector="Border.markdown-html-kbd">
  <Setter Property="BorderThickness" Value="1" />
  <Setter Property="CornerRadius" Value="2" />
</Style>
<Style Selector="Border.markdown-html-kbd > TextBlock">
  <Setter Property="FontFamily" Value="Inter" />
</Style>
```

## Figures

| Class | Control | Element |
|-------|---------|---------|
| `markdown-figure` | `Border` | Figure container (centred) |
| `markdown-figure-caption` | `TextBlock` | Caption text |

## Abbreviations

| Class | Control | Element |
|-------|---------|---------|
| `markdown-abbr` | `TextBlock` | Abbreviated term with tooltip |

## Footnotes

| Class | Control | Element |
|-------|---------|---------|
| `markdown-footnote-ref` | `HyperlinkButton` | Inline footnote reference `[1]` |
| `markdown-footnote-separator` | `Separator` | Line above the definitions; also has `markdown-thematic-break`, so it looks like a horizontal rule unless styled |
| `markdown-footnote-group` | `StackPanel` | Definition list at end of document |
| `markdown-footnote-item` | `Grid` | Individual footnote row |

## Definition lists

Requires Markdig's `UseDefinitionLists()`.

| Class | Control | Element |
|-------|---------|---------|
| `markdown-definition-list` | `StackPanel` | The whole list |
| `markdown-definition-term` | `TextBlock` | A defined term |
| `markdown-definition` | `StackPanel` | One definition, indented |

## Custom containers

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

## Footers

Requires Markdig's `UseFooters()`.

| Class | Control | Element |
|-------|---------|---------|
| `markdown-footer` | `StackPanel` | The `^^` footer lines |

## Classes from generic attributes

With Markdig's `UseGenericAttributes()` (called last), `{.name}` after an element adds `name` to the style classes of the control or inline rendered for it, next to its `markdown-*` classes:

```markdown
Read the docs first. {.note}
```

```xml
<Style Selector=":is(TextBlock).markdown-paragraph.note">
  <Setter Property="FontStyle" Value="Italic" />
</Style>
```

## Math

With the `MarkView.Avalonia.Math` package.

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

## Mermaid

With the `MarkView.Avalonia.Mermaid` package.

| Class | Control | Element |
|-------|---------|---------|
| `markdown-mermaid` | `Border` | Rendered diagram container |
| `markdown-mermaid-fallback` | `Border` | Container shown when a diagram fails to render |

Diagram colours are baked into the drawing when it is rendered, so a style on the `Border` does not change them; they come from the `MarkdownMermaid*` [resource keys](resource-keys.md#mermaid-diagrams) of `MermaidTheme.axaml`.

## Control themes

| Theme | Control | Role |
|---|---|---|
| `MarkdownDocumentTheme` | `Grid` | The whole document (`Grid.markdown-document`); defined only by the Docs preset |
| `MarkdownHeading1Theme` … `MarkdownHeading6Theme` | `MarkdownSelectableTextBlock` | Headings |
| `MarkdownParagraphTheme`, `MarkdownListParagraphTheme`, `MarkdownTableParagraphTheme`, `MarkdownQuoteParagraphTheme`, `MarkdownAlertParagraphTheme` | `MarkdownSelectableTextBlock` | Paragraphs; in an alert, otherwise in a table cell, otherwise in a quote, otherwise in a list item |
| `MarkdownCodeBlockTheme`, `MarkdownToolbarCodeBlockTheme` | `Border` | Code blocks, without and with a toolbar |
| `MarkdownCodeTheme`, `MarkdownCodeLineNumbersTheme` | `TextBlock` | A code block's text and its line numbers |
| `MarkdownCodeContainerTheme`, `MarkdownCodeHeaderTheme`, `MarkdownCodeLanguageTheme` | `StackPanel`, `Border`, `TextBlock` | A code block with its toolbar, the toolbar, the language label |
| `MarkdownCustomFenceTheme`, `MarkdownMathContentTheme` | `Border` | A custom fenced block's control; a display formula next to its copy button |
| `MarkdownCodeInlineTheme`, `MarkdownParagraphCodeInlineTheme` | `Run` | Inline code in a paragraph, and elsewhere (a heading, for example) |
| `MarkdownMarkedTheme`, `MarkdownCitationTheme` | `Span` | `==marked==` text; `""citations""` |
| `MarkdownKbdTheme`, `MarkdownKbdTextTheme` | `Border`, `TextBlock` | `<kbd>` keys |
| `MarkdownAbbreviationTheme` | `TextBlock` | Abbreviations |
| `MarkdownBlockquoteTheme` | `Border` | Blockquotes |
| `MarkdownListTheme`, `MarkdownNestedListTheme`, `MarkdownTaskMarkerTheme` | `StackPanel`, `StackPanel`, `TextBlock` | Lists, nested lists, a task's check box glyph |
| `MarkdownTableTheme`, `MarkdownTableHeaderContentTheme` | `Grid`, `StackPanel` | Tables; the content of a header cell |
| `MarkdownTableCellTheme`, `MarkdownTableFirstRowCellTheme`, `MarkdownTableFirstColumnCellTheme`, `MarkdownTableCornerCellTheme` | `Border` | Table cells, by position (header and even rows have the `markdown-table-header` and `markdown-table-row-even` classes) |
| `MarkdownAlertTheme`, `MarkdownAlert<Kind>Theme` | `Border` | Alerts of another kind, and of each of the five GitHub kinds |
| `MarkdownAlertHeaderTheme`, `MarkdownAlert<Kind>HeaderTheme` | `TextBlock` | Alert titles; a foldable title has `:expanded` or `:collapsed` |
| `MarkdownAlertContentTheme` | `StackPanel` | An alert's body; defined only by the Docs preset |
| `MarkdownDetailsTheme`, `MarkdownDetailsSummaryTheme` | `Border`, `TextBlock` | `<details>` blocks and their summary |
| `MarkdownThinkingTheme`, `MarkdownThinkingHeaderTheme`, `MarkdownThinkingContentTheme` | `Border`, `TextBlock`, `Border` | Thinking blocks |
| `MarkdownFigureTheme`, `MarkdownFigureCaptionTheme` | `Border`, `MarkdownSelectableTextBlock` | Figures |
| `MarkdownDefinitionTermTheme`, `MarkdownDefinitionTheme` | `MarkdownSelectableTextBlock`, `StackPanel` | Definition lists |
| `MarkdownCustomContainerTheme`, `MarkdownCustomContainerTitleTheme` | `Border`, `MarkdownSelectableTextBlock` | Custom containers |
| `MarkdownFootnoteGroupTheme`, `MarkdownFootnoteItemTheme` | `StackPanel`, `Grid` | The footnotes, and each of them |
| `MarkdownFooterTheme` | `StackPanel` | Footers |
| `MarkdownImageFailedTheme` | `Border` | The placeholder of an image that failed to load |
| `MarkdownImageLightboxBackingTheme`, `MarkdownImageLightboxToolbarTheme`, `MarkdownImageLightboxZoomLevelTheme` | `Border`, `Border`, `TextBlock` | The image lightbox |
| `MarkdownYoutubeOverlayTheme`, `MarkdownYoutubePlayTheme` | `Grid`, `TextBlock` | A YouTube thumbnail's overlay and play glyph |
| `MarkdownMermaidTheme` | `Border` | Mermaid diagrams (`MermaidTheme.axaml`) |

A viewer looks its themes up when it renders, and again when its resources change, so adding or removing a preset or a theme at run time restyles the open document in place.
