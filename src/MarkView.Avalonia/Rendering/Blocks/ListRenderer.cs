using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;

namespace MarkView.Avalonia.Rendering.Blocks;

public class ListRenderer : AvaloniaObjectRenderer<ListBlock>
{
    protected override void Write(AvaloniaRenderer renderer, ListBlock obj)
    {
        var listPanel = new StackPanel { Spacing = obj.IsLoose ? 8 : 2 };
        listPanel.Classes.Add("markdown-list");
        if (obj.IsOrdered)
            listPanel.Classes.Add("markdown-list-ordered");
        else
            listPanel.Classes.Add("markdown-list-unordered");
        if (obj.IsLoose)
            listPanel.Classes.Add("markdown-list-loose");
        else
            listPanel.Classes.Add("markdown-list-tight");

        int index = obj.IsOrdered ? (obj.OrderedStart is null ? 1 : int.TryParse(obj.OrderedStart, out var start) ? start : 1) : 0;

        foreach (var item in obj)
        {
            if (item is not ListItemBlock listItem) continue;

            var itemGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            };

            bool isTaskItem = listItem.Count > 0
                && listItem[0] is ParagraphBlock para
                && para.Inline?.FirstChild is TaskList;

            if (!isTaskItem)
            {
                var markerText = obj.IsOrdered ? $"{index}." : "\u2022";
                var marker = new TextBlock
                {
                    Text = markerText,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Top,
                };
                marker.Classes.Add("markdown-list-marker");
                Grid.SetColumn(marker, 0);
                itemGrid.Children.Add(marker);
            }

            var contentPanel = new StackPanel { Spacing = 4 };
            Grid.SetColumn(contentPanel, 1);
            itemGrid.Children.Add(contentPanel);

            renderer.Push(contentPanel);
            renderer.WriteChildren(listItem);
            renderer.Pop();

            listPanel.Children.Add(itemGrid);
            if (obj.IsOrdered) index++;
        }

        renderer.WriteBlock(listPanel);
    }
}
