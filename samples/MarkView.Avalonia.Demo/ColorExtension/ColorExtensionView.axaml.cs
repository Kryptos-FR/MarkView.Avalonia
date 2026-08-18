// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;

using Markdig;

namespace MarkView.Avalonia.Demo.ColorExtension;

/// <summary>
/// Hosts a <see cref="MarkdownViewer"/> configured with its own <see cref="MarkdownViewer.Pipeline"/>
/// and <see cref="MarkdownViewer.Extensions"/> instead of <see cref="MarkdownViewerDefaults"/> — the
/// markdown content below walks through how <c>%[color:NAME]text%</c> spans are parsed and rendered.
/// </summary>
public partial class ColorExtensionView : UserControl
{
    private static readonly Uri TutorialSource =
        new("avares://MarkView.Avalonia.Demo/Assets/color-extension.md");

    public ColorExtensionView()
    {
        InitializeComponent();

        Viewer.Pipeline = new MarkdownPipelineBuilder()
            .UseSupportedExtensions()
            .UseColorSpans()
            .Build();
        Viewer.Extensions.Add(new ColorSpanExtension());
        Viewer.Source = TutorialSource;
    }
}
