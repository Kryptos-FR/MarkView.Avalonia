using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Mermaid;

/// <summary>
/// Registers <see cref="MermaidBlockRenderer"/> at the front of the renderer list
/// so it intercepts <c>```mermaid</c> fences before <c>CodeBlockRenderer</c>.
/// </summary>
public sealed class MermaidExtension : IMarkViewExtension
{
    private readonly double _initialHeight;

    public MermaidExtension(double initialHeight = 300)
    {
        _initialHeight = initialHeight;
    }

    public void Register(AvaloniaRenderer renderer)
    {
        renderer.ObjectRenderers.Insert(0, new MermaidBlockRenderer(_initialHeight));
    }
}
