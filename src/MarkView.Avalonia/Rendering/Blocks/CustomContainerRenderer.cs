// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Logging;
using Avalonia.Media;

using Markdig.Extensions.CustomContainers;

using MarkView.Avalonia.Extensions;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Renders a Markdig <see cref="CustomContainer"/> (<c>:::name</c> fences, Markdig's
/// <c>UseCustomContainers()</c>) as a <c>Border.markdown-custom-container</c> around its blocks.
/// The border also gets the container's name as a class (<c>:::note</c> gives <c>note</c>), so each
/// kind of container can be styled on its own. Text after the name (<c>:::note Read this first</c>)
/// becomes a <c>markdown-custom-container-title</c> header.
/// A container whose name has a factory in <see cref="AvaloniaRenderer.ContainerRenderers"/> renders
/// as the control that factory builds around its blocks instead, in a <c>Border.markdown-container-control</c>.
/// </summary>
public sealed class CustomContainerRenderer : AvaloniaObjectRenderer<CustomContainer>
{
    protected override void Write(AvaloniaRenderer renderer, CustomContainer obj)
    {
        var contentPanel = new StackPanel { Spacing = 8 };

        renderer.Push(contentPanel);
        renderer.WriteChildren(obj);
        renderer.Pop();

        var arguments = obj.Arguments?.Trim() ?? string.Empty;
        if (obj.Info is { Length: > 0 } name
            && renderer.ContainerRenderers.TryGetValue(name, out var factory)
            && TryCreate(factory, name, arguments, contentPanel) is { } host)
        {
            renderer.WriteBlock(host);
            return;
        }

        // The content may be left inside a control the factory dropped.
        if (!Detach(contentPanel))
            return;

        if (arguments.Length > 0)
        {
            var title = new MarkdownSelectableTextBlock
            {
                Text = arguments,
                TextWrapping = TextWrapping.Wrap,
                Renderer = renderer,
            };
            title.Classes.Add("markdown-custom-container-title");
            contentPanel.Children.Insert(0, title);
        }

        var border = new Border { Child = contentPanel };
        border.Classes.Add("markdown-custom-container");
        // Avalonia reserves names starting with ':' for pseudo-classes.
        if (!string.IsNullOrEmpty(obj.Info) && obj.Info[0] != ':')
            border.Classes.Add(obj.Info);

        renderer.WriteBlock(border);
    }

    // The factory is application code: a failure renders the container as if it had no factory
    // instead of losing the rest of the document.
    private ComponentHost? TryCreate(ContainerRendererFactory factory, string name, string arguments, Panel content)
    {
        try
        {
            if (factory(content, arguments) is not { } control)
                return null;

            // Assigning the child throws when the factory returns a control that already has a parent.
            var host = new ComponentHost { Child = control, ContentPanel = content };
            host.Classes.Add("markdown-container-control");
            host.Classes.Add($"markdown-container-{name.ToLowerInvariant()}");
            if (control.IsSet(MarkdownSelection.CopyTextProperty))
                MarkdownSelection.SetCopyText(host, MarkdownSelection.GetCopyText(control));
            return host;
        }
        catch (Exception ex)
        {
            // Stryker disable once String : the message text is diagnostic output only
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this,
                "The container renderer for '{Name}' failed, the container renders as if it had none: {Exception}", name, ex);
            return null;
        }
    }

    /// <summary>
    /// Takes <paramref name="content"/> out of the control it was placed in, if any, and returns whether
    /// it could: only from the usual hosts of a control (a panel, a content control, a decorator).
    /// </summary>
    private bool Detach(Control content)
    {
        switch (content.Parent)
        {
            case null:
                return true;
            case Panel panel:
                panel.Children.Remove(content);
                break;
            case ContentControl contentControl:
                contentControl.Content = null;
                break;
            case Decorator decorator:
                decorator.Child = null;
                break;
            case ContentPresenter presenter:
                presenter.Content = null;
                break;
        }
        if (content.Parent is null)
            return true;

        // Stryker disable once String : the message text is diagnostic output only
        Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this,
            "A container renderer left the container's content in a {Parent} after failing, the container is not rendered", content.Parent.GetType().Name);
        return false;
    }
}
