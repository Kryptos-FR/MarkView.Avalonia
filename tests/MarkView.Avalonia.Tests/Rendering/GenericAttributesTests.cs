// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Markdig;
using Markdig.Renderers.Html;
using MarkView.Avalonia.Rendering;
using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

public class GenericAttributesTests
{
    private static StackPanel Render(string markdown, bool applyAttributeClasses, Action<Markdig.Syntax.MarkdownDocument>? edit = null)
    {
        var pipeline = new MarkdownPipelineBuilder().UseCustomContainers().UseGenericAttributes().Build();
        var document = Markdig.Markdown.Parse(markdown, pipeline);
        edit?.Invoke(document);
        var renderer = new AvaloniaRenderer { ApplyAttributeClasses = applyAttributeClasses };
        pipeline.Setup(renderer);
        renderer.Render(document);
        return renderer.RootPanel;
    }

    private static string[] Classes(StyledElement element) =>
        element.Classes.Where(c => !c.StartsWith(':')).ToArray();

    [AvaloniaFact]
    public void Block_classes_are_added_to_the_block_control()
    {
        var root = Render("""
            # Title {.special}

            Text {.note .wide}
            """, applyAttributeClasses: true);

        Assert.Equal(["markdown-heading", "markdown-h1", "special"], Classes(root.Children[0]));
        Assert.Equal(["markdown-paragraph", "note", "wide"], Classes(root.Children[1]));
    }

    [AvaloniaFact]
    public void Inline_classes_are_added_to_the_inline()
    {
        var root = Render("A [link](https://example.com){.button} and *word*{.hot}", applyAttributeClasses: true);

        var paragraph = Assert.IsAssignableFrom<TextBlock>(Assert.Single(root.Children));
        Assert.Equal(["markdown-paragraph"], Classes(paragraph));
        var inlines = paragraph.Inlines!.Where(i => i.Classes.Count > 0).ToList();
        Assert.Contains(inlines, i => i.Classes.Contains("button"));
        Assert.Contains(inlines, i => i is Italic && i.Classes.Contains("hot"));
    }

    [AvaloniaFact]
    public void Image_classes_are_added_to_the_image_control()
    {
        var root = Render("![logo](logo.png){.icon}", applyAttributeClasses: true);

        var paragraph = Assert.IsAssignableFrom<TextBlock>(Assert.Single(root.Children));
        var container = Assert.IsType<InlineUIContainer>(Assert.Single(paragraph.Inlines!));
        var image = Assert.IsType<Image>(container.Child);
        Assert.Equal(["markdown-image", "icon"], Classes(image));
    }

    [AvaloniaFact]
    public void Block_written_outside_any_markdown_object_gets_no_classes()
    {
        var renderer = new AvaloniaRenderer { ApplyAttributeClasses = true };
        var border = new Border();

        renderer.WriteBlock(border);

        Assert.Same(border, Assert.Single(renderer.RootPanel.Children));
        Assert.Empty(Classes(border));
    }

    [AvaloniaFact]
    public void Classes_go_to_the_outer_control_only()
    {
        var root = Render("""
            :::note {.outer}
            Inside
            :::
            """, applyAttributeClasses: true);

        var border = Assert.IsType<Border>(Assert.Single(root.Children));
        Assert.Contains("outer", border.Classes);
        var paragraph = Assert.Single(Assert.IsType<StackPanel>(border.Child).Children);
        Assert.Equal(["markdown-paragraph"], Classes(paragraph));
    }

    [AvaloniaFact]
    public void Inner_classes_stay_on_the_inner_control()
    {
        // The container writes its border after its children.
        var root = Render("""
            :::note
            Inside {.inner}
            :::
            """, applyAttributeClasses: true);

        var border = Assert.IsType<Border>(Assert.Single(root.Children));
        Assert.Equal(["markdown-custom-container", "note"], Classes(border));
        var paragraph = Assert.Single(Assert.IsType<StackPanel>(border.Child).Children);
        Assert.Equal(["markdown-paragraph", "inner"], Classes(paragraph));
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Code_block_classes_go_to_the_block_not_to_its_text(bool showToolbar)
    {
        // The code's text runs are written before the block itself.
        var pipeline = new MarkdownPipelineBuilder().UseGenericAttributes().Build();
        var document = Markdig.Markdown.Parse("```csharp {.sample}\nvar x = 1;\nvar y = 2;\n```\n", pipeline);
        var renderer = new AvaloniaRenderer
        {
            ApplyAttributeClasses = true,
            ShowCodeBlockLanguage = showToolbar,
            ShowCodeBlockCopyButton = showToolbar,
        };
        pipeline.Setup(renderer);
        renderer.Render(document);

        var block = Assert.Single(renderer.RootPanel.Children);
        var code = showToolbar ? Assert.IsType<Border>(Assert.IsType<StackPanel>(block).Children[1]) : Assert.IsType<Border>(block);
        Assert.Contains("sample", Classes(block));
        Assert.All(Assert.IsType<TextBlock>(code.Child).Inlines!, run => Assert.Empty(Classes(run)));
    }

    [AvaloniaFact]
    public void Classes_are_ignored_unless_enabled()
    {
        var root = Render("Text {.note}", applyAttributeClasses: false);

        Assert.Equal(["markdown-paragraph"], Classes(Assert.Single(root.Children)));
    }

    [AvaloniaFact]
    public void Pseudo_class_names_are_skipped()
    {
        var root = Render("Text", applyAttributeClasses: true, document =>
        {
            var attributes = document[0].GetAttributes();
            attributes.AddClass(":pointerover");
            attributes.AddClass("");
            attributes.AddClass("kept");
        });

        Assert.Equal(["markdown-paragraph", "kept"], Classes(Assert.Single(root.Children)));
    }

    [AvaloniaFact]
    public void Viewer_applies_classes_when_its_pipeline_uses_generic_attributes()
    {
        var viewer = new MarkdownViewer
        {
            Pipeline = new MarkdownPipelineBuilder().UseGenericAttributes().Build(),
            Markdown = "Text {.note}",
        };

        var root = Assert.IsType<StackPanel>(Assert.IsType<Grid>(viewer.Content).Children[0]);
        Assert.Contains("note", Assert.Single(root.Children).Classes);
    }

    [AvaloniaFact]
    public void Viewer_does_not_apply_classes_other_extensions_attach()
    {
        // Markdig stores "language-csharp" as an attribute class of every fenced code block.
        var viewer = new MarkdownViewer
        {
            Markdown = """
                ```csharp
                var x = 1;
                ```
                """,
        };

        var root = Assert.IsType<StackPanel>(Assert.IsType<Grid>(viewer.Content).Children[0]);
        var container = Assert.IsType<StackPanel>(Assert.Single(root.Children));
        Assert.Equal(["markdown-code-container"], Classes(container));
        var code = Assert.IsType<Border>(container.Children[1]);
        Assert.Equal(["markdown-code-block", "language-csharp"], Classes(code));
    }
}
