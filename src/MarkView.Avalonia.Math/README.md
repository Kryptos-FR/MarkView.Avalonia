# MarkView.Avalonia.Math

[![NuGet Version](https://img.shields.io/nuget/v/MarkView.Avalonia.Math)](https://www.nuget.org/packages/MarkView.Avalonia.Math)
[![NuGet Downloads](https://img.shields.io/nuget/dt/MarkView.Avalonia.Math)](https://www.nuget.org/packages/MarkView.Avalonia.Math)
[![Avalonia](https://img.shields.io/badge/Avalonia-12-blue)](https://avaloniaui.net)
[![CI](https://img.shields.io/github/actions/workflow/status/Kryptos-FR/MarkView.Avalonia/ci.yml?branch=main&label=CI)](https://github.com/Kryptos-FR/MarkView.Avalonia/actions/workflows/ci.yml)
[![License](https://img.shields.io/github/license/Kryptos-FR/MarkView.Avalonia)](https://github.com/Kryptos-FR/MarkView.Avalonia/blob/main/LICENSE.md)

LaTeX math for [MarkView.Avalonia](https://www.nuget.org/packages/MarkView.Avalonia), typeset with [CSharpMath](https://github.com/verybadcat/CSharpMath) in .NET: no browser, WebView or JavaScript. Math is opt-in, since `$` is common in ordinary prose.

## Installation

```bash
dotnet add package MarkView.Avalonia.Math
```

## Usage

```csharp
viewer.UseMath();                              // $…$, $$…$$, \(…\) and \[…\]
viewer.UseMath(bracketDelimiters: false);      // dollars only
```

`UseMath()` also sets the viewer's pipeline, so that the delimiters are parsed. To enable math on every viewer of the application, add the extension and the pipeline at startup:

```csharp
MarkdownViewerDefaults.Extensions.AddMath();
MarkdownViewerDefaults.Pipeline = new MarkdownPipelineBuilder()
    .UseGitHubExtensions()
    .UseMathematics()
    .UseMathBracketDelimiters()
    .Build();
```

```markdown
Einstein's famous equation is $E = mc^2$.

$$
x = \frac{-b \pm \sqrt{b^2 - 4ac}}{2a}
$$
```

Formulas follow the theme between light and dark, display formulas get a copy button for their TeX source, and invalid LaTeX shows an error instead of throwing.

## Documentation

- [Math](https://kryptos-fr.github.io/MarkView.Avalonia/guides/math/): delimiters, the bracket rules, copy, streaming and errors.

The [documentation site](https://kryptos-fr.github.io/MarkView.Avalonia/) covers the whole library, and the [demo](https://kryptos-fr.github.io/MarkView.Avalonia/demo/) shows every feature in the browser.

## License

[MIT](https://github.com/Kryptos-FR/MarkView.Avalonia/blob/main/LICENSE.md) © Nicolas Musset
