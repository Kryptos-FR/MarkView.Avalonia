using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

using Markdig;
using Markdig.Extensions.Tables;
using MarkView.Avalonia.Demo.Views;

namespace MarkView.Avalonia.Demo;

public class App : Application
{
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

        // Global fence renderer — every ```palette block renders as a row of colour swatches
        MarkdownViewerDefaults.Extensions.AddFenceRenderer("palette", (code, _) => CreatePalette(code));

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
        static Control? CreatePalette(string code)
        {
            var swatches = new WrapPanel();
            foreach (var name in code.Split([' ', ',', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Color.TryParse(name, out var color))
                    return null;
                var swatch = new Border
                {
                    Width = 48,
                    Height = 48,
                    Margin = new Thickness(0, 0, 8, 8),
                    CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(color),
                };
                ToolTip.SetTip(swatch, name);
                swatches.Children.Add(swatch);
            }
            return swatches;
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
