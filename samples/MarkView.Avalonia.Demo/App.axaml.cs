using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using Markdig;

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
            .UseSupportedExtensions()
            .UseAbbreviations()
            .UseAlertBlocks()
            .UseCitations()
            .UseFigures()
            .UseFootnotes()
            .UseHardlineBreaks()
            .UseMediaLinks()
            .UseMathematics()
            .Build();

        // Global extensions — applies to every MarkdownViewer in the app
        MarkdownViewerDefaults.Extensions.AddTextMateHighlighting();
        MarkdownViewerDefaults.Extensions.AddSvg();
        MarkdownViewerDefaults.Extensions.AddMermaid();
        MarkdownViewerDefaults.Extensions.AddMath();

        // Global link handler — handles external links for every MarkdownViewer in the app
        MarkdownViewer.LinkClickedEvent.AddClassHandler<MarkdownViewer>(OnLinkClicked);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
        return;

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
