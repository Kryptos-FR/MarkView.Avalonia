// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

using Xunit;

namespace MarkView.Avalonia.Tests.Accessibility;

/// <summary>
/// <see cref="MarkdownViewer.TextScale"/> and <see cref="MarkdownViewer.FollowSystemTextScale"/>: every font size of
/// the document scales, whoever set it, and nothing is scaled twice.
/// </summary>
public sealed class TextScaleTests : IDisposable
{
    private const string Document = "# Title `code`\n\nText with `code`.\n\n> [!NOTE]\n> Body";

    public TextScaleTests() => SystemTextScale.Reset();

    public void Dispose()
    {
        SystemTextScale.Reset();
        SystemTextScale.Detect = () => 1;
    }

    private sealed record Fixture(Window Window, MarkdownViewer Viewer) : IDisposable
    {
        public TextBlock Heading => All<TextBlock>().Single(t => t.Classes.Contains("markdown-h1"));

        public MarkdownSelectableTextBlock Paragraph => All<MarkdownSelectableTextBlock>().First(t => t.Classes.Contains("markdown-paragraph"));

        public TextBlock AlertHeader => All<TextBlock>().Single(t => t.Classes.Contains("markdown-alert-header"));

        public static Run Code(TextBlock block) => block.Inlines!.OfType<Run>().Single(r => r.Classes.Contains("markdown-code-inline"));

        public static Run Text(TextBlock block) => block.Inlines!.OfType<Run>().First(r => !r.Classes.Contains("markdown-code-inline"));

        public IEnumerable<T> All<T>() where T : Visual => Viewer.GetVisualDescendants().OfType<T>();

        public void Dispose() => Window.Close();
    }

    private static Style FontSizeStyle(Func<Selector?, Selector> selector, double size) =>
        new(selector) { Setters = { new Setter(TextElement.FontSizeProperty, size) } };

    // Styles built in code, like a theme's, so the test does not depend on a theme file.
    private static Fixture Show(string markdown = Document, Action<MarkdownViewer>? configure = null)
    {
        var viewer = new MarkdownViewer { FontSize = 14 };
        viewer.Styles.Add(FontSizeStyle(s => s.Is<TextBlock>().Class("markdown-h1"), 28));
        viewer.Styles.Add(FontSizeStyle(s => s.OfType<Run>().Class("markdown-code-inline"), 13));
        viewer.Styles.Add(FontSizeStyle(s => s.Is<TextBlock>().Class("markdown-alert-header"), 12));
        configure?.Invoke(viewer);
        viewer.Markdown = markdown;
        var window = new Window { Width = 800, Height = 600, Content = viewer };
        window.Show();
        return new Fixture(window, viewer);
    }

    [AvaloniaFact]
    public void Font_sizes_are_unchanged_at_scale_one()
    {
        using var f = Show();

        Assert.Equal(1, f.Viewer.EffectiveTextScale);
        Assert.Equal(28, f.Heading.FontSize);
        Assert.Equal(14, f.Paragraph.FontSize);
        Assert.Equal(13, Fixture.Code(f.Paragraph).FontSize);
        Assert.False(((Grid)f.Viewer.Content!).IsSet(TextElement.FontSizeProperty));
    }

    [AvaloniaTheory]
    [InlineData(2.0)]
    [InlineData(0.5)]
    public void Every_font_size_of_the_document_scales_once(double scale)
    {
        using var f = Show(configure: v => v.TextScale = scale);

        Assert.Equal(scale, f.Viewer.EffectiveTextScale);
        // Set by a style on a block, on an inline, and inherited from the viewer.
        Assert.Equal(28 * scale, f.Heading.FontSize);
        Assert.Equal(13 * scale, Fixture.Code(f.Heading).FontSize);
        Assert.Equal(13 * scale, Fixture.Code(f.Paragraph).FontSize);
        Assert.Equal(14 * scale, f.Paragraph.FontSize);
        Assert.Equal(12 * scale, f.AlertHeader.FontSize);
        // Text that inherits a scaled size is not scaled again.
        Assert.Equal(28 * scale, Fixture.Text(f.Heading).FontSize);
        Assert.Equal(14 * scale, Fixture.Text(f.Paragraph).FontSize);
        Assert.False(f.Paragraph.IsSet(TextElement.FontSizeProperty));
        // The viewer itself keeps its size: only the document scales.
        Assert.Equal(14, f.Viewer.FontSize);
    }

    [AvaloniaFact]
    public void Changing_the_scale_rescales_the_document_in_place()
    {
        using var f = Show();
        var heading = f.Heading;

        f.Viewer.TextScale = 1.5;
        Assert.Same(heading, f.Heading);
        Assert.Equal(42, heading.FontSize);
        Assert.Equal(21, f.Paragraph.FontSize);
        Assert.Equal(19.5, Fixture.Code(f.Paragraph).FontSize);

        f.Viewer.TextScale = 1;
        Assert.Equal(28, heading.FontSize);
        Assert.Equal(14, f.Paragraph.FontSize);
        Assert.Equal(13, Fixture.Code(f.Paragraph).FontSize);
        Assert.False(((Grid)f.Viewer.Content!).IsSet(TextElement.FontSizeProperty));
    }

    [AvaloniaFact]
    public void A_new_document_keeps_the_scale()
    {
        using var f = Show(configure: v => v.TextScale = 2);

        f.Viewer.Markdown = "# Other";

        Assert.Equal(56, f.Heading.FontSize);
    }

    [AvaloniaFact]
    public void The_viewers_font_size_is_the_scaled_base_of_the_text()
    {
        using var f = Show(configure: v => v.TextScale = 2);

        f.Viewer.FontSize = 16;

        Assert.Equal(32, f.Paragraph.FontSize);
        Assert.Equal(56, f.Heading.FontSize);
    }

    [AvaloniaFact]
    public void Controls_embedded_in_the_text_scale_with_it()
    {
        using var f = Show("> [!NOTE]-\n> Body", v =>
        {
            v.Pipeline = new Markdig.MarkdownPipelineBuilder().UseExtendedAlertBlocks().Build();
            v.TextScale = 2;
        });

        var chevron = f.All<AlertChevron>().Single();
        Assert.Equal(24, chevron.GetValue(TextElement.FontSizeProperty));
    }

    [AvaloniaFact]
    public void Text_outside_a_viewer_is_never_scaled()
    {
        using var f = Show(configure: v => v.TextScale = 2);
        var outside = new TextBlock { FontSize = 20 };
        f.Window.Content = new StackPanel { Children = { outside } };

        Assert.Equal(20, outside.FontSize);
    }

    [AvaloniaFact]
    public void The_system_text_scale_multiplies_the_viewers_own()
    {
        SystemTextScale.Factor = 1.5;
        using var f = Show(configure: v => v.TextScale = 2);

        Assert.Equal(3, f.Viewer.EffectiveTextScale);
        Assert.Equal(84, f.Heading.FontSize);

        f.Viewer.FollowSystemTextScale = false;
        Assert.Equal(2, f.Viewer.EffectiveTextScale);
        Assert.Equal(56, f.Heading.FontSize);
    }

    [AvaloniaFact]
    public void A_change_of_the_system_text_scale_updates_shown_viewers()
    {
        using var f = Show();
        var changes = 0;
        SystemTextScale.Changed += OnChanged;
        try
        {
            SystemTextScale.Factor = 1.25;
            SystemTextScale.Factor = 1.25;

            Assert.Equal(1, changes);
            Assert.Equal(1.25, f.Viewer.EffectiveTextScale);
            Assert.Equal(35, f.Heading.FontSize);
        }
        finally
        {
            SystemTextScale.Changed -= OnChanged;
        }

        void OnChanged(object? sender, EventArgs e) => changes++;
    }

    [AvaloniaFact]
    public void A_removed_viewer_stops_following_the_system_text_scale()
    {
        using var f = Show();
        f.Window.Content = null;

        SystemTextScale.Factor = 2;

        Assert.Equal(1, f.Viewer.EffectiveTextScale);
    }

    [AvaloniaFact]
    public void Activating_the_window_reads_the_system_text_scale_again()
    {
        var detected = 1.0;
        SystemTextScale.Detect = () => detected;
        using var f = Show();
        Assert.Equal(1, f.Viewer.EffectiveTextScale);

        detected = 1.75;
        f.Window.Activate();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1.75, SystemTextScale.Factor);
        Assert.Equal(1.75, f.Viewer.EffectiveTextScale);
    }

    [AvaloniaFact]
    public void A_factor_set_by_the_application_wins_over_the_platform()
    {
        SystemTextScale.Detect = () => 1.5;
        Assert.Equal(1.5, SystemTextScale.Factor);

        SystemTextScale.Factor = 1.2;
        SystemTextScale.Refresh();

        Assert.Equal(1.2, SystemTextScale.Factor);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void The_system_text_scale_must_be_a_positive_number(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SystemTextScale.Factor = value);
    }

    [AvaloniaTheory]
    [InlineData(0.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void The_viewers_text_scale_must_be_a_positive_number(double value)
    {
        Assert.Throws<ArgumentException>(() => new MarkdownViewer().TextScale = value);
    }
}
