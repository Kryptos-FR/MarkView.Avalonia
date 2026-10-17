// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

using MarkView.Avalonia.Parsing;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Renders an HTML <c>&lt;details&gt;</c> element (<see cref="DetailsBlock"/>) as a collapsible block, like a
/// foldable alert (see <see cref="CollapsibleAlert"/>): a <c>markdown-details</c> <see cref="Border"/> holding
/// the summary line (<c>markdown-details-summary</c>, starting with a <c>markdown-details-chevron</c>), which
/// toggles the body (<c>markdown-details-content</c>) on click, Enter or Space. The body starts shown for
/// <c>&lt;details open&gt;</c>, hidden otherwise.
/// </summary>
internal sealed class DetailsBlockRenderer : AvaloniaObjectRenderer<DetailsBlock>
{
    /// <summary>The summary shown when the element has none, as browsers do.</summary>
    internal const string DefaultSummary = "Details";

    protected override void Write(AvaloniaRenderer renderer, DetailsBlock obj)
    {
        var chevron = new AlertChevron();
        chevron.Classes.Add("markdown-details-chevron");

        var summary = new TextBlock { TextWrapping = TextWrapping.Wrap };
        summary.Classes.Add("markdown-details-summary");
        summary.Inlines!.Add(new InlineUIContainer(chevron));
        summary.Inlines.Add(new Run(" "));
        summary.Inlines.Add(new Run(obj.Summary ?? DefaultSummary));

        var content = new StackPanel { Spacing = 8 };
        content.Classes.Add("markdown-details-content");

        var outer = new StackPanel { Spacing = 8 };
        outer.Children.Add(summary);
        outer.Children.Add(content);

        var border = new Border { Child = outer };
        border.Classes.Add("markdown-details");

        renderer.Push(content);
        renderer.WriteChildren(obj);
        renderer.Pop();

        CollapsibleAlert.Attach(border, summary, chevron, content, obj.StartsExpanded);
        renderer.WriteBlock(border);
    }
}
