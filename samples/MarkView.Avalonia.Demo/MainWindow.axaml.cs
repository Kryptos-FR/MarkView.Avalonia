using System.Diagnostics;

using Avalonia.Controls;
using Markdig;

namespace MarkView.Avalonia.Demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Combine all opt-in Markdig extensions into one pipeline
        MarkdownView.Pipeline = new MarkdownPipelineBuilder()
            .UseSupportedExtensions()
            .UseFootnotes()
            .UseAlertBlocks()
            .UseAbbreviations()
            .UseFigures()
            .UseMediaLinks()
            .Build();

        MarkdownView.UseTextMateHighlighting()
                    .UseSvg()
                    .UseMermaid();

        DataContext = new MainViewModel();

        MarkdownView.LinkClicked += OnLinkClicked;
    }

    private void OnLinkClicked(object? sender, MarkView.Avalonia.Rendering.LinkClickedEventArgs e)
    {
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
