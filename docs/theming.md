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

### Citations

| Class | Control | Element |
|-------|---------|---------|
| `markdown-citation` | `Span` | Citation text (`""text""`), requires `UseCitations()` |

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

- All `DynamicResource` references in `MarkdownTheme.axaml` update automatically.
- `TextMateCodeBlockRenderer` rebuilds only the `TextBlock.Inlines` for each code block.
- `MermaidBlockRenderer` re-renders diagrams, reading `MarkdownMermaid*` resources for the new variant.
- The document scroll position is preserved in both cases.
