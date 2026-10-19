// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Shapes = Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

using Markdig.Syntax;
using MarkView.Avalonia.Mermaid;
using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Demo.MermaidStyles;

/// <summary>
/// Renders a Mermaid fence with the wrapped <see cref="MermaidBlockRenderer"/>, then lays a styles
/// button (a palette icon) over the diagram's top-right corner. The button raises
/// <see cref="MermaidStylesDialog.RequestedEvent"/>, which bubbles up to the page that shows the dialog.
/// Other fences, and a diagram still being streamed, are left to the wrapped renderer alone.
/// </summary>
internal sealed class MermaidStylesButtonRenderer(MermaidBlockRenderer inner) : AvaloniaObjectRenderer<FencedCodeBlock>
{
    // A painter's palette (16×16), drawn rather than taken from a font, as the browser build only has
    // the application's fonts.
    private static readonly Geometry PaletteIcon = Geometry.Parse(
        "M8,1.5 C4.4,1.5 1.5,4.4 1.5,8 C1.5,11.6 4.4,14.5 8,14.5 C9,14.5 9.6,13.9 9.6,13.1 " +
        "C9.6,12.3 9,11.9 9,11.2 C9,10.5 9.6,9.9 10.3,9.9 L11.8,9.9 C13.3,9.9 14.5,8.7 14.5,7.2 " +
        "C14.5,4.1 11.6,1.5 8,1.5 Z " +
        "M4.6,8.6 A0.6,0.6 0 1 1 4.61,8.6 Z M5.6,5.4 A0.6,0.6 0 1 1 5.61,5.4 Z " +
        "M8.6,4 A0.6,0.6 0 1 1 8.61,4 Z M11.4,5.6 A0.6,0.6 0 1 1 11.41,5.6 Z");

    protected override void Write(AvaloniaRenderer renderer, FencedCodeBlock obj)
    {
        if (!string.Equals(obj.Info, "mermaid", StringComparison.OrdinalIgnoreCase) || renderer.IsProvisional(obj))
        {
            inner.Write(renderer, obj);
            return;
        }

        // As wide as the diagram, so the button sits on its corner rather than at the far side of the page.
        // A plain Panel: selection looks inside it and still copies the diagram's source.
        var host = new Panel { HorizontalAlignment = HorizontalAlignment.Left };
        renderer.Push(host);
        inner.Write(renderer, obj);
        renderer.Pop();

        var code = ExtractSource(obj);
        var icon = new Shapes.Path
        {
            Data = PaletteIcon,
            Width = 16,
            Height = 16,
            StrokeThickness = 1.25,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
        };
        var button = new Button
        {
            Content = icon,
            Padding = new Thickness(5),
            MinHeight = 0,
            MinWidth = 0,
            Margin = new Thickness(0, 8, 4, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
        };
        // The glyph is drawn in the button's text colour, so it follows the theme and the hover state.
        icon[!Shapes.Shape.StrokeProperty] = button[!TemplatedControl.ForegroundProperty];
        ToolTip.SetTip(button, "Compare this diagram in each Mermaid style");
        AutomationProperties.SetName(button, "Compare diagram styles");
        button.Click += (_, e) =>
        {
            e.Handled = true;
            button.RaiseEvent(new MermaidStylesRequestedEventArgs(code));
        };
        host.Children.Add(button);

        // Shown once the diagram has rendered: until then it has no width, and a diagram that fails
        // to render is replaced by its error message.
        if (host.Children is [Border { Child: Image image }, ..])
        {
            button.IsVisible = false;
            image.PropertyChanged += (_, e) =>
            {
                if (e.Property == Image.SourceProperty)
                    button.IsVisible = image.Source is not null;
            };
        }

        renderer.WriteBlock(host);
    }

    private static string ExtractSource(FencedCodeBlock block)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < block.Lines.Count; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(block.Lines.Lines[i].Slice.AsSpan());
        }
        return sb.ToString();
    }
}
