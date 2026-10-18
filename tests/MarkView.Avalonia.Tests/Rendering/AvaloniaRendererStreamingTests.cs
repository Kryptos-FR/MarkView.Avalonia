// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

public class AvaloniaRendererStreamingTests
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseGitHubExtensions().Build();

    private static MarkdownDocument Parse(string text) => Markdown.Parse(text, Pipeline);

    [AvaloniaFact]
    public void TruncateAnchors_keeps_the_earlier_registration_of_a_duplicate_id()
    {
        var renderer = new AvaloniaRenderer();
        var first = new TextBlock();
        var second = new TextBlock();
        renderer.RegisterAnchor("x", first);
        renderer.RegisterAnchor("x", second);
        Assert.Equal(2, renderer.AnchorCount);
        Assert.Same(second, renderer.Anchors["x"]);

        renderer.TruncateAnchors(1);

        Assert.Equal(1, renderer.AnchorCount);
        Assert.Same(first, renderer.Anchors["x"]);
    }

    [AvaloniaFact]
    public void TruncateAnchors_zero_empties_anchors()
    {
        var renderer = new AvaloniaRenderer();
        renderer.RegisterAnchor("x", new TextBlock());
        renderer.RegisterAnchor("y", new TextBlock());

        renderer.TruncateAnchors(0);

        Assert.Empty(renderer.Anchors);
        Assert.Equal(0, renderer.AnchorCount);
    }

    [AvaloniaFact]
    public void TruncateAnchors_keeps_the_anchors_before_the_cut()
    {
        var renderer = new AvaloniaRenderer();
        var x = new TextBlock();
        var y = new TextBlock();
        renderer.RegisterAnchor("x", x);
        renderer.RegisterAnchor("y", y);
        renderer.RegisterAnchor("z", new TextBlock());

        renderer.TruncateAnchors(2);

        Assert.Equal(2, renderer.Anchors.Count);
        Assert.Same(x, renderer.Anchors["x"]);
        Assert.Same(y, renderer.Anchors["y"]);
        Assert.False(renderer.Anchors.ContainsKey("z"));
    }

    [AvaloniaFact]
    public void TruncateHeadings_drops_later_entries()
    {
        var renderer = new AvaloniaRenderer();
        renderer.RegisterHeading(1, "a", "a");
        renderer.RegisterHeading(2, "b", "b");
        renderer.RegisterHeading(2, "c", "c");
        Assert.Equal(3, renderer.HeadingCount);

        renderer.TruncateHeadings(2);

        Assert.Equal(2, renderer.HeadingCount);
        Assert.Equal(2, renderer.HeadingEntries.Count);
        Assert.Equal((2, "b", "b"), renderer.HeadingEntries[1]);
    }

    [AvaloniaFact]
    public void BeginDocument_resets_anchors_headings_and_slugs()
    {
        var renderer = new AvaloniaRenderer();
        renderer.RegisterAnchor("x", new TextBlock());
        renderer.RegisterHeading(1, "x", renderer.SlugGenerator.GenerateSlug("x"));

        renderer.BeginDocument();

        Assert.Equal(0, renderer.AnchorCount);
        Assert.Empty(renderer.Anchors);
        Assert.Equal(0, renderer.HeadingCount);
        Assert.Empty(renderer.HeadingEntries);
        Assert.Equal(0, renderer.SlugGenerator.Count);
        Assert.Equal("x", renderer.SlugGenerator.GenerateSlug("x"));
    }

    [AvaloniaFact]
    public void BeginDocument_places_the_custom_fence_renderer_once()
    {
        var renderer = new AvaloniaRenderer();
        renderer.FenceRenderers["chart"] = (code, _) => new TextBlock { Text = code };

        renderer.BeginDocument();
        var count = renderer.ObjectRenderers.Count;
        renderer.BeginDocument();

        Assert.Equal(count, renderer.ObjectRenderers.Count);
        Assert.Single(renderer.ObjectRenderers.OfType<CustomFenceRenderer>());
    }

    [AvaloniaFact]
    public void Render_places_the_custom_fence_renderer_once_across_renders()
    {
        var renderer = new AvaloniaRenderer();
        renderer.FenceRenderers["chart"] = (code, _) => new TextBlock { Text = code };
        var document = Parse("para");

        renderer.Render(document);
        renderer.Render(document);

        Assert.Single(renderer.ObjectRenderers.OfType<CustomFenceRenderer>());
    }

    [AvaloniaFact]
    public void WriteTopLevel_appends_to_the_root_panel()
    {
        var renderer = new AvaloniaRenderer();
        var document = Parse("one\n\ntwo");

        renderer.BeginDocument();
        renderer.WriteTopLevel(document[0]);
        renderer.WriteTopLevel(document[1]);

        Assert.Equal(2, renderer.RootPanel.Children.Count);
    }

    [AvaloniaFact]
    public void Render_returns_the_root_panel_with_the_document_blocks()
    {
        var renderer = new AvaloniaRenderer();

        var result = renderer.Render(Parse("one\n\ntwo"));

        Assert.Same(renderer.RootPanel, result);
        Assert.Equal(2, renderer.RootPanel.Children.Count);
    }

    [AvaloniaFact]
    public void IsProvisional_is_false_when_not_streaming()
    {
        var renderer = new AvaloniaRenderer { ProvisionalStart = 0 };
        var document = Parse("para");

        Assert.False(renderer.IsStreaming);
        Assert.False(renderer.IsProvisional(document[0]));
    }

    [AvaloniaFact]
    public void IsProvisional_is_true_for_the_tail_block_when_streaming()
    {
        var document = Parse("para");
        var renderer = new AvaloniaRenderer { IsStreaming = true, ProvisionalStart = document[0].Span.Start };

        Assert.True(renderer.IsProvisional(document[0]));
    }

    [AvaloniaFact]
    public void IsProvisional_is_false_for_a_block_before_the_tail_when_streaming()
    {
        var document = Parse("para\n\nnext");
        var renderer = new AvaloniaRenderer { IsStreaming = true, ProvisionalStart = document[1].Span.Start };

        Assert.False(renderer.IsProvisional(document[0]));
        Assert.True(renderer.IsProvisional(document[1]));
    }

    [AvaloniaFact]
    public void IsProvisional_is_true_for_a_one_character_tail_block()
    {
        var document = Parse("para\n\nx");
        var renderer = new AvaloniaRenderer { IsStreaming = true, ProvisionalStart = document[1].Span.Start };

        Assert.Equal(document[1].Span.Start, document[1].Span.End);
        Assert.True(renderer.IsProvisional(document[1]));
        Assert.False(renderer.IsProvisional(document[0]));
    }

    [AvaloniaFact]
    public void IsProvisional_resolves_an_inline_through_its_parent_block()
    {
        var document = Parse("**a**");
        var renderer = new AvaloniaRenderer { IsStreaming = true, ProvisionalStart = document[0].Span.Start };
        var paragraph = Assert.IsType<ParagraphBlock>(document[0]);
        var emphasis = Assert.IsType<EmphasisInline>(paragraph.Inline!.FirstChild);
        var literal = Assert.IsType<LiteralInline>(emphasis.FirstChild);

        Assert.True(renderer.IsProvisional(literal));
        Assert.True(renderer.IsProvisional(emphasis));
    }

    [AvaloniaFact]
    public void IsProvisional_is_false_for_an_inline_of_a_block_before_the_tail()
    {
        var document = Parse("**a**\n\nnext");
        var renderer = new AvaloniaRenderer { IsStreaming = true, ProvisionalStart = document[1].Span.Start };
        var paragraph = Assert.IsType<ParagraphBlock>(document[0]);
        var emphasis = Assert.IsType<EmphasisInline>(paragraph.Inline!.FirstChild);

        Assert.False(renderer.IsProvisional(emphasis.FirstChild!));
        Assert.False(renderer.IsProvisional(paragraph.Inline));
    }

    [AvaloniaFact]
    public void IsProvisional_is_false_for_an_inline_outside_any_block()
    {
        var renderer = new AvaloniaRenderer { IsStreaming = true, ProvisionalStart = 0 };

        Assert.False(renderer.IsProvisional(new LiteralInline("detached")));
    }

    [AvaloniaFact]
    public void IsProvisional_is_true_for_a_closed_heading_at_the_tail()
    {
        var document = Parse("para\n\n# title");
        var renderer = new AvaloniaRenderer { IsStreaming = true, ProvisionalStart = document[1].Span.Start };

        Assert.False(document[1].IsOpen);
        Assert.True(renderer.IsProvisional(document[1]));
        Assert.False(renderer.IsProvisional(document[0]));
    }

    [AvaloniaFact]
    public void IsProvisional_is_true_for_a_list_whose_last_item_is_the_tail_but_not_for_its_first_item()
    {
        var document = Parse("- a\n- b");
        var list = Assert.IsType<ListBlock>(document[0]);
        var renderer = new AvaloniaRenderer { IsStreaming = true, ProvisionalStart = list.LastChild!.Span.Start };

        Assert.True(renderer.IsProvisional(list));
        Assert.False(renderer.IsProvisional(Assert.IsType<ListItemBlock>(list[0]).LastChild!));
        Assert.True(renderer.IsProvisional(list.LastChild!));
    }

    [AvaloniaFact]
    public void IsProvisional_is_false_for_everything_when_no_block_is_the_tail()
    {
        var renderer = new AvaloniaRenderer { IsStreaming = true };
        Assert.False(renderer.IsProvisional(Parse("para")[0]));
    }
}
