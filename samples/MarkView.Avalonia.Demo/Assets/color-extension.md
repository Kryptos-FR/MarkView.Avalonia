# Writing a Custom Extension: Colour Spans

Markdown has no syntax for colouring text. This page implements one — `%[color:red]text%` —
as a worked example of the two extension points a real Markdig + MarkView extension needs:
a **Markdig** parser (turns text into a syntax tree node) and a **MarkView** renderer
(turns that node into an Avalonia control).

---

## Live result

%[color:red]This text is red%, %[color:#22c55e]this one is a hex green%,
and %[color:not-a-real-color]this one falls back to the default foreground%
because the colour name doesn't parse.

---

## 1. The syntax tree node — `ColorSpanInline`

A `LeafInline` (like Markdig's own `CodeInline`) holding the parsed colour name and literal
text content. It deliberately does not support nested markdown inside the span, the same way
code spans don't:

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

## 2. The parser — `ColorSpanParser`

An `InlineParser` triggered on `%`. It matches `[color:NAME]`, then scans forward for the
closing `%`, bailing out (returning `false`, leaving the text untouched) if the span crosses
a line break or never closes — exactly how Markdig's built-in parsers degrade gracefully.

```csharp
public sealed class ColorSpanParser : InlineParser
{
    public ColorSpanParser() => OpeningCharacters = ['%'];

    public override bool Match(InlineProcessor processor, ref StringSlice slice)
    {
        // ... match "[color:NAME]", scan to the closing '%' ...
        processor.Inline = new ColorSpanInline(color, content) { /* Span, Line, Column */ };
        return true;
    }
}
```

## 3. Registering the parser — `ColorMarkdownExtension`

Markdig extensions implement `IMarkdownExtension` and register into the pipeline being built.
This is the Markdig-only half — it knows nothing about Avalonia:

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

## 4. Rendering the node — `ColorSpanRenderer`

The MarkView-side renderer turns a `ColorSpanInline` into a `Run` with its `Foreground` set:

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
```

## 5. Registering the renderer — `ColorSpanExtension`

```csharp
public sealed class ColorSpanExtension : IMarkViewExtension
{
    public void Register(AvaloniaRenderer renderer) =>
        renderer.ObjectRenderers.Add(new ColorSpanRenderer());
}
```

## Wiring it up on this page

This view's `MarkdownViewer` sets `Pipeline` and `Extensions` directly, bypassing
`MarkdownViewerDefaults` for parsing entirely:

```csharp
Viewer.Pipeline = new MarkdownPipelineBuilder()
    .UseSupportedExtensions()
    .UseColorSpans()
    .Build();
Viewer.Extensions.Add(new ColorSpanExtension());
```

`Pipeline` fully replaces the global default when set on an instance — this page's other
views (footnotes, alert blocks, …) are intentionally unavailable here. `Extensions`
(the rendering side) is additive instead: this page still benefits from the app's global
syntax-highlighting extension for the code fences above, on top of `ColorSpanExtension`.

See [docs/custom-extensions.md](../../../docs/custom-extensions.md) for the full write-up.
