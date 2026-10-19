// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

using Markdig.Syntax;

using MarkView.Avalonia.Parsing;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Renders a language model's reasoning (<see cref="ThinkingBlock"/>, see
/// <see cref="MarkdownExtensions.UseThinkingBlocks"/>) as a collapsible block, like a details element (see
/// <see cref="CollapsibleAlert"/>): a <c>markdown-thinking</c> <see cref="Border"/> holding a header
/// (<c>markdown-thinking-header</c>, starting with a <c>markdown-thinking-chevron</c>) that toggles the reasoning
/// (a <c>markdown-thinking-content</c> <see cref="Border"/> around a <see cref="StackPanel"/>) on click, Enter or Space. While a streaming session has not received the
/// closing tag yet, the border has the <c>:thinking</c> pseudo-class and the header shows
/// <see cref="ThinkingBlockOptions.ThinkingTitle"/>. A complete block with no content (some models write an empty
/// one when thinking is off) renders nothing.
/// </summary>
internal sealed class ThinkingBlockRenderer : AvaloniaObjectRenderer<ThinkingBlock>, IGrowingBlockRenderer
{
    /// <summary>The titles and fold behaviour, set by <see cref="ThinkingBlockExtension"/>.</summary>
    public ThinkingBlockOptions Options { get; set; } = new();

    protected override void Write(AvaloniaRenderer renderer, ThinkingBlock obj)
    {
        if (WriteShell(renderer, obj) is not { } content)
            return;
        renderer.Push(content);
        renderer.WriteChildren(obj);
        renderer.Pop();
    }

    // The header changes when the reasoning completes, and the block is then rendered again.
    public object? ShellKey(AvaloniaRenderer renderer, ContainerBlock block) => IsThinking(renderer, (ThinkingBlock)block);

    private static bool IsThinking(AvaloniaRenderer renderer, ThinkingBlock block) => renderer.IsStreaming && !block.HasClosingTag;

    public Panel? WriteShell(AvaloniaRenderer renderer, ContainerBlock block)
    {
        var obj = (ThinkingBlock)block;
        var isThinking = IsThinking(renderer, obj);
        if (!isThinking && obj.Count == 0)
            return null;

        var duration = renderer.MeasureThinking(obj, isThinking);
        var title = isThinking
            ? Options.ThinkingTitle
            : duration is { } elapsed && Options.DurationTitle is { } format ? format(elapsed) : Options.Title;

        var chevron = new AlertChevron();
        chevron.Classes.Add("markdown-thinking-chevron");
        var header = new FoldHeader { TextWrapping = TextWrapping.Wrap };
        header.Classes.Add("markdown-thinking-header");
        header.Inlines!.Add(new InlineUIContainer(chevron));
        header.Inlines.Add(new Run(" "));
        header.Inlines.Add(new Run(title));

        var content = new StackPanel { Spacing = 8 };
        var body = new Border { Child = content };
        body.Classes.Add("markdown-thinking-content");

        var outer = new StackPanel { Spacing = 8 };
        outer.Children.Add(header);
        outer.Children.Add(body);

        var border = new Border { Child = outer };
        border.Classes.Add("markdown-thinking");
        ((IPseudoClasses)border.Classes).Set(":thinking", isThinking);
        AutomationRoles.Set(border, AutomationControlType.Group);
        AutomationProperties.SetName(border, title);

        var expanded = renderer.GetFoldState(obj, defaultValue: isThinking ? Options.ExpandWhileThinking : Options.ExpandWhenDone);
        CollapsibleAlert.Attach(border, header, chevron, body, expanded, renderer.FoldStateRecorder(obj));
        renderer.WriteBlock(border);
        return content;
    }
}
