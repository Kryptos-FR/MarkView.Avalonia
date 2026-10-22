---
title: "Displaying documents"
---

# Displaying documents

Give the viewer its text with `Markdown`, or a location to load it from with `Source`:

```xml
<mv:MarkdownViewer Markdown="{Binding Notes}" />
<mv:MarkdownViewer Source="avares://MyApp/Docs/guide.md" />
```

Setting either renders the whole document again and scrolls back to the top. To show text that grows at its end, such as the answer of a language model, use a [streaming session](streaming.md) instead.

## Loading a file or a URL

`Source` loads the document from an embedded resource, a file or the web:

```csharp
viewer.Source = new Uri("avares://MyApp/Docs/guide.md");
viewer.Source = new Uri("/home/user/documents/notes.md");
viewer.Source = new Uri("https://example.com/readme.md");
viewer.Source = null;   // back to the Markdown property
```

| Scheme | Loading | Notes |
|--------|---------|-------|
| `avares://` | Synchronous | `ManifestResourceStream`, already in memory |
| `file://` | Async | Local and network paths |
| `http`, `https` | Async | The request is cancelled when `Source` changes; a response over 64 MB is not loaded (remote images too) |

- When both are set, `Source` wins over `Markdown`.
- The base address for relative links and images is the folder of `Source`, unless `BaseUri` is set.
- A `#fragment` scrolls to that anchor once the document is rendered: `new Uri(path + "#installation")` behaves like setting `Source` and then calling `ScrollToAnchor("installation")`, without waiting for the render.
- For `avares://`, everything after the first `#` is the fragment, so an asset whose path contains `#` (such as `Docs/C#.md`) cannot be loaded through `Source`. Rename it, or read it with `AssetLoader.Open` and set `Markdown`.

## Relative links and images

`BaseUri` resolves relative links and image addresses when the text does not come from `Source`, or overrides the folder of `Source`:

```csharp
viewer.BaseUri = new Uri("https://raw.githubusercontent.com/org/repo/main/docs/");
viewer.Markdown = File.ReadAllText("README.md");
```

## Links

Clicking a link opens it with the platform's launcher: the default browser on the desktop, an intent on mobile, a new tab in the browser. Links to an anchor of the document (`#heading`) scroll to it. A link the platform cannot open (no application for its scheme) does nothing; if the launcher throws, the viewer logs a warning in Avalonia's `Control` log area instead of letting the exception end the application.

`LinkClicked` is raised first, so an application can handle some links itself. Set `e.Handled` to stop the default launch:

```csharp
viewer.LinkClicked += (_, e) =>
{
    if (e.Url.EndsWith(".md"))
    {
        viewer.Source = new Uri(e.Url);   // open the linked document in place
        e.Handled = true;
    }
};
```

`LinkClicked` is a routed event that bubbles, so a class handler registered once handles every viewer:

```csharp
MarkdownViewer.LinkClickedEvent.AddClassHandler<MarkdownViewer>((viewer, e) =>
{
    // e.Url is the address as written, possibly relative ("#section")
});
```

The event is raised for every link, anchor links included; the viewer has already scrolled to an anchor before raising it, and never launches one.

## Anchors

`ScrollToAnchor` scrolls to a heading or a footnote:

```csharp
viewer.ScrollToAnchor("installation");   // "## Installation"
viewer.ScrollToAnchor("fn-1");           // footnote [^1]
```

Anchors follow GitHub's rules: the heading's text in lower case, spaces turned into hyphens, other punctuation removed. A heading whose anchor is already taken gets a suffix (`-1`, `-2`…). A heading inside a collapsed alert, details block or [heading section](folding.md) is expanded before the viewer scrolls to it.

### Smooth scrolling

With `IsSmoothScrollingEnabled`, a jump to an anchor (a `#anchor` link, a footnote reference, `ScrollToAnchor`) is animated instead of immediate:

```xml
<Style Selector="mv|MarkdownViewer">
  <Setter Property="IsSmoothScrollingEnabled" Value="True" />
</Style>
```

The animation takes longer for a longer jump, as in a browser: from 150 ms for a short one to 500 ms at most. A jump of more than about 3,900 px goes at once to that distance from the anchor and animates the rest. The animation follows its target if content above it changes height, and stops as soon as anything else scrolls the document (the wheel, the scroll bar, the keyboard). Opening a document at a fragment still shows the anchor at once. Avalonia has no reduced-motion setting, so an application that has one can bind this property to it.

## Table of contents

`TableOfContents` lists the document's headings as a tree, to bind to your own side panel, menu or breadcrumb; the library has no table of contents control of its own:

```csharp
foreach (var entry in viewer.TableOfContents)
{
    Console.WriteLine($"{new string(' ', (entry.Level - 1) * 2)}{entry.Text}");
    // entry.Slug is the heading's anchor, for ScrollToAnchor
}
```

Each `TocEntry` has `Level`, `Text`, `Slug` and `Children`. `TableOfContentsMaxDepth` (6 by default, all levels) leaves out deeper headings:

```csharp
viewer.TableOfContentsMaxDepth = 2;   // h1 and h2 only
```

The list is rebuilt after every render. The [demo](https://github.com/Kryptos-FR/MarkView.Avalonia/tree/main/samples/MarkView.Avalonia.Demo) binds it to a side panel, a drop-down and a top bar.

## Lazy rendering

Some content costs far more to build than text: decoding an image, typesetting a formula, laying out a Mermaid diagram, colouring a code block. The viewer builds it when it first comes within one screen of the visible area, so a long document shows its first screen quickly and the parts nobody scrolls to cost nothing. On the desktop, Mermaid diagrams are rendered in the background as soon as the viewer is shown instead.

| Content | Before it comes near | Once it comes near |
|---|---|---|
| Image, YouTube thumbnail | Not loaded; an image already in the [image cache](images.md#image-cache) shows at once | Starts loading |
| Formula | Empty | Typeset |
| Mermaid diagram, in the browser | Empty | Rendered |
| Code block with syntax highlighting | Its text, uncoloured | Coloured |

The "before" column never shows on screen: content is built a screen ahead, and content already in view is built during the layout pass that first lays it out, before it is drawn. Images are the exception, since they still have to download. Find, selection and copy work on the whole document, built or not. A theme switch only rebuilds the content already built.

Content built while scrolling is often taller than the space it held, and so is a block a streaming session updates. When that happens above the part of the document in view, the viewer moves the scroll position by as much, as browsers do, so the text being read stays where it is. A heading reached with `ScrollToAnchor` or a match found with `FindNext` stays where it was shown while the content above it loads.

Turn it off for a viewer that must show everything without being scrolled, for example one rendered to a bitmap for printing:

```xml
<mv:MarkdownViewer IsLazyRenderingEnabled="False" />
```

With it off, formulas and colours are built during the render, and images and diagrams start loading as soon as the viewer is shown.

## Settings for every viewer

`MarkdownViewerDefaults` sets a pipeline and extensions for every viewer of the application:

```csharp
// App.axaml.cs, in OnFrameworkInitializationCompleted
MarkdownViewerDefaults.Pipeline = new MarkdownPipelineBuilder()
    .UseGitHubExtensions()
    .UseExtendedAlertBlocks()
    .Build();

MarkdownViewerDefaults.Extensions.AddTextMateHighlighting();
MarkdownViewerDefaults.Extensions.AddFenceRenderer("chart", (code, info) => new MyChart(code));
```

1. A viewer uses its own `Pipeline` if set, otherwise `MarkdownViewerDefaults.Pipeline`, otherwise the built-in default (`UseGitHubExtensions()`).
2. The default extensions are registered first, then the viewer's `Extensions`. An extension object in both lists is registered once.
3. For [fence renderers](code-blocks.md#custom-fenced-blocks), the last one registered for a language wins, so a viewer's own replaces a default one.

The other properties are styled properties, so a style sets them for every viewer:

```xml
<Style Selector="mv|MarkdownViewer">
  <Setter Property="ShowCodeBlockLanguage" Value="True" />
  <Setter Property="IsImageZoomEnabled" Value="True" />
</Style>
```

## Scrolling and the template

The theme's `ControlTemplate` puts the document in a `ScrollViewer` named `PART_ScrollViewer`:

```
Border → ScrollViewer (PART_ScrollViewer) → ContentPresenter
```

The usual `ScrollViewer` attached properties reach it, as with a `ListBox`:

```xml
<mv:MarkdownViewer ScrollViewer.VerticalScrollBarVisibility="Hidden" />
```

The horizontal scroll bar is disabled by default, which gives the document the viewer's width: images scale down and code blocks [wrap](code-blocks.md#line-numbers-wrapping-and-height) to it. With `ScrollViewer.HorizontalScrollBarVisibility="Auto"` (or `Visible`), wide content scrolls instead, code does not wrap, and images may no longer scale down to the viewer's width.

A template of your own can leave `PART_ScrollViewer` out. The document still renders, but `ScrollToAnchor()` falls back to `BringIntoView()`, and images no longer scale to the viewer's width unless the template constrains it:

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
