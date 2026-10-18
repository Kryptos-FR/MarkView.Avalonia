# Writing Custom Extensions

MarkView.Avalonia is designed to be extended. The three main extension points are:

1. **Block / inline renderers** — replace or supplement how a Markdig AST node is rendered
2. **Image loaders** — add support for new image URL schemes
3. **Code highlighters** — replace the syntax-highlighting backend

All extension points are accessed through `IMarkViewExtension`.

To show the fenced code blocks of one language as your own control (```` ```chart ```` → a chart),
you don't need any of them: register a [fence renderer](#rendering-a-fence-language-as-your-own-control).

## Rendering a fence language as your own control

A fence renderer maps a fenced code block's language to a factory that builds a control from the
block's content. No Markdig renderer is involved:

```csharp
using MarkView.Avalonia;

viewer.UseFenceRenderer("chart", (code, info) => new MyChart(code));
// or for every viewer of the application (App.axaml.cs):
MarkdownViewerDefaults.Extensions.AddFenceRenderer("chart", (code, info) => new MyChart(code));
```

````markdown
```chart {height=200}
jan 12
feb 19
```
````

The factory is a `FenceRendererFactory`, `Control? (string code, string info)`:

- `code` is the block's content without its fences, its lines joined with `\n`.
- `info` is the whole info string after the opening fence: the language, then anything after it
  (`chart {height=200}` above), for the factory to parse as it likes. With Markdig's
  `UseGenericAttributes()` in the pipeline, a trailing `{...}` is parsed as
  [attributes](theming.md#classes-from-generic-attributes) instead, and is not part of `info`.
- Return a **new** control on every call. Return `null` to render that block as an ordinary code
  block, e.g. when its content isn't valid for your control.

Behaviour:

- **Precedence.** The language is matched case-insensitively against the first word of the info
  string. A fence renderer takes precedence over every other renderer of fenced code blocks —
  Mermaid diagrams, TextMate highlighting, the plain code block — whatever order the extensions
  were registered in, so `UseFenceRenderer("mermaid", …)` replaces the Mermaid package's diagrams.
  Blocks of other languages render as they would without it, and `$$` math blocks stay math.
- **One renderer per language.** When several are registered for the same language, the last one
  registered wins: a viewer's own `Extensions` override `MarkdownViewerDefaults.Extensions`.
  `UseFenceRenderer` also removes the viewer's earlier fence renderer for that language.
- **Re-rendering.** `UseFenceRenderer` re-renders the viewer's document. Adding a
  `FenceRendererExtension` to `Extensions` directly (or with `AddFenceRenderer`) takes effect on
  the next render, as with any other extension.
- **Failures.** When the factory throws (or returns a control that already has a parent), the block
  renders as an ordinary code block and the exception is logged as a warning through Avalonia's
  logger (area `Control`), so one faulty block never takes the rest of the document down.
- **Styling.** The control is written inside a `Border` with the `markdown-custom-fence` and
  `markdown-fence-<language>` classes (the language in lower case, e.g. `markdown-fence-chart`);
  see [Theming](theming.md#core).
- **Selection.** The `Border` is selected as a whole and copied as the block's fenced source
  (```` ```chart {height=200} ```` … ```` ``` ````), as Mermaid diagrams are. To copy something
  else, set [`MarkdownSelection.CopyText`](#content-that-is-not-text) on the control the factory
  returns; set it to `null` to leave the control out of selection.

A fence renderer is itself an `IMarkViewExtension` — `new FenceRendererExtension("chart", factory)`
— so it can be added to an `Extensions` list like any other extension.

### Following the theme

A control styled with `DynamicResource` follows light/dark switches on its own. When the control
bakes colours in (a chart drawn to a bitmap, say), rebuild them with
[`ThemeTracking.ReapplyOnThemeChange`](#content-that-depends-on-the-theme), from the factory:

```csharp
viewer.UseFenceRenderer("chart", (code, info) =>
{
    var image = new Image();
    void Bake() => image.Source = MyChart.Render(code,
        isDark: Application.Current?.ActualThemeVariant == ThemeVariant.Dark);
    Bake();
    ThemeTracking.ReapplyOnThemeChange(image, Bake);
    return image;
});
```

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

        var code = new TextBlock { Text = source };
        renderer.ApplyTheme(code, "MarkdownCodeTheme");
        var border = new Border { Child = code };
        border.Classes.Add("markdown-code-block");
        renderer.ApplyTheme(border, "MarkdownCodeBlockTheme");
        renderer.WriteBlock(border);
    }
}
```

The built-in look comes from [control themes](theming.md#sizes-spacing-and-control-themes), not from
the `markdown-*` classes: `renderer.ApplyTheme(control, key)` gives a control the theme with that
resource key, found from the viewer, so it follows presets and is swapped when a preset is added or
removed. A class alone only matches the application's own styles. Set the class too, so those
styles and the [style class reference](theming.md#style-class-reference) still apply.

A custom code block renderer does not get the built-in [code block toolbar](configuration.md#code-block-toolbar), nor its line numbers, wrapping and maximum height; add your own controls if you want them.

`renderer.WriteBlock(control)` adds the control to the current `Panel` container.  
`renderer.WriteInline(inline)` adds an inline to the current `InlineCollection`.  
`renderer.Push(container)` / `renderer.Pop()` manage the render stack.

### Renderers and streaming

While a document is [streamed](streaming.md), a renderer runs again for the block that contains the
end of the text on every update. `renderer.IsProvisional(obj)` says whether the block is in that
state, so a renderer can show a cheap placeholder, such as the source text, instead of building a
chart or fetching a resource from a half-written block. See
[For custom renderers](streaming.md#for-custom-renderers) for a sketch. A renderer for
`FencedCodeBlock` registered before `UseMath()` receives the `MathBlock` (empty `Info`, fence
character `$`) in the provisional frame, so it must dispatch on `Info` as the built-in ones do.

### Content that depends on the theme

Controls styled through `DynamicResource` follow light/dark switches on their own. Content whose
colours are baked in at render time — bitmaps, SVG, per-token brushes — has to be rebuilt instead.
Build it once, then hand the rebuild to `ThemeTracking.ReapplyOnThemeChange`:

```csharp
using Avalonia;
using Avalonia.Styling;
using MarkView.Avalonia.Rendering;

public class ChartBlockRenderer : AvaloniaObjectRenderer<ChartBlock>
{
    protected override void Write(AvaloniaRenderer renderer, ChartBlock obj)
    {
        var image = new Image();
        var border = new Border { Child = image };

        void Bake() => image.Source = MyChart.Render(obj,
            isDark: Application.Current?.ActualThemeVariant == ThemeVariant.Dark);

        Bake();
        ThemeTracking.ReapplyOnThemeChange(border, Bake);
        renderer.WriteBlock(border);
    }
}
```

The callback runs when the theme changes while the element is shown, and when the element is
attached again after a switch that happened while it was detached (e.g. on an inactive tab). It
listens to the element's own inherited theme, so a document that is never shown, or is replaced
by a re-render, is garbage-collected normally. Subscribing to `Application.Current` events
directly instead keeps every rendered document alive for the lifetime of the application.

Pass an element that stays in the document for as long as the content should follow the theme —
here the `Border`, not the `Image`, so that a renderer swapping the image for an error fallback
keeps tracking the theme.

### Content that is costly to build

When the viewer's [lazy rendering](configuration.md#lazy-rendering) is on, its own renderers build
images, formulas, diagrams and syntax colours only once they are about to scroll into view. A custom
renderer can do the same with `ViewportTracking.RunWhenNearViewport`, when
`renderer.IsLazyRenderingEnabled` is set:

```csharp
var built = false;
ThemeTracking.ReapplyOnThemeChange(border, () => { if (built) Bake(); });

if (renderer.IsLazyRenderingEnabled)
    ViewportTracking.RunWhenNearViewport(border, () => { built = true; Bake(); });
else
{
    built = true;
    Bake();
}
renderer.WriteBlock(border);
```

The callback runs once, the first time the element is laid out within one viewport height of the
visible area, during that layout pass and before it is drawn: content it builds is laid out before
it is ever seen. An element that is never shown never runs it, and is garbage-collected normally.
A bare `AvaloniaRenderer` has the switch off by default, since nothing guarantees that its output will
be laid out in a viewport; `MarkdownViewer` turns it on from its `IsLazyRenderingEnabled` property.

### Content that is not text

Document-wide [text selection](text-selection.md) only knows how to copy text. Give a non-text
control the text a selection should copy in its place with the `MarkdownSelection.CopyText`
attached property:

```csharp
var border = new Border { Child = image };
MarkdownSelection.SetCopyText(border, obj.Lines.ToString());   // copy the chart's source
renderer.WriteBlock(border);
```

- A control written with `WriteBlock` is selectable only when it has copy text. It is then
  highlighted and copied as a whole; dragging past its vertical middle includes it in the selection.
- A control written with `WriteInline(Control)` is always highlighted with the surrounding text,
  and copied as its copy text, or as nothing when it has none.
- [Copied as Markdown](text-selection.md#copying-as-markdown), a block your renderer wrote is
  its source text when the selection covers it whole. When the selection only covers part of it,
  a block control is its copy text as is, so give it the block's Markdown source (as the fence
  renderers, formulas and diagrams do), and the text of your other controls is copied as escaped
  text.

As with theme tracking, set it on the element that stays in the document (the `Border`), so an
error fallback that replaces the border's content is still copied.

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

When no loader returns an image, or a loader throws (other than for the cancellation it was given,
which comes when the image is removed while it loads), the viewer shows a
[placeholder](configuration.md#images-that-fail-to-load) instead. A loaded image is kept in the
[image cache](configuration.md#image-cache) under its URL and the types of the loaders in the chain,
so a loader is not asked again for an image it already returned. Its result should therefore depend
only on the URL. The viewer may show the image a loader returns in several places at once, and never
disposes it.

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

`IDualThemeCodeHighlighter` goes further: `HighlightBoth(line, language)` tokenises a line once and returns each token's colour for both variants, so a theme switch only recolours the existing text instead of highlighting it again and rebuilding it.

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
    .UseGitHubExtensions()
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
