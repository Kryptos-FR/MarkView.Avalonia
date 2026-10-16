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
| `markdown-code-container` | `StackPanel` | Wraps a code block and its toolbar (only when the toolbar is shown) |
| `markdown-code-header` | `Border` | Code block toolbar |
| `markdown-code-language` | `TextBlock` | Language label in the toolbar |
| `markdown-code-copy` | `Button` | Copy button in the toolbar; also has the `copied` class for two seconds after a copy |
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
<!-- Show the copy button only while the pointer is over the code block -->
<Style Selector="StackPanel.markdown-code-container Button.markdown-code-copy">
  <Setter Property="Opacity" Value="0" />
</Style>
<Style Selector="StackPanel.markdown-code-container:pointerover Button.markdown-code-copy">
  <Setter Property="Opacity" Value="1" />
</Style>
```

| Resource key | Dark | Light | Use |
|---|---|---|---|
| `MarkdownCodeHeaderBackground` | `#2D2D2D` | `#E4E4E4` | Toolbar background |
| `MarkdownCodeHeaderForeground` | `#A0A0A0` | `#5F5F5F` | Language label and copy icon |
| `MarkdownCodeCopyButtonHoverBackground` | `#1AFFFFFF` | `#14000000` | Copy button under the pointer |
| `MarkdownCodeCopyIcon` | | | `StreamGeometry` of the copy icon |
| `MarkdownCodeCopiedIcon` | | | `StreamGeometry` of the icon shown after a copy |

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
