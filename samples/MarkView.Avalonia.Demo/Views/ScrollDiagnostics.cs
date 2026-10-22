using System.Diagnostics;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace MarkView.Avalonia.Demo.Views;

// Temporary: logs wheel events, focus, pointer capture, bring-into-view requests and scroll offset
// changes (with the stack that made them) to %TEMP%/markview-scroll-diag.log.
internal static class ScrollDiagnostics
{
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "markview-scroll-diag.log");
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static bool _inWheel;

    public static void Attach(MarkdownViewer viewer)
    {
        File.WriteAllText(LogPath, $"started {DateTime.Now:O}\n");
        viewer.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<ScrollViewer>("PART_ScrollViewer") is not { } sv) return;
            Log($"template applied, scroll viewer {Id(sv)}");
            sv.PropertyChanged += (_, c) =>
            {
                if (c.Property == ScrollViewer.OffsetProperty)
                    Log($"offset {((Vector)c.OldValue!).Y:0.##} -> {((Vector)c.NewValue!).Y:0.##} (in wheel: {_inWheel})\n{Stack()}");
                else if (c.Property == ScrollViewer.ExtentProperty || c.Property == ScrollViewer.ViewportProperty)
                    Log($"{c.Property.Name} {c.OldValue} -> {c.NewValue}");
            };
            sv.AddHandler(Control.RequestBringIntoViewEvent,
                (_, r) => Log($"bring into view: {Id(r.TargetObject)} rect {r.TargetRect}\n{Stack()}"),
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        };
        viewer.AttachedToVisualTree += (_, _) =>
        {
            if (TopLevel.GetTopLevel(viewer) is not { } top) return;
            top.AddHandler(InputElement.PointerWheelChangedEvent, (_, e) =>
            {
                _inWheel = true;
                Log($"wheel tunnel: delta {e.Delta} at {e.GetPosition(top)} source {Id(e.Source)} captured {Id(e.Pointer.Captured)} focused {Id(top.FocusManager?.GetFocusedElement())}");
            }, RoutingStrategies.Tunnel, handledEventsToo: true);
            top.AddHandler(InputElement.PointerWheelChangedEvent, (_, e) =>
            {
                _inWheel = false;
                Log($"wheel bubble: handled {e.Handled} route {Route(e.Source)}");
            }, RoutingStrategies.Bubble, handledEventsToo: true);
            top.AddHandler(InputElement.PointerPressedEvent,
                (_, e) => Log($"pressed: source {Id(e.Source)} captured {Id(e.Pointer.Captured)}"), RoutingStrategies.Tunnel, handledEventsToo: true);
            top.AddHandler(InputElement.PointerReleasedEvent,
                (_, e) => Log($"released: source {Id(e.Source)} captured {Id(e.Pointer.Captured)}"), RoutingStrategies.Tunnel, handledEventsToo: true);
            top.AddHandler(InputElement.PointerReleasedEvent,
                (_, e) => Log($"released (bubble): captured {Id(e.Pointer.Captured)}"), RoutingStrategies.Bubble, handledEventsToo: true);
            top.AddHandler(InputElement.PointerCaptureLostEvent,
                (_, e) => Log($"capture lost: source {Id(e.Source)} now {Id(e.Pointer.Captured)}"), RoutingStrategies.Bubble, handledEventsToo: true);
            top.AddHandler(InputElement.GotFocusEvent,
                (_, e) => Log($"got focus: {Id(e.Source)} via {e.NavigationMethod}"), RoutingStrategies.Bubble, handledEventsToo: true);
            top.AddHandler(InputElement.LostFocusEvent,
                (_, e) => Log($"lost focus: {Id(e.Source)}"), RoutingStrategies.Bubble, handledEventsToo: true);
            if (top is Window window)
            {
                window.Activated += (_, _) => Log("window activated");
                window.Deactivated += (_, _) => Log("window deactivated");
            }
        };
    }

    private static string Route(object? source)
    {
        var parts = new List<string>();
        for (var v = source as Visual; v is not null && parts.Count < 12; v = v.GetVisualParent())
            parts.Add(Id(v));
        return string.Join(" < ", parts);
    }

    private static string Id(object? o) => o switch
    {
        null => "null",
        StyledElement s => $"{s.GetType().Name}{(string.IsNullOrEmpty(s.Name) ? "" : "#" + s.Name)}{(s.Classes.Count > 0 ? "." + string.Join(".", s.Classes) : "")}@{s.GetHashCode():x}",
        _ => o.GetType().Name,
    };

    private static string Stack() =>
        string.Join("\n", new StackTrace(2).GetFrames().Take(25)
            .Select(f => f.GetMethod())
            .Where(m => m is not null)
            .Select(m => $"    {m!.DeclaringType?.FullName}.{m.Name}"));

    private static void Log(string line)
    {
        var text = $"[{Clock.ElapsedMilliseconds,7}] {line}\n";
        try { File.AppendAllText(LogPath, text); } catch (IOException) { }
    }
}
