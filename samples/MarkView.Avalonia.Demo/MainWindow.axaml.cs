using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace MarkView.Avalonia.Demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void OnGoBackClicked(object? sender, RoutedEventArgs e) =>
        ((MainViewModel)DataContext!).GoBack();

    private void OnGoForwardClicked(object? sender, RoutedEventArgs e) =>
        ((MainViewModel)DataContext!).GoForward();

    private async void OnOpenFileClicked(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Markdown file",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Markdown files") { Patterns = ["*.md", "*.markdown"] },
                new FilePickerFileType("All files")      { Patterns = ["*"] },
            ],
        });

        if (files is [var file])
        {
            var path = file.TryGetLocalPath();
            if (path is not null)
                ((MainViewModel)DataContext!).LoadFile(path);
        }
    }

    private void OnLoadUrlClicked(object? sender, RoutedEventArgs e) =>
        ((MainViewModel)DataContext!).LoadFromUrl();

    private void OnUrlTextKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            ((MainViewModel)DataContext!).LoadFromUrl();
    }
}
