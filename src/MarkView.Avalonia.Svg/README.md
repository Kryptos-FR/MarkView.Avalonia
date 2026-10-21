# MarkView.Avalonia.Svg

[![NuGet Version](https://img.shields.io/nuget/v/MarkView.Avalonia.Svg)](https://www.nuget.org/packages/MarkView.Avalonia.Svg)
[![NuGet Downloads](https://img.shields.io/nuget/dt/MarkView.Avalonia.Svg)](https://www.nuget.org/packages/MarkView.Avalonia.Svg)
[![Avalonia](https://img.shields.io/badge/Avalonia-12-blue)](https://avaloniaui.net)
[![CI](https://img.shields.io/github/actions/workflow/status/Kryptos-FR/MarkView.Avalonia/ci.yml?branch=main&label=CI)](https://github.com/Kryptos-FR/MarkView.Avalonia/actions/workflows/ci.yml)
[![License](https://img.shields.io/github/license/Kryptos-FR/MarkView.Avalonia)](https://github.com/Kryptos-FR/MarkView.Avalonia/blob/main/LICENSE.md)

SVG images for [MarkView.Avalonia](https://www.nuget.org/packages/MarkView.Avalonia), rendered with [Svg.Skia](https://github.com/wieslawsoltes/Svg.Skia), the vector library Avalonia uses itself.

## Installation

```bash
dotnet add package MarkView.Avalonia.Svg
```

## Usage

```csharp
viewer.UseSvg();

// every viewer of the application, at startup
MarkdownViewerDefaults.Extensions.AddSvg();
```

Images keep the standard Markdown syntax:

```markdown
![Logo](https://example.com/logo.svg)
![Build](https://img.shields.io/badge/build-passing-green)
![Inline](data:image/svg+xml;base64,PHN2Zy8+)
```

Addresses ending in `.svg`, `data:image/svg+xml` URIs and badge services that serve SVG without an `.svg` address all work; anything that is not SVG is left to the bitmap loader. An SVG image can be saved as SVG or PNG from the lightbox or the context menu.

## Documentation

- [SVG images](https://kryptos-fr.github.io/MarkView.Avalonia/guides/images/#svg-images): which addresses are read as SVG.
- [Saving images and diagrams](https://kryptos-fr.github.io/MarkView.Avalonia/guides/images/#saving-images-and-diagrams).
- [Writing a custom image loader](https://kryptos-fr.github.io/MarkView.Avalonia/guides/custom-extensions/#writing-a-custom-image-loader).

The [documentation site](https://kryptos-fr.github.io/MarkView.Avalonia/) covers the whole library, and the [demo](https://kryptos-fr.github.io/MarkView.Avalonia/demo/) shows every feature in the browser.

## License

[MIT](https://github.com/Kryptos-FR/MarkView.Avalonia/blob/main/LICENSE.md) © Nicolas Musset
