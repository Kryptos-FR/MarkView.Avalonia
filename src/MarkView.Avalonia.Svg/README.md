# MarkView.Avalonia.Svg

[![NuGet Version](https://img.shields.io/nuget/v/MarkView.Avalonia.Svg)](https://www.nuget.org/packages/MarkView.Avalonia.Svg)
[![NuGet Downloads](https://img.shields.io/nuget/dt/MarkView.Avalonia.Svg)](https://www.nuget.org/packages/MarkView.Avalonia.Svg)
[![Avalonia](https://img.shields.io/badge/Avalonia-12-blue)](https://avaloniaui.net)
[![CI](https://github.com/Kryptos-FR/MarkView.Avalonia/actions/workflows/ci.yml/badge.svg)](https://github.com/Kryptos-FR/MarkView.Avalonia/actions/workflows/ci.yml)
[![License](https://img.shields.io/github/license/Kryptos-FR/MarkView.Avalonia)](../../LICENSE.md)

SVG image rendering extension for [MarkView.Avalonia](https://www.nuget.org/packages/MarkView.Avalonia). Renders SVG files referenced in markdown images using [Svg.Skia](https://github.com/wieslawsoltes/Svg.Skia) — the same backend that Avalonia itself uses for vector icons.

## Installation

```bash
dotnet add package MarkView.Avalonia.Svg
```

## Quick Start

Call `UseSvg()` before setting `Markdown`:

```csharp
var viewer = new MarkdownViewer();
viewer.UseSvg();
viewer.Markdown = markdownText;
```

Markdown syntax is unchanged — standard image syntax with an `.svg` URL:

```markdown
![Logo](https://example.com/logo.svg)
![Badge](https://shields.io/badge/build-passing-green)
![Inline](data:image/svg+xml;base64,PHN2Zy8+)
```

## Supported URL Formats

| URL form | Handled by |
|----------|-----------|
| `https://example.com/icon.svg` | HTTP download, SVG parse |
| `relative/path/icon.svg` (with `BaseUri` set) | Resolved first; handled if the resolved URL is supported by `SvgImageLoader` |
| Any `https://` / `http://` URL | Attempted speculatively; returns `null` if response is not valid SVG, letting the bitmap fallback load it |
| `data:image/svg+xml;base64,…` | Base64 decode, SVG parse |
| `data:image/svg+xml,…` | URL-decode, SVG parse |
| `data:image/png;base64,…` | Not handled — passed to the next loader |

The speculative HTTP/HTTPS rule means badge services like `shields.io` that return SVG without a `.svg` extension are handled transparently.

When using relative image paths, set `MarkdownViewer.BaseUri` to an HTTP/HTTPS base URL if you want this extension to fetch and parse those images as SVG.

## How It Works

`UseSvg()` registers a `SvgExtension` which inserts `SvgImageLoader` at index 0 of `renderer.ImageLoaders`. Being first in the chain, it gets first pick on every image URL. If `CanLoad` returns `false` (e.g. a `data:image/png` URI), or if `LoadAsync` returns `null` (e.g. the HTTP response is not valid SVG), the next loader in the chain is tried.

Loading is asynchronous. The `Image` control is placed in the visual tree immediately; the SVG source is set once the download and parse complete. `SvgImage` is an `AvaloniaObject` and is created on the UI thread via `Dispatcher.UIThread`.

## Writing a Custom Image Loader

Implement `IImageLoader` from the core package to handle any image source:

```csharp
using MarkView.Avalonia.Extensions;

public class MyLoader : IImageLoader
{
    public bool CanLoad(string url) => url.StartsWith("myscheme://");

    public async Task<IImage?> LoadAsync(string url, CancellationToken ct = default)
    {
        // Return null to fall through to the next loader
        var bitmap = await FetchBitmapAsync(url, ct);
        return bitmap;
    }
}

// Insert at 0 to take priority, or Add() to run after built-in loaders
renderer.ImageLoaders.Insert(0, new MyLoader());
```

Or register via a `IMarkViewExtension`:

```csharp
public class MyExtension : IMarkViewExtension
{
    public void Register(AvaloniaRenderer renderer)
        => renderer.ImageLoaders.Insert(0, new MyLoader());
}

viewer.Extensions.Add(new MyExtension());
```

## License

[MIT](../../LICENSE.md) © Nicolas Musset
