using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace MarkView.Avalonia.Demo.Views;

public partial class MainView : UserControl
{
    /// <summary>The number of characters each step of the Stream command appends.</summary>
    private const int StreamChunkSize = 40;

    /// <summary>The pause between two steps of the Stream command.</summary>
    private static readonly TimeSpan StreamChunkInterval = TimeSpan.FromMilliseconds(20);

    // Cancels the replay in progress; replaced by each click on Stream.
    private CancellationTokenSource? _streamCts;

    // The replay in progress or last started, which a new one waits for: a viewer has one session at a time.
    private Task? _streamTask;

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

    private void OnToggleThemeClicked(object? sender, RoutedEventArgs e)
    {
        var viewModel = (MainViewModel)DataContext!;
        viewModel.IsLightTheme = !viewModel.IsLightTheme;
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

    // ── Streaming replay ─────────────────────────────────────────────────────

    private async void OnStreamClicked(object? sender, RoutedEventArgs e)
    {
        var viewModel = (MainViewModel)DataContext!;
        _streamCts?.Cancel();
        _streamCts?.Dispose();
        var cts = _streamCts = new CancellationTokenSource();
        var replay = _streamTask = ReplayAsync(viewModel, _streamTask, cts);
        await replay;
    }

    private async Task ReplayAsync(MainViewModel viewModel, Task? previous, CancellationTokenSource cts)
    {
        var token = cts.Token;
        // The replay a click interrupted ends its session as it unwinds; WhenAny does not rethrow its failure.
        if (previous is not null)
            await Task.WhenAny(previous);
        if (token.IsCancellationRequested)
            return;

        var text = await viewModel.ReadCurrentMarkdownAsync();
        if (text is null || token.IsCancellationRequested)
            return;

        viewModel.BeginReplay();
        try
        {
            using var session = MarkdownView.BeginStreaming();
            var position = 0;
            // The viewer ends the session itself when the page changes (another tab, a file), which stops the loop.
            while (position < text.Length && !token.IsCancellationRequested && MarkdownView.IsStreaming)
            {
                var length = int.Min(StreamChunkSize, text.Length - position);
                session.Append(text.AsSpan(position, length));
                position += length;
                try
                {
                    await Task.Delay(StreamChunkInterval, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
        finally
        {
            // After the session is disposed: the view model gets the page's text back, unless the page changed.
            viewModel.EndReplay(text);
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
