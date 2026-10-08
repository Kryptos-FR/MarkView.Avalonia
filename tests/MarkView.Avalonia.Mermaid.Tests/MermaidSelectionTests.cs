// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Headless.XUnit;

using Xunit;

namespace MarkView.Avalonia.Mermaid.Tests;

public class MermaidSelectionTests
{
    [AvaloniaFact]
    public void Mermaid_diagram_is_copied_as_its_fenced_source()
    {
        const string markdown = """
            before

            ```mermaid
            graph TD
              A --> B
            ```

            after
            """;
        var viewer = new MarkdownViewer().UseMermaid();
        viewer.Markdown = markdown;

        viewer.SelectAll();

        Assert.Equal("before\n```mermaid\ngraph TD\n  A --> B\n```\nafter", viewer.GetSelectedText());
    }
}
