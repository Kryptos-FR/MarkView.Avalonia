using System.Diagnostics;

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
            .UseFootnotes()
            .UseAlertBlocks()
            .UseAbbreviations()
            .UseFigures()
            .UseMediaLinks()
            .Build();

        // Global extensions — applies to every MarkdownViewer in the app
        MarkdownViewerDefaults.Extensions.AddTextMateHighlighting();
        MarkdownViewerDefaults.Extensions.AddSvg();
        MarkdownViewerDefaults.Extensions.AddMermaid();

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
            // If the link resolves to a local .md file, open it in the viewer instead of the browser.
            if (Uri.TryCreate(e.Url, UriKind.Absolute, out var uri)
                && uri.IsFile
                && (uri.LocalPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                    || uri.LocalPath.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase)))
            {
                var path = uri.LocalPath;
                if (File.Exists(path))
                {
                    ((MainViewModel)sender.DataContext!).LoadFile(path);
                    return;
                }
            }

            try
            {
                Process.Start(new ProcessStartInfo(e.Url) { UseShellExecute = true });
            }
            catch
            {
                // Ignore failures to open browser
            }
        }
    }
}
