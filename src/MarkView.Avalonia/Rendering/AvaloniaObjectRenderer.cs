using Markdig.Renderers;
using Markdig.Syntax;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Base class for all Avalonia markdown object renderers.
/// </summary>
/// <typeparam name="TObject">The type of Markdig AST node this renderer handles.</typeparam>
public abstract class AvaloniaObjectRenderer<TObject> : MarkdownObjectRenderer<AvaloniaRenderer, TObject>
    where TObject : MarkdownObject
{
}
