// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>Links to Markdown documents opened in the viewer, and Back and Forward.</summary>
public class MarkdownViewerNavigationTests
{
    private static readonly string Filler = string.Join("\n\n", Enumerable.Range(0, 40).Select(i => $"Filler {i}"));

    private static readonly Dictionary<string, string> Documents = new()
    {
        ["app://docs/guide/a.md"] = $"""
            # A

            [B](b.md) [B at its end](b.md#end) [Other](../other/c.markdown) [Elsewhere](https://example.com/x.md) [Picture](pic.png) [Missing](missing.md) [Own end](a.md#a-end) [Self](a.md)

            {Filler}

            ## A end

            [Top](#a) [Nowhere](#nowhere)
            """,
        ["app://docs/guide/b.md"] = $"""
            # B

            {Filler}

            ## End
            """,
        ["app://docs/other/c.markdown"] = "# C",
    };

    private sealed class MemoryResolver : IResourceResolver
    {
        public List<Uri> Requests { get; } = [];

        public Task<Stream?> OpenAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            Requests.Add(uri);
            return Documents.TryGetValue(uri.AbsoluteUri, out var text)
                ? Task.FromResult<Stream?>(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)))
                : Task.FromException<Stream?>(new FileNotFoundException(null, uri.AbsoluteUri));
        }
    }

    private sealed class Fixture
    {
        public required Window Window { get; init; }
        public required MarkdownViewer Viewer { get; init; }
        public required ScrollViewer ScrollViewer { get; init; }
        public required MemoryResolver Resolver { get; init; }
        public List<Uri> Launched { get; } = [];
        public List<string> Clicked { get; } = [];

        public double Offset => ScrollViewer.Offset.Y;

        public string Title => ((MarkdownSelectableTextBlock)DocumentPanel.Children[0]).Inlines!.Text!;

        private StackPanel DocumentPanel => (StackPanel)((Grid)Viewer.Content!).Children[0];

        public void Click(string text)
        {
            var link = Viewer.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>()
                .SelectMany(t => t.Inlines!.OfType<MarkdownHyperlink>())
                .Single(l => l.Inlines.Text == text);
            Viewer.ActivateLink(link);
            Settle();
        }

        public void Settle()
        {
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        public void Scroll(double y)
        {
            ScrollViewer.Offset = new Vector(0, y);
            Window.UpdateLayout();
        }
    }

    private static Fixture Show(string? source = "app://docs/guide/a.md", bool navigation = true)
    {
        var resolver = new MemoryResolver();
        var viewer = new MarkdownViewer { ResourceResolver = resolver, IsNavigationEnabled = navigation };
        viewer.Template = ScrollingViewerTemplate.Create();
        var window = new Window { Width = 400, Height = 200, Content = viewer };
        window.Show();
        var fixture = new Fixture
        {
            Window = window,
            Viewer = viewer,
            ScrollViewer = viewer.GetVisualDescendants().OfType<ScrollViewer>().Single(),
            Resolver = resolver,
        };
        viewer.LaunchUri = uri =>
        {
            fixture.Launched.Add(uri);
            return Task.CompletedTask;
        };
        viewer.LinkClicked += (_, e) => fixture.Clicked.Add(e.Url);
        if (source is not null)
            viewer.Source = new Uri(source);
        fixture.Settle();
        return fixture;
    }

    // ── Following links ───────────────────────────────────────────────────────

    [AvaloniaFact]
    public void A_link_to_a_markdown_document_of_the_same_origin_opens_in_the_viewer()
    {
        var f = Show();

        f.Click("B");

        Assert.Equal("B", f.Title);
        Assert.Equal(new Uri("app://docs/guide/b.md"), f.Viewer.Source);
        Assert.Equal(["app://docs/guide/b.md"], f.Clicked);
        Assert.Empty(f.Launched);
        f.Window.Close();
    }

    [AvaloniaFact]
    public void A_markdown_extension_and_another_folder_of_the_same_origin_open_in_the_viewer_too()
    {
        var f = Show();

        f.Click("Other");

        Assert.Equal("C", f.Title);
        f.Window.Close();
    }

    [AvaloniaFact]
    public void A_link_with_a_fragment_opens_the_document_at_its_heading()
    {
        var f = Show();

        f.Click("B at its end");

        Assert.Equal("B", f.Title);
        Assert.True(f.Offset > 0);
        f.Window.Close();
    }

    [AvaloniaFact]
    public void A_link_to_a_heading_of_the_current_document_scrolls_without_loading_it_again()
    {
        var f = Show();
        var requests = f.Resolver.Requests.Count;

        f.Click("Own end");

        Assert.True(f.Offset > 0);
        Assert.Equal(requests, f.Resolver.Requests.Count);
        Assert.True(f.Viewer.CanGoBack);
        f.Window.Close();
    }

    [AvaloniaFact]
    public void A_link_to_the_current_document_scrolls_to_its_top()
    {
        var f = Show();
        f.Scroll(300);

        f.Click("Self");

        Assert.Equal(0, f.Offset);
        f.Viewer.GoBack();
        f.Settle();
        Assert.Equal(300, f.Offset);
        f.Window.Close();
    }

    [AvaloniaFact]
    public void A_markdown_document_of_another_origin_is_launched()
    {
        var f = Show();

        f.Click("Elsewhere");

        Assert.Equal("A", f.Title);
        Assert.Equal([new Uri("https://example.com/x.md")], f.Launched);
        f.Window.Close();
    }

    [AvaloniaFact]
    public void A_link_to_something_other_than_markdown_is_not_opened_in_the_viewer()
    {
        var f = Show();

        f.Click("Picture");

        Assert.Equal("A", f.Title);
        Assert.Equal(new Uri("app://docs/guide/a.md"), f.Viewer.Source);
        f.Window.Close();
    }

    [AvaloniaFact]
    public void A_handled_click_is_left_to_its_handler()
    {
        var f = Show();
        f.Viewer.LinkClicked += (_, e) => e.Handled = true;

        f.Click("B");

        Assert.Equal("A", f.Title);
        f.Window.Close();
    }

    [AvaloniaFact]
    public void With_navigation_disabled_links_to_documents_are_not_opened_in_the_viewer()
    {
        var f = Show(navigation: false);

        f.Click("B");

        Assert.Equal("A", f.Title);
        Assert.Empty(f.Launched); // app:// is not a scheme the viewer launches
        f.Window.Close();
    }

    [AvaloniaFact]
    public void A_markdown_text_without_a_base_uri_opens_no_relative_document()
    {
        var f = Show(source: null);
        f.Viewer.Markdown = "[B](b.md)";
        f.Settle();

        f.Click("B");

        Assert.Null(f.Viewer.Source);
        f.Window.Close();
    }

    [AvaloniaFact]
    public void A_markdown_text_with_a_base_uri_opens_its_documents_in_the_viewer()
    {
        var f = Show(source: null);
        f.Viewer.BaseUri = new Uri("app://docs/guide/");
        f.Viewer.Markdown = "[B](b.md)";
        f.Settle();

        f.Click("B");

        Assert.Equal("B", f.Title);
        f.Viewer.GoBack();
        f.Settle();
        Assert.Null(f.Viewer.Source);
        Assert.Equal("B", Assert.IsType<MarkdownHyperlink>(((MarkdownSelectableTextBlock)((StackPanel)((Grid)f.Viewer.Content!).Children[0]).Children[0]).Inlines![0]).Inlines.Text);
        f.Window.Close();
    }

    // ── Back and Forward ──────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Back_returns_to_the_previous_document_where_it_was_left_and_Forward_comes_back()
    {
        var f = Show();
        Assert.False(f.Viewer.CanGoBack);
        f.Scroll(250);
        f.Click("B at its end");
        var bOffset = f.Offset;

        f.Viewer.GoBack();
        f.Settle();

        Assert.Equal("A", f.Title);
        Assert.Equal(250, f.Offset);
        Assert.False(f.Viewer.CanGoBack);
        Assert.True(f.Viewer.CanGoForward);

        f.Viewer.GoForward();
        f.Settle();

        Assert.Equal("B", f.Title);
        Assert.Equal(bOffset, f.Offset);
        Assert.True(f.Viewer.CanGoBack);
        Assert.False(f.Viewer.CanGoForward);
        f.Window.Close();
    }

    [AvaloniaFact]
    public void Back_after_a_heading_link_returns_to_where_the_reader_was()
    {
        var f = Show();
        f.Click("Own end");
        var end = f.Offset;
        f.Click("Top");
        Assert.Equal(0, f.Offset);

        f.Viewer.GoBack();
        f.Settle();
        Assert.Equal(end, f.Offset);

        f.Viewer.GoBack();
        f.Settle();
        Assert.Equal(0, f.Offset);
        Assert.False(f.Viewer.CanGoBack);
        f.Window.Close();
    }

    [AvaloniaFact]
    public void A_link_to_a_heading_the_document_does_not_have_is_not_recorded()
    {
        var f = Show();
        f.Click("Own end");
        f.Click("Nowhere");

        f.Viewer.GoBack();
        f.Settle();

        Assert.False(f.Viewer.CanGoBack);
        f.Window.Close();
    }

    [AvaloniaFact]
    public void Following_a_link_after_Back_drops_the_places_Forward_would_go_to()
    {
        var f = Show();
        f.Click("B");
        f.Viewer.GoBack();
        f.Settle();

        f.Click("Other");

        Assert.False(f.Viewer.CanGoForward);
        f.Viewer.GoBack();
        f.Settle();
        Assert.Equal("A", f.Title);
        f.Window.Close();
    }

    [AvaloniaFact]
    public void A_document_that_fails_to_load_is_not_recorded()
    {
        var f = Show();

        f.Click("Missing");

        Assert.Equal("A", f.Title);
        Assert.False(f.Viewer.CanGoBack);
        f.Window.Close();
    }

    [AvaloniaFact]
    public void A_source_set_by_the_application_is_recorded()
    {
        var f = Show();

        f.Viewer.Source = new Uri("app://docs/guide/b.md");
        f.Settle();
        f.Viewer.GoBack();
        f.Settle();

        Assert.Equal("A", f.Title);
        Assert.Equal(new Uri("app://docs/guide/a.md"), f.Viewer.Source);
        f.Window.Close();
    }

    [AvaloniaFact]
    public void A_document_loaded_again_is_recorded_once()
    {
        var f = Show();

        f.Viewer.ResourceResolver = new MemoryResolver();
        f.Settle();

        Assert.False(f.Viewer.CanGoBack);
        f.Window.Close();
    }

    [AvaloniaFact]
    public void Clearing_the_source_starts_the_history_again()
    {
        var f = Show();
        f.Click("B");

        f.Viewer.Source = null;

        Assert.False(f.Viewer.CanGoBack);
        Assert.False(f.Viewer.CanGoForward);
        f.Window.Close();
    }

    [AvaloniaFact]
    public void Another_markdown_text_starts_the_history_again()
    {
        var f = Show(source: null);
        f.Viewer.Markdown = $"[Down](#down)\n\n{Filler}\n\n## Down";
        f.Settle();
        f.Click("Down");
        Assert.True(f.Viewer.CanGoBack);

        f.Viewer.Markdown = "Other text";

        Assert.False(f.Viewer.CanGoBack);
        f.Window.Close();
    }

    [AvaloniaFact]
    public void CanGoBack_and_CanGoForward_notify_their_changes()
    {
        var f = Show();
        var changes = new List<string>();
        f.Viewer.PropertyChanged += (_, e) =>
        {
            if (e.Property == MarkdownViewer.CanGoBackProperty || e.Property == MarkdownViewer.CanGoForwardProperty)
                changes.Add($"{e.Property.Name}={e.NewValue}");
        };

        f.Click("B");
        f.Viewer.GoBack();
        f.Settle();

        Assert.Equal(["CanGoBack=True", "CanGoBack=False", "CanGoForward=True"], changes);
        f.Window.Close();
    }

    [AvaloniaFact]
    public void Back_and_Forward_do_nothing_without_somewhere_to_go()
    {
        var f = Show();

        f.Viewer.GoBack();
        f.Viewer.GoForward();
        f.Settle();

        Assert.Equal("A", f.Title);
        f.Window.Close();
    }

    // ── Mouse and keyboard ────────────────────────────────────────────────────

    [AvaloniaTheory]
    [InlineData(Key.Left, RawInputModifiers.Alt, Key.Right, RawInputModifiers.Alt)]
    [InlineData(Key.OemOpenBrackets, RawInputModifiers.Meta, Key.OemCloseBrackets, RawInputModifiers.Meta)]
    [InlineData(Key.BrowserBack, RawInputModifiers.None, Key.BrowserForward, RawInputModifiers.None)]
    public void Keys_go_back_and_forward(Key back, RawInputModifiers backModifiers, Key forward, RawInputModifiers forwardModifiers)
    {
        var f = Show();
        f.Click("B");
        f.Viewer.Focus();

        f.Window.KeyPress(back, backModifiers, PhysicalKey.None, null);
        f.Settle();
        Assert.Equal("A", f.Title);

        f.Window.KeyPress(forward, forwardModifiers, PhysicalKey.None, null);
        f.Settle();
        Assert.Equal("B", f.Title);
        f.Window.Close();
    }

    [AvaloniaFact]
    public void The_mouse_back_and_forward_buttons_go_back_and_forward()
    {
        var f = Show();
        f.Click("B");
        var point = new Point(100, 100);

        f.Window.MouseDown(point, MouseButton.XButton1);
        f.Window.MouseUp(point, MouseButton.XButton1);
        f.Settle();
        Assert.Equal("A", f.Title);

        f.Window.MouseDown(point, MouseButton.XButton2);
        f.Window.MouseUp(point, MouseButton.XButton2);
        f.Settle();
        Assert.Equal("B", f.Title);
        f.Window.Close();
    }

    [AvaloniaFact]
    public void With_navigation_disabled_the_keys_do_nothing()
    {
        var f = Show(navigation: false);
        f.Viewer.Source = new Uri("app://docs/guide/b.md");
        f.Settle();
        f.Viewer.Focus();

        f.Window.KeyPress(Key.Left, RawInputModifiers.Alt, PhysicalKey.None, null);
        f.Settle();

        Assert.Equal("B", f.Title);
        // Back itself still works.
        f.Viewer.GoBack();
        f.Settle();
        Assert.Equal("A", f.Title);
        f.Window.Close();
    }
}
