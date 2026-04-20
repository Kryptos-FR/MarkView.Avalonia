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
