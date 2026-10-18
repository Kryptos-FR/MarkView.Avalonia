using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace MarkView.Avalonia.Demo.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();

        // Ctrl+F (Cmd+F on macOS) from anywhere in the view, before the focused control sees it.
        AddHandler(KeyDownEvent, OnViewKeyDown, RoutingStrategies.Tunnel);
        MarkdownView.PropertyChanged += (_, e) =>
        {
            if (e.Property == MarkdownViewer.FindMatchCountProperty || e.Property == MarkdownViewer.CurrentFindMatchIndexProperty)
                UpdateFindStatus();
        };
    }

    // ── Find in document ─────────────────────────────────────────────────────

    private void OnViewKeyDown(object? sender, KeyEventArgs e)
    {
        var commandModifiers = Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;
        if (e.Key == Key.F && e.KeyModifiers == commandModifiers)
        {
            FindBox.Focus();
            FindBox.SelectAll();
            e.Handled = true;
        }
    }

    private void OnFindTextChanged(object? sender, TextChangedEventArgs e)
    {
        // Jump to the first match as the text is typed, as browsers do.
        MarkdownView.Find(FindBox.Text ?? string.Empty);
        MarkdownView.FindNext();
        UpdateFindStatus();
    }

    private void OnFindBoxKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter when e.KeyModifiers == KeyModifiers.Shift:
                MarkdownView.FindPrevious();
                e.Handled = true;
                break;
            case Key.Enter:
                MarkdownView.FindNext();
                e.Handled = true;
                break;
            case Key.Escape:
                FindBox.Text = string.Empty;
                MarkdownView.Focus();
                e.Handled = true;
                break;
        }
    }

    private void OnFindPreviousClicked(object? sender, RoutedEventArgs e) => MarkdownView.FindPrevious();

    private void OnFindNextClicked(object? sender, RoutedEventArgs e) => MarkdownView.FindNext();

    private void UpdateFindStatus()
    {
        var count = MarkdownView.FindMatchCount;
        FindPreviousButton.IsEnabled = FindNextButton.IsEnabled = count > 0;
        FindStatus.Text = string.IsNullOrEmpty(FindBox.Text) ? string.Empty
            : count == 0 ? "No results"
            : MarkdownView.CurrentFindMatchIndex < 0 ? $"{count} matches"
            : $"{MarkdownView.CurrentFindMatchIndex + 1} of {count}";
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
                new FilePickerFileType("Markdown files")
                {
                    Patterns = ["*.md", "*.markdown"],
                    MimeTypes = ["text/markdown", "text/x-markdown"],
                },
                FilePickerFileTypes.All,
            ],
        });

        if (files is [var file])
        {
            var path = file.TryGetLocalPath();
            if (path is not null)
            {
                ((MainViewModel)DataContext!).LoadFile(path);
            }
            else
            {
                // No local filesystem path (e.g. browser/WASM storage handles) — read the content directly.
                using var stream = await file.OpenReadAsync();
                using var reader = new StreamReader(stream);
                var markdown = await reader.ReadToEndAsync();
                ((MainViewModel)DataContext!).LoadMarkdownContent(markdown);
            }
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
