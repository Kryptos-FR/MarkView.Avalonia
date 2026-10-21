---
title: "Code blocks"
---

# Code blocks

Code blocks have a copy button above the code. The language label, line numbers, wrapping and a maximum height are opt-in, and the SyntaxHighlighting package colours the code:

```xml
<mv:MarkdownViewer ShowCodeBlockLanguage="True"
                   ShowCodeBlockLineNumbers="True"
                   WrapCodeBlockLines="True"
                   CodeBlockMaxHeight="400" />
```

```csharp
viewer.UseTextMateHighlighting();   // MarkView.Avalonia.SyntaxHighlighting
```

Like every viewer property, these can be set for every viewer with a style (see [Settings for every viewer](displaying-documents.md#settings-for-every-viewer)).

## Toolbar and copy button

The toolbar above the code holds a copy button on the right and, when `ShowCodeBlockLanguage` is on, the block's language on the left (off by default, as on GitHub). The button copies the code exactly as written in the Markdown, and shows a check mark for two seconds once the copy succeeded. The copy raises [`CopyingToClipboard`](text-selection.md#changing-what-is-copied) first, so the application can change or cancel it.

```xml
<Style Selector="mv|MarkdownViewer">
  <Setter Property="ShowCodeBlockLanguage" Value="True" />
  <Setter Property="ShowCodeBlockCopyButton" Value="False" />
</Style>
```

The toolbar is only added when it has something to show: with the copy button off, a block gets none unless `ShowCodeBlockLanguage` is on and it has a language (an indented block, or a fence without an info string, has none). Changing either property renders the document again.

The toolbar applies to the built-in code blocks and to those of the SyntaxHighlighting and Mermaid packages (fences other than `mermaid`). Diagrams have no toolbar. Selection skips the toolbar, so copying across a code block copies only the code. With the [Math](math.md#copy-button) package, `ShowCodeBlockCopyButton` also gives each display formula a copy button.

### Showing copy buttons on hover

```xml
<mv:MarkdownViewer ShowCodeBlockCopyButtonOnHover="True" />
```

The copy buttons of code blocks and formulas then stay hidden until the pointer is over their block. A hidden button keeps its place, so nothing moves when it shows, and stays a Tab stop with its accessible name: it shows while it has keyboard focus, and while it shows its check mark after a copy. The language label is not affected. The property only restyles the buttons, through the viewer's `:copy-on-hover` pseudo-class, without rendering the document again. Touch screens have no hover, so keep the default on devices used by touch alone.

## Line numbers, wrapping and height

All three are off by default, and changing one renders the document again. They apply to the same code blocks as the toolbar.

- **`ShowCodeBlockLineNumbers`** puts the number of each line in a gutter left of the code. The numbers are not part of the code: selection, `Ctrl+A` and the copy button copy the code alone, and the gutter cannot be selected. A wrapped line is numbered once, on its first row.
- **`WrapCodeBlockLines`** wraps lines longer than the block. Without it, long lines run past the block's right edge and are cut off, unless the viewer scrolls horizontally (`ScrollViewer.HorizontalScrollBarVisibility="Auto"`), in which case wrapping has no width to wrap at and does nothing. There is no wrap button in the toolbar, to keep it small; bind a toggle of your own to the property if you want one.
- **`CodeBlockMaxHeight`** caps the height of each code block, padding included and toolbar excluded (the toolbar stays in view). A taller block scrolls vertically inside itself; horizontal scrolling stays with the viewer. The mouse wheel over a block scrolls the block first, and the document once the block has reached its top or bottom, so a long block never traps it. The default, `double.PositiveInfinity`, means no limit.

## Syntax highlighting

The `MarkView.Avalonia.SyntaxHighlighting` package colours code with TextMate grammars, the ones VS Code uses:

```csharp
viewer.UseTextMateHighlighting();   // Dark+ and Light+

viewer.UseTextMateHighlighting(darkTheme: ThemeName.Monokai, lightTheme: ThemeName.QuietLight);

// every viewer
MarkdownViewerDefaults.Extensions.AddTextMateHighlighting();
```

Any `ThemeName` of `TextMateSharp.Grammars` can be used for either variant: `DarkPlus`, `LightPlus`, `Monokai`, `DimmedMonokai`, `QuietLight`, `SolarizedDark`, `SolarizedLight`, `TomorrowNightBlue`, `Abyss`, `KimbieDark`, `HighContrastDark`, `HighContrastLight` and others.

- Each line is coloured once with both themes' colours, so switching between light and dark only recolours the existing text: the document is not rebuilt, and the scroll position is kept.
- Viewers that use the same pair of themes share one highlighter, so the themes and grammars are loaded once however many viewers there are, such as one per chat message.
- A language without a grammar shows as plain code.
- With [lazy rendering](displaying-documents.md#lazy-rendering), a block is coloured when it is about to scroll into view.

To use another highlighter, implement `ICodeHighlighter`; see [Custom extensions](custom-extensions.md#writing-a-custom-code-highlighter).

## Custom fenced blocks

The fenced blocks of a language can render as your own control instead of code, without writing a Markdig renderer:

```csharp
viewer.UseFenceRenderer("chart", (code, info) => new MyChart(code));

// every viewer
MarkdownViewerDefaults.Extensions.AddFenceRenderer("chart", (code, info) => new MyChart(code));
```

The factory receives the block's content (`code`, without the fences) and its whole info string (`info`, such as `chart {height=200}` for ```` ```chart {height=200} ````), and returns a new control, or `null` to render the block as code. The language is matched case-insensitively. A fence renderer wins over Mermaid diagrams, syntax highlighting and the plain code block, whatever the order they were registered in. A factory that throws renders its block as code and logs a warning. `UseFenceRenderer` replaces the viewer's earlier fence renderer for the same language and renders the document again.

The control is wrapped in a `Border` with the `markdown-custom-fence` and `markdown-fence-<language>` classes, and selection copies it as its fenced source. See [Custom extensions](custom-extensions.md#rendering-a-fence-language-as-your-own-control) for following theme changes and what selection copies.

## Styling code blocks

Code takes its font from one resource key:

```xml
<Application.Resources>
  <FontFamily x:Key="MarkdownCodeFontFamily">JetBrains Mono, Cascadia Code, Consolas, Courier New, monospace</FontFamily>
</Application.Resources>
```

Sizes, padding and corners are keys too (`MarkdownCodeBlockPadding`, `MarkdownCodeBlockCornerRadius`…), as are the colours; see [Resource keys](../reference/resource-keys.md). A style changes the block itself:

```xml
<Style Selector="Border.markdown-code-block">
  <Setter Property="BorderBrush" Value="#313244" />
  <Setter Property="BorderThickness" Value="1" />
  <Setter Property="CornerRadius" Value="6" />
</Style>
```

With its toolbar, a block is a `StackPanel.markdown-code-container` holding the `Border.markdown-code-header` and the `Border.markdown-code-block`, which then takes `MarkdownToolbarCodeBlockTheme` rather than `MarkdownCodeBlockTheme`. Only the outer corners of the pair are rounded: if you change the corners, redefine `MarkdownCodeHeaderCornerRadius` and `MarkdownToolbarCodeBlockCornerRadius` along with `MarkdownCodeBlockCornerRadius` to keep them joined.

The copy button's icon, tooltip and accessible name come from the theme, so they can be replaced or translated. A display formula's button is a `markdown-code-copy` too, with the `markdown-math-copy` class:

```xml
<Style Selector="Button.markdown-code-copy">
  <Setter Property="ToolTip.Tip" Value="Copier le code" />
  <Setter Property="AutomationProperties.Name" Value="Copier le code" />
</Style>
<Style Selector="Button.markdown-code-copy.copied">
  <Setter Property="ToolTip.Tip" Value="Copié" />
</Style>
<Style Selector="Button.markdown-code-copy.markdown-math-copy">
  <Setter Property="ToolTip.Tip" Value="Copier la formule" />
  <Setter Property="AutomationProperties.Name" Value="Copier la formule" />
</Style>
```

With line numbers or a maximum height, the code is no longer the border's direct child:

```
Border.markdown-code-block
└── ScrollViewer.markdown-code-scroll      (CodeBlockMaxHeight)
    └── Grid                                (ShowCodeBlockLineNumbers)
        ├── TextBlock.markdown-code-line-numbers
        └── TextBlock                       (the code)
```

Style the code with the descendant selector `Border.markdown-code-block TextBlock`, as the theme does: it also gives the line numbers the code's font, which keeps each number level with its line.

```xml
<!-- Dimmer numbers, further from the code -->
<Style Selector="TextBlock.markdown-code-line-numbers">
  <Setter Property="Opacity" Value="0.7" />
  <Setter Property="Margin" Value="0,0,24,0" />
</Style>
<!-- Keep the scroll bar of a long block visible -->
<Style Selector="ScrollViewer.markdown-code-scroll">
  <Setter Property="AllowAutoHide" Value="False" />
</Style>
```
