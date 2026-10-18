// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text.RegularExpressions;

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;

using Markdig;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;

using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

public class InlinePatternTests
{
    private sealed class LogSink : ILogSink
    {
        public List<(LogEventLevel Level, string Area, object?[] Values)> Events { get; } = [];

        public bool IsEnabled(LogEventLevel level, string area) => true;

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
            Events.Add((level, area, []));

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues) =>
            Events.Add((level, area, propertyValues));
    }

    private static readonly Regex Mention = new(@"(?<!\w)@(\w+)");
    private static readonly Regex Issue = new(@"(?<![\w&])#(\d+)\b");

    private static AvaloniaRenderer Renderer(string markdown, params IMarkViewExtension[] extensions)
    {
        var pipeline = new MarkdownPipelineBuilder().UseGitHubExtensions().Build();
        var document = Markdown.Parse(markdown, pipeline);
        var renderer = new AvaloniaRenderer { BaseUri = new Uri("https://example.com/docs/") };
        foreach (var extension in extensions)
            extension.Register(renderer);
        pipeline.Setup(renderer);
        renderer.Render(document);
        return renderer;
    }

    private static InlineCollection Paragraph(string markdown, params IMarkViewExtension[] extensions) =>
        Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(Renderer(markdown, extensions).RootPanel.Children)).Inlines!;

    private static InlinePatternExtension Bold(Regex pattern) =>
        new(pattern, m => new Run(m.Groups[1].Value) { Classes = { "pattern" } });

    // Each inline as "text" for a plain run and "[text]" for one the pattern created.
    private static string[] Describe(InlineCollection inlines) => inlines.Select(i => i switch
    {
        Run run when run.Classes.Contains("pattern") => $"[{run.Text}]",
        Run run => run.Text ?? string.Empty,
        MarkdownHyperlink link => $"<{link.NavigateUri}|{((Run)link.Inlines.Single()).Text}>",
        Span span => $"span({string.Join(",", Describe(span.Inlines))})",
        _ => i.GetType().Name,
    }).ToArray();

    [AvaloniaFact]
    public void Matches_are_written_as_the_factorys_inlines_between_the_text_around_them()
    {
        var inlines = Paragraph("Ask @bob and @alice now", Bold(Mention));

        Assert.Equal(["Ask ", "[bob]", " and ", "[alice]", " now"], Describe(inlines));
    }

    [AvaloniaFact]
    public void A_match_at_either_end_writes_no_empty_run()
    {
        var inlines = Paragraph("@bob", Bold(Mention));

        Assert.Equal(["[bob]"], Describe(inlines));
    }

    [AvaloniaFact]
    public void Factory_gets_the_match_with_its_groups()
    {
        Match? received = null;

        Paragraph("Hi @bob!", new InlinePatternExtension(Mention, m =>
        {
            received = m;
            return null;
        }));

        Assert.NotNull(received);
        Assert.Equal("@bob", received.Value);
        Assert.Equal("bob", received.Groups[1].Value);
    }

    [AvaloniaFact]
    public void Factory_returning_null_keeps_the_text()
    {
        var inlines = Paragraph("Hi @bob!", new InlinePatternExtension(Mention, _ => null));

        Assert.Equal("Hi @bob!", string.Concat(Describe(inlines)));
    }

    [AvaloniaFact]
    public void Text_without_a_match_is_one_run()
    {
        var inlines = Paragraph("Nothing to see", Bold(Mention));

        Assert.Equal(["Nothing to see"], Describe(inlines));
    }

    [AvaloniaFact]
    public void Matches_inside_formatting_are_written_inside_it()
    {
        var inlines = Paragraph("Ask **@bob** now", Bold(Mention));

        Assert.Equal(["Ask ", "span([bob])", " now"], Describe(inlines));
    }

    [AvaloniaTheory]
    [InlineData("[ask @bob](https://example.com)")]
    [InlineData("![@bob](https://example.com/a.png)")]
    [InlineData("<a href=\"https://example.com\">@bob</a>")]
    [InlineData("`@bob`")]
    public void Links_images_and_code_are_left_alone(string markdown)
    {
        var calls = 0;

        Paragraph(markdown, new InlinePatternExtension(Mention, _ =>
        {
            calls++;
            return null;
        }));

        Assert.Equal(0, calls);
    }

    [AvaloniaFact]
    public void Match_that_starts_first_wins_whatever_the_registration_order()
    {
        var inlines = Paragraph("#1 then @bob", Bold(Mention), Bold(Issue));

        Assert.Equal(["[1]", " then ", "[bob]"], Describe(inlines));
    }

    [AvaloniaFact]
    public void Last_registered_pattern_wins_a_tie_and_matched_text_is_not_searched_again()
    {
        var word = new InlinePatternExtension(new Regex(@"@\w+"), m => new Run("word") { Classes = { "pattern" } });
        var inner = new InlinePatternExtension(new Regex(@"bob"), m => new Run("inner") { Classes = { "pattern" } });

        var inlines = Paragraph("@bob bob", word, Bold(Mention), inner);

        Assert.Equal(["[bob]", " ", "[inner]"], Describe(inlines));
    }

    [AvaloniaFact]
    public void A_later_match_of_a_pattern_is_kept_while_another_pattern_matches_before_it()
    {
        var inlines = Paragraph("@a #1 @b #2 @c", Bold(Mention), Bold(Issue));

        Assert.Equal(["[a]", " ", "[1]", " ", "[b]", " ", "[2]", " ", "[c]"], Describe(inlines));
    }

    [AvaloniaFact]
    public void Overlapping_match_of_another_pattern_is_dropped_and_the_next_one_found()
    {
        var pair = new InlinePatternExtension(new Regex(@"\d \d"), m => new Run(m.Value) { Classes = { "pattern" } });

        var inlines = Paragraph("#1 2 3 4", Bold(Issue), pair);

        Assert.Equal(["[1]", " ", "[2 3]", " 4"], Describe(inlines));
    }

    [AvaloniaFact]
    public void Empty_matches_are_ignored()
    {
        var calls = 0;
        var empty = new InlinePatternExtension(new Regex(@"x*"), m =>
        {
            calls++;
            return new Run(m.Value) { Classes = { "pattern" } };
        });

        var inlines = Paragraph("abxxc", empty);

        Assert.Equal(1, calls);
        Assert.Equal(["ab", "[xx]", "c"], Describe(inlines));
    }

    [AvaloniaFact]
    public void Factory_throwing_keeps_the_text_and_logs_a_warning()
    {
        var sink = new LogSink();
        var previous = Logger.Sink;
        Logger.Sink = sink;
        InlineCollection inlines;
        try
        {
            inlines = Paragraph("Hi @bob and @al", new InlinePatternExtension(Mention, m =>
                m.Value == "@bob" ? throw new InvalidOperationException("boom") : new Run("al") { Classes = { "pattern" } }));
        }
        finally
        {
            Logger.Sink = previous;
        }

        Assert.Equal(["Hi ", "@bob", " and ", "[al]"], Describe(inlines));
        var warning = Assert.Single(sink.Events, e => e.Level == LogEventLevel.Warning);
        Assert.Equal(LogArea.Control, warning.Area);
        Assert.Equal(Mention.ToString(), warning.Values[0]);
        Assert.Equal("@bob", warning.Values[1]);
        Assert.Contains(warning.Values, v => v is InvalidOperationException { Message: "boom" });
    }

    [AvaloniaFact]
    public void Embedded_control_copies_the_matched_text_unless_it_has_its_own_copy_text()
    {
        var own = new Button();
        MarkdownSelection.SetCopyText(own, "own");
        var plain = new Button();

        var inlines = Paragraph("@a @b", new InlinePatternExtension(Mention, m =>
            new InlineUIContainer(m.Value == "@a" ? plain : own)));

        Assert.Equal(2, inlines.OfType<InlineUIContainer>().Count());
        Assert.Equal("@a", MarkdownSelection.GetCopyText(plain));
        Assert.Equal("own", MarkdownSelection.GetCopyText(own));
    }

    [AvaloniaFact]
    public void Link_pattern_writes_a_link_with_the_matched_text()
    {
        var inlines = Paragraph("Fixed in #123.", new LinkPatternExtension(Issue, m => $"https://github.com/o/r/issues/{m.Groups[1].Value}"));

        Assert.Equal(["Fixed in ", "<https://github.com/o/r/issues/123|#123>", "."], Describe(inlines));
        var link = Assert.IsType<MarkdownHyperlink>(inlines[1]);
        Assert.Equal(["markdown-link", "markdown-pattern-link"], link.Classes.Where(c => !c.StartsWith(':')));
    }

    [AvaloniaFact]
    public void Link_pattern_resolves_a_relative_address_against_the_base_uri()
    {
        var inlines = Paragraph("See @bob", new LinkPatternExtension(Mention, m => $"users/{m.Groups[1].Value}.md"));

        Assert.Equal(new Uri("https://example.com/docs/users/bob.md"), Assert.IsType<MarkdownHyperlink>(inlines[1]).NavigateUri);
    }

    [AvaloniaFact]
    public void Link_pattern_returning_null_keeps_the_text()
    {
        var inlines = Paragraph("#1 #2", new LinkPatternExtension(Issue, m => m.Groups[1].Value == "1" ? null : "https://example.com/2"));

        Assert.Equal(["#1", " ", "<https://example.com/2|#2>"], Describe(inlines));
    }

    [AvaloniaFact]
    public void Link_with_an_invalid_address_has_no_uri()
    {
        var inlines = Paragraph("#1", new LinkPatternExtension(Issue, _ => "http://[bad"));

        Assert.Null(Assert.IsType<MarkdownHyperlink>(Assert.Single(inlines)).NavigateUri);
    }

    [AvaloniaFact]
    public void Constructors_reject_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => new InlinePatternExtension(null!, _ => null));
        Assert.Throws<ArgumentNullException>(() => new InlinePatternExtension(Mention, null!));
        Assert.Throws<ArgumentNullException>(() => new LinkPatternExtension(null!, _ => null));
        Assert.Throws<ArgumentNullException>(() => new LinkPatternExtension(Mention, null!));
    }
}
