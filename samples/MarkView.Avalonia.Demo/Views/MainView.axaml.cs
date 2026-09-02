using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace MarkView.Avalonia.Demo.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    private void OnGoBackClicked(object? sender, RoutedEventArgs e) =>
        ((MainViewModel)DataContext!).GoBack();

    private void OnGoForwardClicked(object? sender, RoutedEventArgs e) =>
        ((MainViewModel)DataContext!).GoForward();

    private async void OnOpenFileClicked(object? sender, RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
            return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
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

    private void OnOutlineSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems is [OutlineRow row])
        {
            MarkdownView.ScrollToAnchor(row.Entry.Slug);
            OutlineToggle.IsChecked = false;
        }

        // Clear selection so re-clicking the same heading still raises this handler.
        OutlineList.SelectedItem = null;
    }
}
