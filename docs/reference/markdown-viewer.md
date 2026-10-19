---
title: "MarkdownViewer"
---

# MarkdownViewer

`MarkView.Avalonia.MarkdownViewer` is the control that renders a document. This page lists its members by kind; the [API reference](https://kryptos-fr.github.io/MarkView.Avalonia/api/) has their signatures.

## Properties

Styled properties can be set in a style for every viewer of an application. The ones marked "re-renders" render the document again when they change.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Markdown` | `string?` | `null` | The text to render. Re-renders. See [Displaying documents](../guides/displaying-documents.md). |
| `Source` | `Uri?` | `null` | A document to load and render (`avares://`, `file://`, `http`, `https`); wins over `Markdown`. Re-renders. See [Loading a file or a URL](../guides/displaying-documents.md#loading-a-file-or-a-url). |
| `BaseUri` | `Uri?` | `null` | Base address of relative links and images; the folder of `Source` when not set. Re-renders. |
| `Pipeline` | `MarkdownPipeline?` | `null` | The Markdig pipeline; `null` falls back to `MarkdownViewerDefaults.Pipeline`, then to `UseGitHubExtensions()`. Re-renders. See [Markdown and the pipeline](../guides/markdown.md). |
| `Extensions` | `IList<IMarkViewExtension>` | empty | The viewer's extensions, registered after `MarkdownViewerDefaults.Extensions`. Not a styled property. See [Choosing packages](../packages.md). |
| `ImageResizeMode` | `ImageResizeMode` | `ScaleDownToFit` | How images without a size hint scale to the viewer's width. See [Sizing](../guides/images.md#sizing). |
| `IsImageZoomEnabled` | `bool` | `false` | Clicking an image or a diagram opens it in a lightbox. See [Lightbox](../guides/images.md#lightbox). |
| `IsImageSaveEnabled` | `bool` | `true` | Images and diagrams can be saved from the lightbox and the context menu. See [Saving images and diagrams](../guides/images.md#saving-images-and-diagrams). |
| `IsLazyRenderingEnabled` | `bool` | `true` | Images, formulas, diagrams and code colours are built when about to scroll into view. Re-renders. See [Lazy rendering](../guides/displaying-documents.md#lazy-rendering). |
| `IsIncrementalRenderingEnabled` | `bool` | `false` | A new `Markdown` text re-renders only the blocks it changes, and keeps the scroll position. See [Live preview](../guides/live-preview.md). |
| `IsSmoothScrollingEnabled` | `bool` | `false` | Animates jumps to anchors. See [Smooth scrolling](../guides/displaying-documents.md#smooth-scrolling). |
| `ShowCodeBlockLanguage` | `bool` | `false` | Shows each code block's language in its toolbar. Re-renders. See [Code blocks](../guides/code-blocks.md#toolbar-and-copy-button). |
| `ShowCodeBlockCopyButton` | `bool` | `true` | A copy button for each code block, and for each display formula. Re-renders. |
| `ShowCodeBlockCopyButtonOnHover` | `bool` | `false` | Shows the copy buttons only while the pointer is over their block. See [Showing copy buttons on hover](../guides/code-blocks.md#showing-copy-buttons-on-hover). |
| `ShowCodeBlockLineNumbers` | `bool` | `false` | Numbers the lines of code blocks. Re-renders. See [Line numbers, wrapping and height](../guides/code-blocks.md#line-numbers-wrapping-and-height). |
| `WrapCodeBlockLines` | `bool` | `false` | Wraps long code lines. Re-renders. |
| `CodeBlockMaxHeight` | `double` | `PositiveInfinity` | Maximum height of a code block, past which it scrolls. Re-renders. |
| `AreTaskListsInteractive` | `bool` | `false` | Task-list items are checkboxes that raise `TaskToggled`. Re-renders. See [Task lists](../guides/task-lists.md). |
| `AreHeadingsFoldable` | `bool` | `false` | Each heading has a button that folds its section. Re-renders. See [Folding](../guides/folding.md). |
| `TableOfContents` | `IReadOnlyList<TocEntry>` | empty | Read-only. The headings as a tree, rebuilt after every render. See [Table of contents](../guides/displaying-documents.md#table-of-contents). |
| `TableOfContentsMaxDepth` | `int` | `6` | Deepest heading level in `TableOfContents`. |
| `DetectTextDirection` | `bool` | `true` | Each block takes the direction of its text, so Hebrew and Arabic read right to left. Re-renders. See [Right-to-left text](../guides/right-to-left.md). |
| `HighContrastMode` | `HighContrastMode` | `System` | When to use the high-contrast palette: following the OS (`System`), `Enabled` or `Disabled`. See [High contrast](../guides/accessibility.md#high-contrast). |
| `IsHighContrast` | `bool` | `false` | Read-only. Whether the high-contrast palette is in use; mirrors the `:high-contrast` pseudo-class. |
| `IsHtmlCopyEnabled` | `bool` | `true` | Copying also writes the selection as HTML, on the desktop. See [HTML on the clipboard](../guides/text-selection.md#html-on-the-clipboard). |
| `HasSelection` | `bool` | `false` | Read-only. Whether text is selected. |
| `SelectedText` | `string` | | Read-only, not bindable. The selected text, lines separated by `\n`. |
| `FindMatchCount` | `int` | `0` | Read-only. The number of matches of the current search. See [Find in document](../guides/find-in-document.md). |
| `CurrentFindMatchIndex` | `int` | `-1` | Read-only. The index of the current match, or -1. |
| `IsStreaming` | `bool` | `false` | Read-only. Whether a streaming session is open. See [Streaming](../guides/streaming.md). |
| `AutoScrollToEnd` | `bool` | `false` | During a streaming session, keeps the end of the document in view. |

`MarkdownViewer.InHighContrast` is an inherited attached property, true on every control of a document in high contrast, for control themes (see [Themes and presets](../guides/themes.md#sizes-spacing-and-control-themes)).

## Events

`LinkClicked`, `TaskToggled`, `SelectionChanged`, `CopyingToClipboard` and `SavingImage` are routed events that bubble, with a matching `…Event` field for class handlers.

| Event | Raised when |
|-------|-------------|
| `LinkClicked` | A link is clicked, before the viewer opens it; set `Handled` to open it yourself. See [Links](../guides/displaying-documents.md#links). |
| `TaskToggled` | The reader ticks a task-list checkbox. See [Task lists](../guides/task-lists.md). |
| `SelectionChanged` | The selection changes. See [Selection changes](../guides/text-selection.md#selection-changes). |
| `CopyingToClipboard` | The viewer is about to write to the clipboard; the handler can change or cancel the copy. See [Changing what is copied](../guides/text-selection.md#changing-what-is-copied). |
| `SavingImage` | An image or a diagram is about to be saved. See [Saving images and diagrams](../guides/images.md#saving-images-and-diagrams). |

## Methods

| Method | Description |
|--------|-------------|
| `ScrollToAnchor(string)` | Scrolls to a heading or a footnote. See [Anchors](../guides/displaying-documents.md#anchors). |
| `SelectAll()`, `ClearSelection()` | Selects the whole document, or nothing. |
| `GetSelectedText()` | The selection as text. |
| `GetSelectedMarkdown()` | The selection as Markdown. See [Copying as Markdown](../guides/text-selection.md#copying-as-markdown). |
| `CopyToClipboardAsync()`, `CopyAsMarkdownAsync()` | Copies the selection, as text (and HTML) or as Markdown. |
| `Find(string, MarkdownFindOptions)` | Highlights every match and returns their number. See [Find in document](../guides/find-in-document.md). |
| `FindNext()`, `FindPrevious()`, `ClearFind()` | Moves to the next or previous match, or clears the search. |
| `BeginStreaming(TimeSpan?, bool)` | Starts a streaming session that appends to `Markdown`. See [Streaming](../guides/streaming.md). |

Extension methods of `MarkdownViewer`, in the `MarkView.Avalonia` namespace, set the pipeline or add an extension; each returns the viewer:

| Method | Does |
|--------|------|
| `UseAbbreviations()`, `UseAlertBlocks()`, `UseCitations()`, `UseFigures()`, `UseFootnotes()`, `UseHardlineBreaks()`, `UseMediaLinks()`, `UseThinkingBlocks(options)` | Sets `Pipeline` to `UseGitHubExtensions()` plus that syntax. See [Adding syntax](../guides/markdown.md#adding-syntax). |
| `UseFenceRenderer(language, factory)` | Renders the fenced blocks of a language as your own control. See [Custom fenced blocks](../guides/code-blocks.md#custom-fenced-blocks). |
| `UseTextMateHighlighting(dark, light)` | SyntaxHighlighting package. See [Syntax highlighting](../guides/code-blocks.md#syntax-highlighting). |
| `UseSvg()` | Svg package. See [SVG images](../guides/images.md#svg-images). |
| `UseMermaid(style)` | Mermaid package. See [Mermaid diagrams](../guides/images.md#mermaid-diagrams). |
| `UseMath(bracketDelimiters)` | Math package; also sets `Pipeline`. See [Math](../guides/math.md). |

## MarkdownViewerDefaults

Static settings for every viewer of the application. See [Settings for every viewer](../guides/displaying-documents.md#settings-for-every-viewer).

| Member | Description |
|--------|-------------|
| `Pipeline` | The pipeline of viewers without their own. |
| `Extensions` | Extensions registered on every viewer, before its own. The packages add `AddTextMateHighlighting()`, `AddSvg()`, `AddMermaid()`, `AddMath()`, and the core `AddFenceRenderer()`. |
| `ImageCacheSize` | Size of the shared image cache, in bytes (100 MB by default; 0 turns it off). See [Image cache](../guides/images.md#image-cache). |
| `ClearImageCache()` | Empties the image cache. |
