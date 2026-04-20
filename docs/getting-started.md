# Getting Started

## Installation

Install the core package:

```bash
dotnet add package MarkView.Avalonia
```

Optionally add extension packages for richer rendering:

```bash
dotnet add package MarkView.Avalonia.SyntaxHighlighting  # TextMate code highlighting
dotnet add package MarkView.Avalonia.Svg                 # SVG image rendering
dotnet add package MarkView.Avalonia.Mermaid             # Mermaid diagram rendering
```

## 1. Include the default theme

Add the built-in stylesheet to your application. The standard place is `App.axaml`:

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="MyApp.App">
  <Application.Styles>
    <FluentTheme />
    <StyleInclude Source="avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml" />
  </Application.Styles>
</Application>
```

## 2. Add MarkdownViewer to a window

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:mv="using:MarkView.Avalonia"
        x:Class="MyApp.MainWindow">

  <mv:MarkdownViewer Markdown="# Hello, MarkView!" />
</Window>
```

## 3. Bind to a view model

```xml
<mv:MarkdownViewer Markdown="{Binding DocumentText}" />
```

```csharp
public class MainViewModel : INotifyPropertyChanged
{
    private string _documentText = "# My Document";

    public string DocumentText
    {
        get => _documentText;
        set { _documentText = value; OnPropertyChanged(); }
    }
}
```

## 4. Enable extension packages (optional)

Configure globally at startup so all `MarkdownViewer` instances share the same settings:

```csharp
// App.axaml.cs
public override void OnFrameworkInitializationCompleted()
{
    MarkdownViewerDefaults.Pipeline = new MarkdownPipelineBuilder()
        .UseSupportedExtensions()
        .UseAlertBlocks()
        .UseFootnotes()
        .Build();

    MarkdownViewerDefaults.Extensions.AddTextMateHighlighting();
    MarkdownViewerDefaults.Extensions.AddSvg();
    MarkdownViewerDefaults.Extensions.AddMermaid();

    // Open links in the system browser
    MarkdownViewer.LinkClickedEvent.AddClassHandler<MarkdownViewer>((_, e) =>
        Process.Start(new ProcessStartInfo(e.Url) { UseShellExecute = true }));

    // ...
    base.OnFrameworkInitializationCompleted();
}
```

See [configuration.md](configuration.md) for the full set of options.
