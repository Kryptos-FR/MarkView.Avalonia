# Theming

## Including the default theme

`MarkdownTheme.axaml` is an embedded resource in the `MarkView.Avalonia` package. Include it in `App.axaml`:

```xml
<Application.Styles>
  <FluentTheme />
  <StyleInclude Source="avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml" />
</Application.Styles>
```

The theme uses `DynamicResource` throughout so it responds to Avalonia's `RequestedThemeVariant` changes automatically.

## Style class reference

Every element rendered by `MarkdownViewer` is tagged with a CSS-style class name. Override any selector in your own `Styles` block to customise appearance.

### Core

| Class | Control | Element |
|-------|---------|---------|
| `markdown-h1` … `markdown-h6` | `TextBlock` | Headings |
| `markdown-paragraph` | `TextBlock` | Paragraphs |
| `markdown-code-block` | `Border` | Fenced code block container |
| `markdown-code-inline` | `Border` | Inline code container |
| `markdown-blockquote` | `Border` | Blockquote container |
| `markdown-list` | `StackPanel` | Ordered / unordered list |
| `markdown-thematic-break` | `Separator` | Horizontal rule |
| `markdown-image` | `Image` | Rendered image |
| `markdown-link` | `HyperlinkButton` | Hyperlink |
| `markdown-table` | `Grid` | Table |
| `markdown-table-cell` | `Border` | Table body cell |
| `markdown-table-header` | `Border` | Table header cell |

### EmphasisExtras

| Class | Control | Element |
|-------|---------|---------|
| `markdown-marked` | `Span` | Highlighted text (`==text==`) |

Subscript, superscript, underline (inserted), bold, italic, and strikethrough use standard Avalonia inline properties (`BaselineAlignment`, `FontFeatures`, `TextDecorations`) and do not have dedicated style classes.

### Alert blocks

| Class | Control | Element |
|-------|---------|---------|
| `markdown-alert` | `Border` | Alert container |
| `markdown-alert-note` | `Border` | NOTE variant |
| `markdown-alert-tip` | `Border` | TIP variant |
| `markdown-alert-important` | `Border` | IMPORTANT variant |
| `markdown-alert-warning` | `Border` | WARNING variant |
| `markdown-alert-caution` | `Border` | CAUTION variant |
| `markdown-alert-header` | `TextBlock` | Alert kind label (e.g. "NOTE") |
| `markdown-alert-content` | `StackPanel` | Alert body |

Example — colour the NOTE variant:

```xml
<Style Selector="Border.markdown-alert-note">
  <Setter Property="BorderBrush" Value="#3b82f6" />
  <Setter Property="Background" Value="#eff6ff" />
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
| `markdown-footnote-group` | `StackPanel` | Definition list at end of document |
| `markdown-footnote-item` | `Grid` | Individual footnote row |

## Example customisations

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

## Live theme switching

When the user switches between `Light` and `Dark` theme variants:

- All `DynamicResource` references in `MarkdownTheme.axaml` update automatically.
- `TextMateCodeBlockRenderer` rebuilds only the `TextBlock.Inlines` for each code block.
- `MermaidBlockRenderer` re-renders diagrams with updated colour variables.
- The document scroll position is preserved in both cases.
