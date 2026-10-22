---
title: "Upgrading"
---

# Upgrading

Each breaking release (see [Versioning](reference/versioning.md)) lists below the changes that can need work in an application. When skipping versions, check every release in between.

## Upgrading to 12.5

### Only `http`, `https` and `mailto` links open by themselves

A clicked link that no `LinkClicked` handler handled used to be given to the platform's launcher whatever its scheme. The viewer now launches only the schemes in `MarkdownViewerDefaults.LaunchableLinkSchemes` (`http`, `https` and `mailto` by default), because the operating system can run a program for a `file:` link or an installer protocol. To open other links, handle `LinkClicked`, or add their scheme to the set when the documents are trusted. See [Links the viewer opens](guides/displaying-documents.md#links-the-viewer-opens).

### `AvaloniaRenderer.ResolveUrl` can return `null`

`ResolveUrl(string)` became `string? ResolveUrl(string url, MarkdownUrlKind kind = MarkdownUrlKind.Link)`: it also applies the viewer's [`UrlTransform`](guides/displaying-documents.md#filtering-links-and-images), which returns `null` for a URL it drops. A custom renderer that calls it must be recompiled, should pass `MarkdownUrlKind.Image` for an image, and should render nothing that loads or opens the URL when it gets `null`, as the built-in renderers do (a dropped link renders as its text, a dropped image as its placeholder).

### A `Source` that cannot be loaded keeps the current document

Setting `Source` to an `avares://` asset that does not exist used to throw from the setter. Like a missing file or a failed download, it now leaves the current document on screen. Code that caught the exception to detect a missing asset should check for the asset itself, with `AssetLoader.Exists`.

## Upgrading to 12.4

12.4.0 is a breaking release (see [Versioning](reference/versioning.md)). Most applications only need to update the packages together and recompile; check the changes below that apply to yours.

### Update every package together

The extension packages of 12.4.0 (SyntaxHighlighting, Svg, Mermaid, Math) require `MarkView.Avalonia` 12.4.0, and apps built against 12.3.0 must be recompiled: `UseMath()`, `UseMermaid()` and `AddMermaid()` gained optional parameters, which is source compatible but not binary compatible.

### The default pipeline is now GitHub's

A viewer without a `Pipeline` now uses `UseGitHubExtensions()` instead of `UseSupportedExtensions()`, so documents render as on GitHub:

- `~text~` is strikethrough instead of subscript.
- Alerts (`> [!NOTE]`), footnotes, `<details>` blocks and the [HTML subset](guides/markdown.md#html-subset) render.
- Grid tables, `^superscript^`, `++underline++` and `==highlight==` are no longer parsed.

To keep the previous rendering, set the previous pipeline as the default:

```csharp
MarkdownViewerDefaults.Pipeline = new MarkdownPipelineBuilder().UseSupportedExtensions().Build();
```

See [Markdown and the pipeline](guides/markdown.md) for the differences and how to combine them.

### Removed pipeline methods

`UseAbbreviations()`, `UseCitations()`, `UseFigures()`, `UseFootnotes()`, `UseAlertBlocks()` and `UseMediaLinks()` were removed from `MarkView.Avalonia.MarkdownExtensions`, the extension methods of `MarkdownPipelineBuilder`. They were identical to Markdig's own, and four of them clashed with them (error CS0121) when both namespaces were imported. Code that calls them keeps compiling and now calls Markdig's methods. The `MarkdownViewer` methods of the same names (`viewer.UseFootnotes()`…) are unchanged.

### The viewer has a context menu

A right-click on the viewer opens its new context menu (copy, copy as Markdown, copy a link or a table, save an image, select all). Set your own `ContextFlyout` or `ContextMenu` to replace it, or remove it:

```xml
<mv:MarkdownViewer ContextFlyout="{x:Null}" />
```

See [Selection and copy](guides/text-selection.md#context-menu).

### Mermaid diagrams render when shown

A diagram renders once its viewer is attached to a window, in the background on the desktop and, in the browser, once it comes within a screen of the visible area. A diagram that is never shown is never rendered.

### Sealed classes

`MarkdownSelectableTextBlock` and `SlugGenerator` are sealed. Style headings and paragraphs through their [control themes](guides/themes.md#sizes-spacing-and-control-themes) instead of deriving from them.

### New defaults to know

These are not breaking, but change what a document shows:

- Code blocks have a copy button above the code (`ShowCodeBlockCopyButton`, on by default); the language label is off by default (`ShowCodeBlockLanguage`).
- Images, formulas, diagrams and code colours are built when they are about to scroll into view (`IsLazyRenderingEnabled`, on by default). Turn it off for a viewer that is rendered to a bitmap without being scrolled; see [Lazy rendering](guides/displaying-documents.md#lazy-rendering).
- Each block takes the direction of its text, so Hebrew and Arabic read right to left (`DetectTextDirection`, on by default).
- The high-contrast palette follows the OS setting (`HighContrastMode`, `System` by default).

The [release notes](https://github.com/Kryptos-FR/MarkView.Avalonia/releases) list every change of the release.
