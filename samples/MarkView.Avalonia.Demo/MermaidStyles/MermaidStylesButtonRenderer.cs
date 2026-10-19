// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

using Markdig.Syntax;
using MarkView.Avalonia.Mermaid;
using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Demo.MermaidStyles;

/// <summary>
/// Renders a Mermaid fence with the wrapped <see cref="MermaidBlockRenderer"/>, then lays a "Styles"
/// button over the diagram's top-right corner. The button raises
/// <see cref="MermaidStylesDialog.RequestedEvent"/>, which bubbles up to the page that shows the dialog.
/// Other fences, and a diagram still being streamed, are left to the wrapped renderer alone.
/// </summary>
internal sealed class MermaidStylesButtonRenderer(MermaidBlockRenderer inner) : AvaloniaObjectRenderer<FencedCodeBlock>
{
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
        var button = new Button
        {
            Content = "Styles",
            FontSize = 12,
            Padding = new Thickness(8, 2),
            MinHeight = 0,
            Margin = new Thickness(0, 8, 4, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
        };
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
