// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;

namespace MarkView.Avalonia;

public partial class MarkdownViewer
{
    // The viewer's own context menu, given to it when nothing else sets ContextFlyout or ContextMenu.
    private MenuFlyout? _defaultContextFlyout;

    // The link the context menu was opened on, for its Copy link item.
    private MarkdownHyperlink? _contextLink;

    /// <inheritdoc/>
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        // Styles are applied by now, so a ContextFlyout set by the application, locally or in a style, even
        // to null, is set; the default only fills the gap, and any later value replaces it.
        if (!IsSet(ContextFlyoutProperty) && !IsSet(ContextMenuProperty))
            SetCurrentValue(ContextFlyoutProperty, _defaultContextFlyout ??= CreateDefaultContextFlyout());
    }

    /// <summary>
    /// The viewer's default context menu: Copy and Copy as Markdown (enabled while something is selected),
    /// Copy link (shown when opened on a link) and Select all. Its labels are the resources
    /// <c>StringTextFlyoutCopyText</c> (the one a <see cref="TextBox"/> uses), <c>MarkdownStringCopyAsMarkdown</c>,
    /// <c>MarkdownStringCopyLink</c> and <c>MarkdownStringSelectAll</c>, read when it opens, in English when
    /// they are not found.
    /// </summary>
    private MenuFlyout CreateDefaultContextFlyout()
    {
        var copy = new MenuItem();
        copy.Classes.Add("markdown-context-copy");
        copy.Click += (_, _) => _ = CopyToClipboardAsync();

        var copyAsMarkdown = new MenuItem();
        copyAsMarkdown.Classes.Add("markdown-context-copy-markdown");
        copyAsMarkdown.Click += (_, _) => _ = CopyAsMarkdownAsync();

        var copyLink = new MenuItem();
        copyLink.Classes.Add("markdown-context-copy-link");
        copyLink.Click += (_, _) =>
        {
            if (_contextLink?.NavigateUri is { } uri)
                _ = CopyAsync(MarkdownCopyOrigin.Link, uri.OriginalString);
        };

        var selectAll = new MenuItem();
        selectAll.Classes.Add("markdown-context-select-all");
        selectAll.Click += (_, _) => SelectAll();

        var flyout = new MenuFlyout { Items = { copy, copyAsMarkdown, copyLink, selectAll } };
        flyout.Opening += (_, _) =>
        {
            copy.Header = Text("StringTextFlyoutCopyText", "Copy");
            copyAsMarkdown.Header = Text("MarkdownStringCopyAsMarkdown", "Copy as Markdown");
            copyLink.Header = Text("MarkdownStringCopyLink", "Copy link");
            selectAll.Header = Text("MarkdownStringSelectAll", "Select all");

            var hotkeys = Application.Current?.PlatformSettings?.HotkeyConfiguration;
            copy.InputGesture = hotkeys?.Copy.FirstOrDefault();
            selectAll.InputGesture = hotkeys?.SelectAll.FirstOrDefault();

            copy.IsEnabled = copyAsMarkdown.IsEnabled = HasSelection;
            copyLink.IsVisible = _contextLink?.NavigateUri is not null;
        };
        flyout.Closed += (_, _) => _contextLink = null;
        return flyout;
    }

    private string Text(string key, string fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is string text ? text : fallback;

    /// <summary>
    /// Notes the link a context menu is requested on, under the pointer or focused. A request with the pointer
    /// outside the selected text clears the selection first, as a text editor does; one inside keeps it.
    /// </summary>
    private void OnContentContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (_selectionLayer is not { } layer) return;
        if (e.TryGetPosition(layer, out var position))
        {
            _contextLink = HyperlinkAt(position);
            if (layer.SelectedRange is var (start, end) && layer.HitTestOffset(position) is { } offset
                && (offset < start || offset > end))
                layer.ClearSelection();
        }
        else
            _contextLink = (e.Source as LinkFocusTarget)?.Link;
    }
}
