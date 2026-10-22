// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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

    // The table the context menu was opened on, for its Copy table items.
    private Grid? _contextTable;

    // The anchor of the heading the context menu was opened on, for its Copy link to heading item.
    private string? _contextHeading;

    // The image or diagram the context menu was opened on, for its Save image as item.
    private Image? _contextImage;

    // Noted on the way down, before the viewer opens its menu: a click on a cell's padding, outside its text,
    // reaches the viewer's background rather than the document.
    private static readonly IDisposable ContextBlockHandler = ContextRequestedEvent.AddClassHandler<MarkdownViewer>(
        (viewer, e) => viewer.NoteContextBlock(e), RoutingStrategies.Tunnel);

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
    /// Copy link (shown when opened on a link), Copy link to heading (shown when opened on a heading), Copy table
    /// and Copy table as Markdown (shown when opened on a table), Save image as (shown when opened on an image or
    /// diagram that can be saved, see <see cref="IsImageSaveEnabled"/>) and Select all. Its labels are the resources
    /// <c>StringTextFlyoutCopyText</c> (the one a <see cref="TextBox"/> uses), <c>MarkdownStringCopyAsMarkdown</c>,
    /// <c>MarkdownStringCopyLink</c>, <c>MarkdownStringCopyHeadingLink</c>, <c>MarkdownStringCopyTable</c>,
    /// <c>MarkdownStringCopyTableAsMarkdown</c>, <c>MarkdownStringSaveImage</c> and <c>MarkdownStringSelectAll</c>,
    /// read when it opens, in English when they are not found.
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

        var copyHeadingLink = new MenuItem();
        copyHeadingLink.Classes.Add("markdown-context-copy-heading-link");
        copyHeadingLink.Click += (_, _) =>
        {
            if (_contextHeading is { } anchor)
                _ = CopyAsync(MarkdownCopyOrigin.HeadingLink, HeadingLink(anchor));
        };

        var copyTable = new MenuItem();
        copyTable.Classes.Add("markdown-context-copy-table");
        copyTable.Click += (_, _) =>
        {
            if (_contextTable is { } table)
                _ = CopyTableAsync(table);
        };

        var copyTableAsMarkdown = new MenuItem();
        copyTableAsMarkdown.Classes.Add("markdown-context-copy-table-markdown");
        copyTableAsMarkdown.Click += (_, _) =>
        {
            if (_contextTable is { } table)
                _ = CopyTableAsMarkdownAsync(table);
        };

        var saveImage = new MenuItem();
        saveImage.Classes.Add("markdown-context-save-image");
        saveImage.Click += (_, _) =>
        {
            if (_contextImage is { } image)
                _ = SaveImageAsync(image);
        };

        var selectAll = new MenuItem();
        selectAll.Classes.Add("markdown-context-select-all");
        selectAll.Click += (_, _) => SelectAll();

        var flyout = new MenuFlyout { Items = { copy, copyAsMarkdown, copyLink, copyHeadingLink, copyTable, copyTableAsMarkdown, saveImage, selectAll } };
        flyout.Opening += (_, _) =>
        {
            copy.Header = Text("StringTextFlyoutCopyText", "Copy");
            copyAsMarkdown.Header = Text("MarkdownStringCopyAsMarkdown", "Copy as Markdown");
            copyLink.Header = Text("MarkdownStringCopyLink", "Copy link");
            copyHeadingLink.Header = Text("MarkdownStringCopyHeadingLink", "Copy link to heading");
            copyTable.Header = Text("MarkdownStringCopyTable", "Copy table");
            copyTableAsMarkdown.Header = Text("MarkdownStringCopyTableAsMarkdown", "Copy table as Markdown");
            saveImage.Header = Text("MarkdownStringSaveImage", "Save image as…");
            selectAll.Header = Text("MarkdownStringSelectAll", "Select all");

            var hotkeys = Application.Current?.PlatformSettings?.HotkeyConfiguration;
            copy.InputGesture = hotkeys?.Copy.FirstOrDefault();
            selectAll.InputGesture = hotkeys?.SelectAll.FirstOrDefault();

            copy.IsEnabled = copyAsMarkdown.IsEnabled = HasSelection;
            copyLink.IsVisible = _contextLink?.NavigateUri is not null;
            copyHeadingLink.IsVisible = _contextHeading is not null;
            copyTable.IsVisible = copyTableAsMarkdown.IsVisible = _contextTable is not null;
            saveImage.IsVisible = CanSaveImage(_contextImage);
        };
        flyout.Closed += (_, _) =>
        {
            _contextLink = null;
            _contextTable = null;
            _contextHeading = null;
            _contextImage = null;
        };
        return flyout;
    }

    private string Text(string key, string fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is string text ? text : fallback;

    /// <summary>
    /// Notes the link and the image a context menu is requested on, under the pointer or focused. A request with the pointer
    /// outside the selected text clears the selection first, as a text editor does; one inside keeps it.
    /// </summary>
    private void OnContentContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        _contextImage = e.Source as Image;
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

    /// <summary>
    /// Notes the table a context menu is requested on, the one under the pointer or the one holding the focused
    /// link, and the heading under the pointer. None while the lightbox covers the document.
    /// </summary>
    private void NoteContextBlock(ContextRequestedEventArgs e)
    {
        _contextTable = null;
        _contextHeading = null;
        if (_lightbox is not null || _selectionLayer is not { } layer || Content is not Grid { Children: [Panel root, ..] })
            return;
        if (e.TryGetPosition(layer, out var position))
        {
            _contextTable = TableCopy.TableAt(root, layer, position);
            _contextHeading = HeadingAt(layer, position);
        }
        else if (e.Source is LinkFocusTarget target)
            _contextTable = TableCopy.TableOf(target.Link);
    }

    /// <summary>
    /// The anchor of the visible heading at <paramref name="position"/>, a position in <paramref name="relativeTo"/>,
    /// or <c>null</c>.
    /// </summary>
    private string? HeadingAt(Visual relativeTo, Point position)
    {
        foreach (var (anchor, control) in _anchors)
        {
            if (control.Classes.Contains("markdown-heading") && control.IsEffectivelyVisible
                && control.BoundsIn(relativeTo) is { } bounds && bounds.Contains(position))
                return anchor;
        }
        return null;
    }

    /// <summary>
    /// The link to the heading with the given anchor: the <see cref="Source"/> address with the anchor as its
    /// fragment when the document was loaded from the web or a file, which opens it from anywhere, and
    /// <c>#anchor</c> otherwise, which links to it from the same document.
    /// </summary>
    private string HeadingLink(string anchor) =>
        Source is { IsAbsoluteUri: true, Scheme: "http" or "https" or "file" } source
            ? $"{source.GetLeftPart(UriPartial.Query)}#{anchor}"
            : $"#{anchor}";
}
