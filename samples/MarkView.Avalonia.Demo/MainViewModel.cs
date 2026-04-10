using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MarkView.Avalonia.Demo;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private static readonly HttpClient Http = new();

    private string? _markdown;
    private string _statusText = "Ready";
    private bool _isLoading;
    private int _selectedVersionIndex;
    private Uri? _baseUri;

    public string[] Versions { get; } = ["latest", "4.2", "4.1"];

    public int SelectedVersionIndex
    {
        get => _selectedVersionIndex;
        set
        {
            if (SetField(ref _selectedVersionIndex, value))
                _ = LoadReleaseNotesAsync();
        }
    }

    public string? Markdown
    {
        get => _markdown;
        private set => SetField(ref _markdown, value);
    }

    public Uri? BaseUri
    {
        get => _baseUri;
        private set => SetField(ref _baseUri, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    public MainViewModel()
    {
        _ = LoadReleaseNotesAsync();
    }

    private async Task LoadReleaseNotesAsync()
    {
        var version = Versions[_selectedVersionIndex];
        var url = $"https://doc.stride3d.net/{version}/en/ReleaseNotes/ReleaseNotes.md";
        var baseUrl = $"https://doc.stride3d.net/{version}/en/ReleaseNotes/";

        IsLoading = true;
        StatusText = $"Loading release notes for Stride {version}...";
        Markdown = null;

        try
        {
            var md = await Http.GetStringAsync(url);
            Markdown = md;
            BaseUri = new Uri(baseUrl);
            StatusText = $"Loaded release notes for Stride {version}";
        }
        catch (Exception ex)
        {
            Markdown = $"# Error\n\nFailed to load release notes:\n\n```\n{ex.Message}\n```";
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
