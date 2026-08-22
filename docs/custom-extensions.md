# Writing Custom Extensions

MarkView.Avalonia is designed to be extended. The three main extension points are:

1. **Block / inline renderers** — replace or supplement how a Markdig AST node is rendered
2. **Image loaders** — add support for new image URL schemes
3. **Code highlighters** — replace the syntax-highlighting backend

All extension points are accessed through `IMarkViewExtension`.

## IMarkViewExtension

```csharp
using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;

public interface IMarkViewExtension
{
    void Register(AvaloniaRenderer renderer);
}
```

`Register` is called once per render pass, **before** `pipeline.Setup(renderer)`, so extensions can install renderers that override the defaults.

Register your extension on a viewer instance or globally:

```csharp
viewer.Extensions.Add(new MyExtension());
// or globally:
MarkdownViewerDefaults.Extensions.Add(new MyExtension());
```

## Replacing a block renderer

Use `renderer.ObjectRenderers.ReplaceOrAdd<TExisting, TNew>()` to swap out a built-in renderer:

```csharp
public class MyCodeBlockExtension : IMarkViewExtension
{
    public void Register(AvaloniaRenderer renderer)
    {
        renderer.ObjectRenderers.ReplaceOrAdd<CodeBlockRenderer, MyCodeBlockRenderer>();
    }
}
```

To insert at a specific position (e.g. to intercept before the default):

```csharp
renderer.ObjectRenderers.Insert(0, new MyFencedCodeInterceptor());
```

### Writing a block renderer

Extend `AvaloniaObjectRenderer<TBlock>`:

```csharp
using Markdig.Syntax;
using MarkView.Avalonia.Rendering;

public class MyCodeBlockRenderer : AvaloniaObjectRenderer<FencedCodeBlock>
{
    protected override void Write(AvaloniaRenderer renderer, FencedCodeBlock obj)
    {
        var language = obj.Info ?? string.Empty;
        var source   = obj.Lines.ToString();

        var tb = new TextBlock { Text = source };
        tb.Classes.Add("markdown-code-block");
        // ... add to renderer
        renderer.WriteBlock(new Border { Child = tb });
    }
}
```

`renderer.WriteBlock(control)` adds the control to the current `Panel` container.  
`renderer.WriteInline(inline)` adds an inline to the current `InlineCollection`.  
`renderer.Push(container)` / `renderer.Pop()` manage the render stack.

## Writing a custom image loader

Implement `IImageLoader`:

```csharp
using MarkView.Avalonia.Extensions;

public class MyImageLoader : IImageLoader
{
    public bool CanLoad(string url) => url.StartsWith("myapp://images/");

    public async Task<IImage?> LoadAsync(string url, CancellationToken ct = default)
    {
        // Return null to fall through to the next loader in the chain
        var stream = await MyApp.GetImageStreamAsync(url, ct);
        if (stream is null) return null;
        return new Bitmap(stream);
    }
}
```

Register it in a `IMarkViewExtension`:

```csharp
public class MyImageExtension : IMarkViewExtension
{
    public void Register(AvaloniaRenderer renderer)
    {
        // Insert at 0 to take priority over all built-in loaders
        renderer.ImageLoaders.Insert(0, new MyImageLoader());
    }
}
```

The loader chain is tried in order. The first loader whose `CanLoad` returns `true` **and** whose `LoadAsync` returns a non-`null` result wins. Returning `null` from `LoadAsync` passes control to the next loader.

## Writing a custom code highlighter

Implement `ICodeHighlighter` (or `IThemeAwareCodeHighlighter` for live theme switching):

```csharp
using MarkView.Avalonia.Extensions;
using Avalonia.Media;

public class MyHighlighter : ICodeHighlighter
{
    public IReadOnlyList<(string Text, IBrush? Foreground)>? Highlight(
        ReadOnlyMemory<char> line, string? language)
    {
        // Return null to signal the language is unsupported — falls back to monochrome.
        // Return an empty list to signal supported but no tokens.
        if (line.IsEmpty) return [];
        return [(line.ToString(), Brushes.LimeGreen)];
    }
}
```

`IThemeAwareCodeHighlighter` adds `HighlightVariant(line, language, isDark)` — called in-place when the user switches themes, allowing code blocks to update colours without a full document re-render.

Register on the renderer:

```csharp
public class MyHighlightExtension : IMarkViewExtension
{
    public void Register(AvaloniaRenderer renderer)
    {
        renderer.CodeHighlighter = new MyHighlighter();
    }
}
```

## Full example — custom admonition renderer

```csharp
/// <summary>Renders ::: note / ::: warning fences from a custom Markdig extension.</summary>
public class AdmonitionExtension : IMarkViewExtension
{
    public void Register(AvaloniaRenderer renderer)
    {
        renderer.ObjectRenderers.Add(new AdmonitionBlockRenderer());
    }
}

public class AdmonitionBlockRenderer : AvaloniaObjectRenderer<AdmonitionBlock>
{
    protected override void Write(AvaloniaRenderer renderer, AdmonitionBlock obj)
    {
        var panel = new StackPanel();
        var border = new Border { Child = panel };
        border.Classes.Add("my-admonition");
        border.Classes.Add($"my-admonition-{obj.Kind.ToLowerInvariant()}");

        var header = new TextBlock { Text = obj.Kind.ToUpperInvariant() };
        header.Classes.Add("my-admonition-header");
        panel.Children.Add(header);

        renderer.Push(panel);
        renderer.WriteChildren(obj);
        renderer.Pop();

        renderer.WriteBlock(border);
    }
}
```

## Full example — a complete inline extension (Markdig parser + node + renderer)

The example above renders an `AdmonitionBlock` but doesn't show how that block gets parsed
in the first place — writing a `MarkView.Avalonia` renderer is only half of a real
extension. The other half is a Markdig `IMarkdownExtension` that parses your syntax into a
syntax tree node.

This example implements `%[color:red]text%` inline colour spans end to end. It ships as a
runnable, documented demo page in `samples/MarkView.Avalonia.Demo/ColorExtension/` — open
that folder (and `Assets/color-extension.md`, which the page renders) for the full
tutorial; this section covers the shape of the five pieces involved.

### 1. The syntax tree node

A leaf node holding whatever your parser extracts — here, a colour name and literal text
content:

```csharp
public sealed class ColorSpanInline : LeafInline
{
    public string Color { get; }
    public string Content { get; }

    public ColorSpanInline(string color, string content)
    {
        Color = color;
        Content = content;
    }
}
```

### 2. The Markdig parser

Extend `Markdig.Parsers.InlineParser`, set `OpeningCharacters` to the character(s) that
trigger it, and implement `Match`. Returning `false` leaves the input `slice` untouched and
lets Markdig fall through to the next parser (plain text) — every built-in inline parser
follows this same graceful-degradation contract, so a malformed `%[color:...]` just renders
as literal text instead of breaking the document:

```csharp
public sealed class ColorSpanParser : InlineParser
{
    public ColorSpanParser() => OpeningCharacters = ['%'];

    public override bool Match(InlineProcessor processor, ref StringSlice slice)
    {
        // ... match "[color:NAME]", scan ahead for the closing '%' ...
        // On success:
        processor.Inline = new ColorSpanInline(color, content)
        {
            Span = new SourceSpan(
                processor.GetSourcePosition(startPosition, out var line, out var column),
                processor.GetSourcePosition(slice.Start - 1)),
            Line = line,
            Column = column,
        };
        return true;
    }
}
```

Setting `Span`/`Line`/`Column` via `InlineProcessor.GetSourcePosition` matters beyond
bookkeeping — `MarkView.Avalonia`'s cross-block text selection (see
[docs/text-selection.md](text-selection.md)) relies on accurate source spans to map screen
selections back to document positions.

### 3. Registering the parser — a Markdig `IMarkdownExtension`

This half is pure Markdig and knows nothing about Avalonia:

```csharp
public sealed class ColorMarkdownExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline) =>
        pipeline.InlineParsers.AddIfNotAlready<ColorSpanParser>();

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer) { }
}

public static class ColorMarkdownExtensions
{
    public static MarkdownPipelineBuilder UseColorSpans(this MarkdownPipelineBuilder builder) =>
        builder.Use<ColorMarkdownExtension>();
}
```

### 4. The renderer + its `IMarkViewExtension`

The `MarkView.Avalonia` half turns the parsed node into a control, following the same
`AvaloniaObjectRenderer<T>` / `IMarkViewExtension` shape as every example earlier in this
document:

```csharp
public sealed class ColorSpanRenderer : AvaloniaObjectRenderer<ColorSpanInline>
{
    protected override void Write(AvaloniaRenderer renderer, ColorSpanInline obj)
    {
        var run = new Run(obj.Content);
        if (Color.TryParse(obj.Color, out var color))
            run.Foreground = new SolidColorBrush(color);
        renderer.WriteInline(run);
    }
}

public sealed class ColorSpanExtension : IMarkViewExtension
{
    public void Register(AvaloniaRenderer renderer) =>
        renderer.ObjectRenderers.Add(new ColorSpanRenderer());
}
```

An unparseable colour name is treated the same way as a parse failure anywhere else in this
document's examples: fall back to a sane default (here, the inherited foreground) rather
than throwing.

### 5. Wiring both halves together

```csharp
viewer.Pipeline = new MarkdownPipelineBuilder()
    .UseSupportedExtensions()
    .UseColorSpans()
    .Build();
viewer.Extensions.Add(new ColorSpanExtension());
```

Note the asymmetry if you set `Pipeline` on a `MarkdownViewer` instance instead of (or in
addition to) `MarkdownViewerDefaults.Pipeline`: an instance `Pipeline` fully **replaces**
the global default — none of `MarkdownViewerDefaults.Pipeline`'s extensions apply unless
you add them again yourself. `Extensions`, by contrast, is **additive** — an instance's
`Extensions` list is combined with `MarkdownViewerDefaults.Extensions`, not a replacement
for it. See [docs/configuration.md](configuration.md) for the full precedence rules.
