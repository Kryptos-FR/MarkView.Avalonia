using System.Globalization;

using Avalonia;
using Avalonia.Data.Converters;

namespace MarkView.Avalonia.Demo;

internal sealed record OutlineRow(TocEntry Entry, Thickness Indent);

/// <summary>
/// Flattens <see cref="MarkdownViewer.TableOfContents"/> into an indented, filterable row
/// list for the outline popup in <c>MainWindow.axaml</c>.
/// </summary>
internal sealed class OutlineFilterConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count == 0 || values[0] is not IReadOnlyList<TocEntry> entries)
            return Array.Empty<OutlineRow>();

        var filter = values.Count > 1 ? values[1] as string : null;

        return Flatten(entries, 0)
            .Where(row => string.IsNullOrWhiteSpace(filter)
                || row.Entry.Text.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        static IEnumerable<OutlineRow> Flatten(IReadOnlyList<TocEntry> entries, int depth)
        {
            foreach (var entry in entries)
            {
                yield return new OutlineRow(entry, new Thickness(depth * 16, 0, 0, 0));
                foreach (var child in Flatten(entry.Children, depth + 1))
                    yield return child;
            }
        }
    }
}
