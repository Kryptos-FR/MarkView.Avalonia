using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace MarkView.Avalonia.Demo;

public class GitHubPreset : Styles
{
    public GitHubPreset() => AvaloniaXamlLoader.Load(this);
}
