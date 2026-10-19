using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace MarkView.Avalonia.Demo;

public class DocsPreset : Styles
{
    public DocsPreset() => AvaloniaXamlLoader.Load(this);
}
