// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Svg.Skia;

using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Svg.Tests;

public class SvgImageLoaderAvaresTests
{
    [AvaloniaFact]
    public async Task Relative_svg_in_an_embedded_document_loads_from_the_embedded_resource()
    {
        var viewer = new MarkdownViewer { IsLazyRenderingEnabled = false };
        viewer.UseSvg();
        viewer.Source = new Uri("avares://MarkView.Avalonia.Svg.Tests/TestAssets/embedded.md");
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        window.Show();
        try
        {
            var image = ((Control)viewer.Content!).GetLogicalDescendants().OfType<Image>().Single();
            Assert.Equal("avares://MarkView.Avalonia.Svg.Tests/TestAssets/icon.svg", image.Tag?.ToString());

            await AsyncTestHelpers.WaitUntilAsync(() => image.Source is SvgImage);
        }
        finally
        {
            window.Close();
        }
    }
}
