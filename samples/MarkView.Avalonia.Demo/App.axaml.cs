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
        MarkdownViewer.LinkClickedEvent.AddClassHandler<MarkdownViewer>((_, e) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Url) { UseShellExecute = true });
            }
            catch
            {
                // Ignore failures to open browser
            }
        });

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
