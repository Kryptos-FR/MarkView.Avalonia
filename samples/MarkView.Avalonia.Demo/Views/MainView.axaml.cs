using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

using MarkView.Avalonia.Demo.MermaidStyles;

namespace MarkView.Avalonia.Demo.Views;

public partial class MainView : UserControl
{
    // Cancels the replay in progress; replaced by each click on Stream.
    private CancellationTokenSource? _streamCts;

    // The replay in progress or last started, which a new one waits for: a viewer has one session at a time.
    private Task? _streamTask;

    // How often the source view renders during a replay; see ReplayAsync.
    private static readonly TimeSpan SourceUpdateInterval = TimeSpan.FromMilliseconds(200);

    // Bumped by each change to the source view, so a read that finishes after a later one is dropped.
    private int _sourceVersion;

    // The view model whose changes the view follows; replaced with the data context.
    private MainViewModel? _viewModel;

    public MainView()
    {
        InitializeComponent();

        // Ctrl+F (Cmd+F on macOS) from anywhere in the view, before the focused control sees it.
        AddHandler(KeyDownEvent, OnViewKeyDown, RoutingStrategies.Tunnel);
        MarkdownView.PropertyChanged += OnViewerPropertyChanged;
        SourceView.PropertyChanged += OnViewerPropertyChanged;
        // The styles button of a Mermaid diagram, on any page.
        AddHandler(MermaidStylesDialog.RequestedEvent, (_, e) => StylesDialog.Show(e.Code, e.Source as IInputElement));
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdatePageArea();
            RefreshSource();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.PageView) or nameof(MainViewModel.IsEditing):
                UpdatePageArea();
                RefreshSource();
                break;
            case nameof(MainViewModel.Source) or nameof(MainViewModel.Markdown) or nameof(MainViewModel.IsColorExtensionSelected):
                RefreshSource();
                break;
        }
    }

    // ── Preview, markdown, or both ───────────────────────────────────────────

    private void UpdatePageArea()
    {
        var view = _viewModel!.PageView;
        // A hidden pane still gets its star share of the width, so its column is closed instead.
        PageArea.ColumnDefinitions[0].Width = view == PageView.Preview ? new GridLength(0) : GridLength.Star;
        PageArea.ColumnDefinitions[2].Width = view == PageView.Source ? new GridLength(0) : GridLength.Star;
        PageSplitter.IsVisible = view == PageView.SideBySide;

        // Find searches the pane on the right, or the markdown when it is shown alone.
        var hidden = FindTarget == SourceView ? MarkdownView : SourceView;
        hidden.Find(string.Empty);
        FindTarget.Find(FindBox.Text ?? string.Empty);
        UpdateFindStatus();
    }

    /// <summary>
    /// Shows the markdown of the page in the source view, once it is shown and unless a replay streams
    /// it in. Each change of page reads the text again: a file may have changed since it was opened.
    /// </summary>
    private async void RefreshSource()
    {
        if (_viewModel is not { IsSourceViewShown: true, IsReplaying: false } viewModel)
            return;
        var version = ++_sourceVersion;
        var text = await viewModel.ReadPageMarkdownAsync();
        if (version == _sourceVersion)
            SourceView.Markdown = text is null ? null : CodeFence(text) + "markdown\n" + text + CodeFenceEnd(text);
    }

    /// <summary>A fence longer than any run of backticks in the text, so none of them closes it.</summary>
    private static string CodeFence(string text)
    {
        int longest = 0, run = 0;
        foreach (var c in text)
        {
            run = c == '`' ? run + 1 : 0;
            longest = int.Max(longest, run);
        }
        return new string('`', int.Max(3, longest + 1));
    }

    /// <summary>The closing fence, on a line of its own.</summary>
    private static string CodeFenceEnd(string text) => (text.EndsWith('\n') ? "" : "\n") + CodeFence(text);

    // ── Find in document ─────────────────────────────────────────────────────

    private MarkdownViewer FindTarget => _viewModel is { PageView: PageView.Source, IsEditing: false } ? SourceView : MarkdownView;

    private void OnViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender == FindTarget
            && (e.Property == MarkdownViewer.FindMatchCountProperty || e.Property == MarkdownViewer.CurrentFindMatchIndexProperty))
            UpdateFindStatus();
    }

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
        FindTarget.Find(FindBox.Text ?? string.Empty);
        FindTarget.FindNext();
        UpdateFindStatus();
    }

    private void OnFindBoxKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter when e.KeyModifiers == KeyModifiers.Shift:
                FindTarget.FindPrevious();
                e.Handled = true;
                break;
            case Key.Enter:
                FindTarget.FindNext();
                e.Handled = true;
                break;
            case Key.Escape:
                FindBox.Text = string.Empty;
                FindTarget.Focus();
                e.Handled = true;
                break;
        }
    }

    private void OnFindPreviousClicked(object? sender, RoutedEventArgs e) => FindTarget.FindPrevious();

    private void OnFindNextClicked(object? sender, RoutedEventArgs e) => FindTarget.FindNext();

    private void UpdateFindStatus()
    {
        var target = FindTarget;
        var count = target.FindMatchCount;
        FindPreviousButton.IsEnabled = FindNextButton.IsEnabled = count > 0;
        FindStatus.Text = string.IsNullOrEmpty(FindBox.Text) ? string.Empty
            : count == 0 ? "No results"
            : target.CurrentFindMatchIndex < 0 ? $"{count} matches"
            : $"{target.CurrentFindMatchIndex + 1} of {count}";
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

    private async void OnEditClicked(object? sender, RoutedEventArgs e)
    {
        var viewModel = (MainViewModel)DataContext!;
        if (viewModel.IsEditing)
        {
            viewModel.EndEditing();
            return;
        }
        // A replay in progress stops first, which gives the page its whole text back to edit.
        _streamCts?.Cancel();
        if (_streamTask is not null)
            await Task.WhenAny(_streamTask);
        if (await viewModel.ReadCurrentMarkdownAsync() is { } text)
            viewModel.BeginEditing(text);
    }

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
        var healing = viewModel.HealIncompleteSyntax;
        var session = MarkdownView.BeginStreaming(healIncompleteSyntax: healing);
        // The source view streams the same text, raw, inside a code block: shown or not, it is then current.
        // A session continues from the viewer's text, so the view is emptied first, as BeginReplay empties the page.
        _sourceVersion++;
        SourceView.Markdown = null;
        // Its one code block is rendered again whole at each update, so it updates less often than the page.
        var sourceSession = SourceView.BeginStreaming(SourceUpdateInterval, healIncompleteSyntax: false);
        sourceSession.Append(CodeFence(text) + "markdown\n");
        var position = 0;
        try
        {
            // The viewer ends the session itself when the page changes (another tab, a file), which stops the loop.
            while (position < text.Length && !token.IsCancellationRequested && MarkdownView.IsStreaming)
            {
                if (healing != viewModel.HealIncompleteSyntax)
                {
                    // Healing is a session option: a new session continues from the text streamed so far.
                    healing = viewModel.HealIncompleteSyntax;
                    await session.CompleteAsync();
                    session = MarkdownView.BeginStreaming(healIncompleteSyntax: healing);
                }
                var length = int.Min(viewModel.ReplayChunkSize, text.Length - position);
                session.Append(text.AsSpan(position, length));
                if (!sourceSession.IsCompleted)
                    sourceSession.Append(text.AsSpan(position, length));
                position += length;
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(viewModel.ReplayInterval), token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
        finally
        {
            session.Dispose();
            // Closing the code block, when the whole text was streamed, leaves the text the page's own would show.
            if (position == text.Length && !sourceSession.IsCompleted)
                sourceSession.Append(CodeFenceEnd(text));
            sourceSession.Dispose();
            // After the session is disposed: the view model gets the page's text back, unless the page changed.
            viewModel.EndReplay(text);
        }
    }

    private void OnOutlineSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems is [OutlineRow row])
        {
            if (_viewModel!.PageView == PageView.Source)
            {
                // The headings are in the rendered page: show it, and scroll once it is laid out.
                _viewModel.PageView = PageView.Preview;
                var slug = row.Entry.Slug;
                Dispatcher.UIThread.Post(() => MarkdownView.ScrollToAnchor(slug), DispatcherPriority.Loaded);
            }
            else
                MarkdownView.ScrollToAnchor(row.Entry.Slug);
            OutlineToggle.IsChecked = false;
        }

        // Clear selection so re-clicking the same heading still raises this handler.
        OutlineList.SelectedItem = null;
    }
}
