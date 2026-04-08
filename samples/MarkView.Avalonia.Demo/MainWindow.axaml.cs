using System.Diagnostics;
using Avalonia.Controls;

namespace MarkView.Avalonia.Demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
