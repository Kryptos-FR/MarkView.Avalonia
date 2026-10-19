// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

using MarkView.Avalonia.Mermaid;

namespace MarkView.Avalonia.Demo.MermaidStyles;

/// <summary>
/// Demo-only dialog laid over the page that shows one diagram side by side in each
/// <see cref="MermaidStyle"/>, each in its own <see cref="MarkdownViewer"/> set up with
/// <c>UseMermaid(style)</c>. It is a panel of the page rather than a window, so it works in the browser too.
/// Escape, the close button or a click outside closes it.
/// </summary>
public partial class MermaidStylesDialog : UserControl
{
    /// <summary>Raised by the "Styles" button of a diagram (<see cref="MermaidStylesExtension"/>); bubbles.</summary>
    public static readonly RoutedEvent<MermaidStylesRequestedEventArgs> RequestedEvent =
        RoutedEvent.Register<MermaidStylesDialog, MermaidStylesRequestedEventArgs>("Requested", RoutingStrategies.Bubble);

    // What had focus when the dialog opened (the diagram's button), focused again when it closes.
    private IInputElement? _returnFocus;

    public MermaidStylesDialog()
    {
        InitializeComponent();
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            Close();
            e.Handled = true;
        };
    }

    /// <summary>Shows <paramref name="code"/>, a diagram's Mermaid source, in every style.</summary>
    public void Show(string code, IInputElement? returnFocus)
    {
        _returnFocus = returnFocus;
        var markdown = Fence(code);
        var styles = Enum.GetValues<MermaidStyle>();

        Columns.Children.Clear();
        Columns.ColumnDefinitions.Clear();
        for (var i = 0; i < styles.Length; i++)
        {
            Columns.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var column = CreateColumn(styles[i], markdown);
            Grid.SetColumn(column, i);
            Columns.Children.Add(column);
        }

        IsVisible = true;
        // Focus only moves to a control that has been laid out since it became visible.
        Dispatcher.UIThread.Post(() => CloseButton.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
    }

    public void Close()
    {
        if (!IsVisible) return;
        IsVisible = false;
        Columns.Children.Clear();
        _returnFocus?.Focus();
        _returnFocus = null;
    }

    private static Control CreateColumn(MermaidStyle style, string markdown)
    {
        var title = new TextBlock
        {
            Text = style == MermaidStyle.Quiet ? $"{style} (default)" : style.ToString(),
            FontWeight = FontWeight.SemiBold,
        };
        var call = new TextBlock
        {
            Text = $"UseMermaid(MermaidStyle.{style})",
            FontFamily = new FontFamily("Cascadia Code, Consolas, Menlo, monospace"),
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 8),
        };
        call.Bind(TextBlock.ForegroundProperty, call.GetResourceObservable("TextControlPlaceholderForeground"));

        // A viewer of its own: its UseMermaid() renderer goes ahead of the app-wide one, so the
        // diagram renders in this style, without a "Styles" button.
        var viewer = new MarkdownViewer().UseMermaid(style);
        viewer.Markdown = markdown;

        var header = new StackPanel { Children = { title, call } };
        DockPanel.SetDock(header, Dock.Top);
        return new DockPanel
        {
            VerticalAlignment = VerticalAlignment.Top,
            Children = { header, viewer },
        };
    }

    // A fence longer than any run of backticks in the source, so the source cannot close it early.
    private static string Fence(string code)
    {
        int longest = 0, run = 0;
        foreach (var c in code)
        {
            run = c == '`' ? run + 1 : 0;
            longest = System.Math.Max(longest, run);
        }
        var fence = new string('`', System.Math.Max(3, longest + 1));
        return $"{fence}mermaid\n{code}\n{fence}";
    }

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        Close();
        e.Handled = true;
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
