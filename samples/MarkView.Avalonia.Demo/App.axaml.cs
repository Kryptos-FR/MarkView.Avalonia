using System.Globalization;
using System.Text.RegularExpressions;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

using Markdig;
using Markdig.Extensions.Tables;
using MarkView.Avalonia.Demo.MermaidStyles;
using MarkView.Avalonia.Demo.Views;

namespace MarkView.Avalonia.Demo;

public partial class App : Application
{
    [GeneratedRegex(@"(?<![\w&])#(\d+)\b")]
    private static partial Regex IssueReference();

    [GeneratedRegex(@"(?<![\w@])@(\w(?:[\w-]*\w)?)")]
    private static partial Regex Mention();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Global pipeline — applies to every MarkdownViewer in the app
        MarkdownViewerDefaults.Pipeline = new MarkdownPipelineBuilder()
            .UseSupportedExtensions(new MarkdownExtensionsOptions { PipeTable = new PipeTableOptions { InferColumnWidthsFromSeparator = true } })
            .UseAbbreviations()
            .UseExtendedAlertBlocks()
            .UseHtmlDetails()
            .UseCitations()
            .UseFigures()
            .UseFootnotes()
            .UseHardlineBreaks()
            .UseMediaLinks()
            .UseMathematics()
            .UseMathBracketDelimiters()
            .UseDefinitionLists()
            .UseCustomContainers()
            .UseFooters()
            .UseSmartyPants()
            .UseListExtras()
            // Markdig only parses attributes for the extensions set up before it: keep it last.
            .UseGenericAttributes()
            .Build();

        // Global extensions — applies to every MarkdownViewer in the app
        MarkdownViewerDefaults.Extensions.AddTextMateHighlighting();
        MarkdownViewerDefaults.Extensions.AddSvg();
        MarkdownViewerDefaults.Extensions.AddMermaid();
        MarkdownViewerDefaults.Extensions.AddMath();

        // Demo only, not part of the library: a styles button (a palette icon) on every Mermaid diagram that compares
        // the diagram in each MermaidStyle. It wraps the Mermaid renderer, so it comes after AddMermaid().
        MarkdownViewerDefaults.Extensions.Add(new MermaidStylesExtension());

        // Global fence renderer — every ```palette block renders as a row of colour swatches,
        // sized by an optional argument after the language (```palette size=24)
        MarkdownViewerDefaults.Extensions.AddFenceRenderer("palette", (code, info) => CreatePalette(code, info));

        // Global container renderer — every :::spoiler container renders as an expander around its
        // markdown, titled by the text after the name (:::spoiler The answer)
        MarkdownViewerDefaults.Extensions.AddContainerRenderer("spoiler", (content, title) => new Expander
        {
            Header = title.Length > 0 ? title : "Spoiler",
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });

        // Global patterns — #123 links to the repository's issue, and @name renders as a mention
        MarkdownViewerDefaults.Extensions.AddLinkPattern(IssueReference(),
            match => $"https://github.com/Kryptos-FR/MarkView.Avalonia/issues/{match.Groups[1].Value}");
        MarkdownViewerDefaults.Extensions.AddInlinePattern(Mention(),
            match => new Run(match.Value) { Classes = { "mention" } });

        // Global link handler — handles external links for every MarkdownViewer in the app
        MarkdownViewer.LinkClickedEvent.AddClassHandler<MarkdownViewer>(OnLinkClicked);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
        {
            singleViewFactoryApplicationLifetime.MainViewFactory = () => new MainView
            {
                DataContext = new MainViewModel()
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
        return;

        // Returns null for anything that is not a list of colours, so the block shows as code instead.
        static Control? CreatePalette(string code, string info)
        {
            var size = SwatchSize(info);
            var swatches = new WrapPanel();
            foreach (var name in code.Split([' ', ',', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Color.TryParse(name, out var color))
                    return null;
                var swatch = new Border
                {
                    Width = size,
                    Height = size,
                    Margin = new Thickness(0, 0, size / 6, size / 6),
                    CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(color),
                };
                ToolTip.SetTip(swatch, name);
                swatches.Children.Add(swatch);
            }
            return swatches;
        }

        // Reads "size=N" from the words after the language in the info string ("palette size=24"),
        // keeping the default for a missing or out-of-range value.
        static double SwatchSize(string info)
        {
            foreach (var argument in info.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1))
            {
                if (argument.StartsWith("size=", StringComparison.OrdinalIgnoreCase)
                    && double.TryParse(argument["size=".Length..], NumberStyles.Float, CultureInfo.InvariantCulture, out var size)
                    && size is >= 8 and <= 128)
                    return size;
            }
            return 48;
        }

        static void OnLinkClicked(MarkdownViewer sender, Rendering.LinkClickedEventArgs e)
        {
            // If the link resolves to a local .md file, open it in the viewer instead of the
            // browser. The Uri (fragment included) flows into Source, so MarkdownViewer scrolls
            // to the anchor itself once the new document has rendered.
            if (Uri.TryCreate(e.Url, UriKind.Absolute, out var uri)
                && uri.IsFile
                && (uri.LocalPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                    || uri.LocalPath.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase))
                && File.Exists(uri.LocalPath))
            {
                ((MainViewModel)sender.DataContext!).LoadFile(uri);
                e.Handled = true;
            }

            // Otherwise leave the event unhandled — every rendered link is a HyperlinkButton
            // that opens itself via the platform launcher once its Click handler returns.
        }
    }
}
