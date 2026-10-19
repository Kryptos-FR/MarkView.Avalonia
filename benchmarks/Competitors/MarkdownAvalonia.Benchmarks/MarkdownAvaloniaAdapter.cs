// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Styling;
using Markdown.Avalonia;
using MarkView.Benchmarks.Shared;

namespace MarkdownAvalonia.Benchmarks;

/// <summary>
/// Markdown.Avalonia (the full package, so code blocks are highlighted with AvaloniaEdit, which the library
/// loads by itself when the assembly is present). It has its own parser, not Markdig, so it has no equivalent
/// of the Markdig presets: one configuration, as installed.
/// </summary>
public sealed class MarkdownAvaloniaAdapter : IViewerAdapter
{
    public string Library => LibraryVersion.Of(typeof(MarkdownScrollViewer), "Markdown.Avalonia");

    public IReadOnlyList<string> Configurations { get; } = ["Default"];

    // The control applies its own style collection (MarkdownStyle); nothing to add to the application.
    public void AddStyles(Styles styles)
    {
    }

    public Control CreateViewer(string configuration) => new MarkdownScrollViewer();

    public void SetMarkdown(Control viewer, string markdown) => ((MarkdownScrollViewer)viewer).Markdown = markdown;
}
