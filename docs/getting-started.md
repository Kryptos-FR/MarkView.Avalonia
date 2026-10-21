---
title: "Getting started"
---

# Getting started

MarkView.Avalonia renders Markdown with native Avalonia controls. Install the package, include its theme, and put a `MarkdownViewer` in a window.

## Install

```bash
dotnet add package MarkView.Avalonia
```

The package targets .NET 10 and Avalonia 12. Code highlighting, SVG images, Mermaid diagrams and math come in their own packages; see [Choosing packages](packages.md).

## Include the theme

Add the theme to `App.axaml`, after Avalonia's own theme:

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="MyApp.App">
  <Application.Styles>
    <FluentTheme />
    <StyleInclude Source="avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml" />
  </Application.Styles>
</Application>
```

The theme is not only colours: it holds the viewer's template, which makes the document scroll. Without it the document renders but cannot scroll. To make the document look like GitHub, a chat message or a documentation site, add a [theme preset](guides/themes.md#presets) after it.

## Show a document

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:mv="using:MarkView.Avalonia"
        x:Class="MyApp.MainWindow">

  <mv:MarkdownViewer Markdown="# Hello, MarkView!" />
</Window>
```

Bind `Markdown` to a string of your view model, or point `Source` at a file:

```xml
<mv:MarkdownViewer Markdown="{Binding DocumentText}" />

<!-- An embedded resource, a file or a URL -->
<mv:MarkdownViewer Source="avares://MyApp/Docs/readme.md" />
```

The viewer renders the document as GitHub does: tables, task lists, alerts, footnotes and the HTML that READMEs use. Links open in the system's browser, and relative links and images resolve against the location of `Source`. Users can select and copy text, and right-click for a context menu.

## Configure every viewer at once

Set the pipeline and extensions once at startup, and every viewer of the application uses them:

```csharp
// App.axaml.cs
public override void OnFrameworkInitializationCompleted()
{
    MarkdownViewerDefaults.Extensions.AddTextMateHighlighting(); // MarkView.Avalonia.SyntaxHighlighting
    MarkdownViewerDefaults.Extensions.AddSvg();                  // MarkView.Avalonia.Svg
    MarkdownViewerDefaults.Extensions.AddMermaid();              // MarkView.Avalonia.Mermaid

    // ...
    base.OnFrameworkInitializationCompleted();
}
```

## Next steps

- [Displaying documents](guides/displaying-documents.md): `Source`, links, anchors and the table of contents.
- [Chat and AI answers](guides/chat.md): streaming an answer as it arrives.
- [Themes and presets](guides/themes.md): changing the look of the document.
- [MarkdownViewer reference](reference/markdown-viewer.md): every property, event and method.
- The [demo](https://kryptos-fr.github.io/MarkView.Avalonia/demo/) shows every feature in the browser; its source is in [`samples/`](https://github.com/Kryptos-FR/MarkView.Avalonia/tree/main/samples).
