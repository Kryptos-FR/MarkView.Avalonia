---
title: "Themes and presets"
---

# Themes and presets

The document's look comes from `MarkdownTheme.axaml`. A preset included after it gives the whole document another look, and resource keys, control themes and styles change any part of it:

```xml
<Application.Styles>
  <FluentTheme />
  <StyleInclude Source="avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml" />
  <StyleInclude Source="avares://MarkView.Avalonia/Themes/GitHubTheme.axaml" />   <!-- optional -->
</Application.Styles>
```

## The default theme

`MarkdownTheme.axaml` holds the colours, sizes and control themes of the document, and the viewer's `ControlTemplate`, whose `ScrollViewer` makes the document scroll: without the theme, the document renders but cannot scroll, and `ScrollToAnchor()` does nothing. Its colours are `DynamicResource`s in `Light` and `Dark` dictionaries, so the document follows the application's theme variant live, and they meet WCAG 2.2 AA contrast (see [Accessibility](accessibility.md#colour-contrast)).

The Mermaid package has a theme file of its own for diagram colours, included the same way; it does not depend on the core theme, so the order does not matter. See [Choosing packages](../packages.md#theme-files).

## Presets

A preset builds on the default theme, which must still be included before it: it redefines colours and sizes and a few control themes. Each follows the light and dark variants live and keeps every text colour at 4.5:1 contrast or more.

| Preset | Look | Page |
|---|---|---|
| `GitHubTheme.axaml` | Markdown on github.com: GitHub's fonts, spacing and Primer colours | Sets its own background, text colour and font (16px) |
| `ChatTheme.axaml` | Compact, for chat messages and narrow panels: small headings, 8px between blocks, code in cards | Transparent; inherits the font, size and colour of the bubble around it |
| `DocsTheme.axaml` | A documentation site, after VitePress: Inter at 16px, a reading column at most 768px wide, tinted alerts | Sets its own background, text colour and font |

```xml
<StyleInclude Source="avares://MarkView.Avalonia/Themes/ChatTheme.axaml" />
```

[Theme presets](../reference/theme-presets.md) lists what each one changes, where it departs from its model, and the keys it sets.

### Switching presets at run time

Add or remove the preset's `StyleInclude`; every open document restyles in place, without being rendered again:

```csharp
var gitHub = new StyleInclude((Uri?)null)
{
    Source = new Uri("avares://MarkView.Avalonia/Themes/GitHubTheme.axaml"),
};
var styles = Application.Current!.Styles;
// A StyleInclude written in App.axaml is compiled into the theme's own Styles, so find the default
// theme by one of its resources rather than by type or Source.
var markdownTheme = styles.First(s => s.TryGetResource("MarkdownCodeBlockBackground", ThemeVariant.Dark, out _));
// Right after MarkdownTheme.axaml, so the application's own styles keep winning
styles.Insert(styles.IndexOf(markdownTheme) + 1, gitHub);
// ... and back to the default look:
styles.Remove(gitHub);
```

## Changing colours

Every colour is a resource key, such as `MarkdownLinkForeground` or `MarkdownCodeBlockBackground` (the [resource key reference](../reference/resource-keys.md) lists them). Redefine a key for the variants you want. A key defined closer to the viewer wins, so define it in `Application.Resources` (searched before `Application.Styles`), in a `Styles` block included after the theme and preset, or in the `Resources` of a window or of the viewer itself:

```xml
<Application.Resources>
  <ResourceDictionary>
    <ResourceDictionary.ThemeDictionaries>
      <ResourceDictionary x:Key="Light">
        <SolidColorBrush x:Key="MarkdownLinkForeground">#0B57D0</SolidColorBrush>
      </ResourceDictionary>
      <ResourceDictionary x:Key="Dark">
        <SolidColorBrush x:Key="MarkdownLinkForeground">#A8C7FA</SolidColorBrush>
      </ResourceDictionary>
    </ResourceDictionary.ThemeDictionaries>
  </ResourceDictionary>
</Application.Resources>
```

A value set on the viewer itself wins over both themes: `<mv:MarkdownViewer Background="Transparent" FontSize="14" />`. The GitHub and Docs presets set `Background`, `Foreground`, `FontFamily` and `FontSize` on the viewer (through the `MarkdownBackground` and `MarkdownForeground` keys for the colours), so local values replace them for the whole document.

## Sizes, spacing and control themes

Each control of the document gets a [control theme](https://docs.avaloniaui.net/docs/basics/user-interface/styling/control-themes) for its role, found by resource key from the viewer: a paragraph gets `MarkdownParagraphTheme`, a code block `MarkdownCodeBlockTheme`, and so on. The themes read their sizes and spacing from keys such as `MarkdownParagraphMargin` or `MarkdownCodeBlockPadding`, so most of the look changes by redefining keys, the same way as colours:

```xml
<Application.Resources>
  <Thickness x:Key="MarkdownParagraphMargin">0,6</Thickness>
  <FontFamily x:Key="MarkdownCodeFontFamily">JetBrains Mono, Consolas, monospace</FontFamily>
</Application.Resources>
```

For what keys don't cover, redefine a whole control theme under the same key, with a `TargetType` the control derives from. It replaces the default one, so repeat the setters you want to keep:

```xml
<!-- h1 in a serif font, with a line under it -->
<ControlTheme x:Key="MarkdownHeading1Theme" TargetType="mr:MarkdownSelectableTextBlock"
              xmlns:mr="using:MarkView.Avalonia.Rendering">
  <Setter Property="FontFamily" Value="Georgia, serif" />
  <Setter Property="FontSize" Value="30" />
  <Setter Property="FontWeight" Value="SemiBold" />
  <Setter Property="Margin" Value="0,16,0,8" />
  <Setter Property="Padding" Value="0,0,0,6" />
  <Setter Property="BorderBrush" Value="{DynamicResource MarkdownBlockquoteBorder}" />
  <Setter Property="BorderThickness" Value="0,0,0,1" />
</ControlTheme>
```

- A theme's nested styles must start from the control itself (`^:nth-child(1)`, `^.markdown-table-header`, `^:expanded`). A selector that names an ancestor first, such as `:is(mv|MarkdownViewer):high-contrast ^`, compiles without an error but matches every control.
- To change a value in high contrast, use the `MarkdownViewer.InHighContrast` attached property, which every control of a document in high contrast inherits: `^[(mv|MarkdownViewer.InHighContrast)=True]`.
- The viewer looks its themes up when it renders, and again when its resources change, so adding or removing a theme at run time restyles the open document.

The [style class reference](../reference/style-classes.md#control-themes) lists every control theme, and the [resource key reference](../reference/resource-keys.md#sizes-and-spacing) every size key.

## Styles

Every control of the document also has a `markdown-*` style class (`markdown-h1`, `markdown-code-block`, `markdown-table`…), listed in the [style class reference](../reference/style-classes.md). A style wins over a control theme for the same property:

```xml
<!-- Headings are MarkdownSelectableTextBlocks: select them with :is(TextBlock) -->
<Style Selector=":is(TextBlock).markdown-h1">
  <Setter Property="FontSize" Value="36" />
  <Setter Property="FontWeight" Value="Bold" />
</Style>
```

Prefer keys and control themes for changes to the whole document. A theme only costs memory on the controls it is set on, while every style costs a little on every control of its selector's type, whether or not its classes match, and a long document has tens of thousands of controls.

The document itself is a `Grid.markdown-document`, so one style changes it whole. This one adds a margin and centres the document in a column at most 720px wide, as the Docs preset does:

```xml
<Style Selector="Grid.markdown-document">
  <Setter Property="Margin" Value="24,16" />
  <Setter Property="MaxWidth" Value="720" />
</Style>
```

Use this class rather than a `/template/` style on the viewer's `PART_ContentPresenter`: Avalonia does not undo what a `/template/` style changed when it is removed at run time.

With `UseGenericAttributes()` in the pipeline (called last), `{.name}` after an element adds `name` to its classes, so a document can ask for your styles:

```markdown
Read the docs first. {.note}
```

```xml
<Style Selector=":is(TextBlock).markdown-paragraph.note">
  <Setter Property="FontStyle" Value="Italic" />
</Style>
```

## Light, dark and high contrast

When the application switches between `Light` and `Dark`, every `DynamicResource` of the themes updates, code colours are swapped in place, and Mermaid diagrams and formulas are drawn again in the new colours. The document is not rendered again, and the scroll position is kept. A viewer that is hidden (on an inactive tab, say) catches up when it is shown.

In high contrast (`MarkdownViewer.HighContrastMode`, following the OS by default), the viewer swaps in the `MarkdownHighContrast*` palette, at 7:1 contrast or more. See [Accessibility](accessibility.md#high-contrast).

## Other guides

The guides of each feature show how to restyle it: [code blocks](code-blocks.md#styling-code-blocks), [images and the lightbox](images.md#styling-images), [folding](folding.md#styling), the [context menu](text-selection.md#context-menu), [find highlights](find-in-document.md#highlight-colours) and [task lists](task-lists.md).
