// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Globalization;
using System.Text;

using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;

using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Rendering.Blocks;

public sealed class ListRenderer : AvaloniaObjectRenderer<ListBlock>
{
    protected override void Write(AvaloniaRenderer renderer, ListBlock obj)
    {
        var listPanel = new StackPanel { Spacing = obj.IsLoose ? 8 : 2 };
        // Every item's marker column takes the width of the widest marker ("iii." next to "i."),
        // so the items' text lines up.
        Grid.SetIsSharedSizeScope(listPanel, true);
        AutomationRoles.Set(listPanel, AutomationControlType.List);
        listPanel.Classes.Add("markdown-list");
        if (obj.IsOrdered)
            listPanel.Classes.Add("markdown-list-ordered");
        else
            listPanel.Classes.Add("markdown-list-unordered");
        if (obj.IsLoose)
            listPanel.Classes.Add("markdown-list-loose");
        else
            listPanel.Classes.Add("markdown-list-tight");

        int index = int.TryParse(obj.OrderedStart, out var start) ? start : 1;

        foreach (var listItem in obj.OfType<ListItemBlock>())
        {
            var itemGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto) { SharedSizeGroup = "MarkdownListMarker" },
                    new ColumnDefinition(1, GridUnitType.Star),
                },
            };
            AutomationRoles.Set(itemGrid, AutomationControlType.ListItem);

            bool isTaskItem = listItem.Count > 0
                && listItem[0] is ParagraphBlock para
                && para.Inline?.FirstChild is TaskList;

            if (isTaskItem)
            {
                var taskParagraph = (ParagraphBlock)listItem[0];
                var taskList = (TaskList)taskParagraph.Inline!.FirstChild!;
                // Markdig leaves the space that follows "[x]" in the next literal; the marker
                // column's margin already separates the glyph from the text.
                if (taskList.NextSibling is LiteralInline literal)
                    literal.Content.TrimStart();

                var glyph = new TaskCheckGlyph(taskList.Checked);
                glyph.Classes.Add("markdown-list-marker");
                glyph.Classes.Add("markdown-task-list");

                // Interactive: the glyph becomes the content of a toggle; the toggle takes the
                // marker's place (and layout) in the item grid.
                Control marker = renderer.AreTaskListsInteractive
                    ? TaskListToggle.Create(glyph, taskList.Checked, isChecked => renderer.OnTaskToggled(taskParagraph, isChecked))
                    : glyph;
                marker.Margin = new Thickness(0, 0, 8, 0);
                marker.VerticalAlignment = VerticalAlignment.Top;
                // Stryker disable once Statement : column 0 is Grid's default column
                Grid.SetColumn(marker, 0);
                itemGrid.Children.Add(marker);
            }
            else
            {
                var markerText = obj.IsOrdered ? OrderedMarker(index, obj.BulletType, obj.OrderedDelimiter) : "\u2022";
                var marker = new TextBlock
                {
                    Text = markerText,
                    Margin = new Thickness(0, 0, 8, 0),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                };
                marker.Classes.Add("markdown-list-marker");
                // The list item role already says it's a bullet point; a number is kept, since
                // an ordered list can start anywhere and the reader can't infer it.
                if (!obj.IsOrdered)
                    AutomationRoles.Hide(marker);
                // Stryker disable once Statement : column 0 is Grid's default column
                Grid.SetColumn(marker, 0);
                itemGrid.Children.Add(marker);
            }

            var contentPanel = new StackPanel { Spacing = 4 };
            Grid.SetColumn(contentPanel, 1);
            itemGrid.Children.Add(contentPanel);

            renderer.Push(contentPanel);
            if (isTaskItem) renderer.SkipNextTaskList = true;
            renderer.WriteChildren(listItem);
            renderer.Pop();

            listPanel.Children.Add(itemGrid);
            if (obj.IsOrdered) index++;
        }

        renderer.WriteBlock(listPanel);
    }

    /// <summary>
    /// The marker of an ordered list item: a number, or with Markdig's <c>UseListExtras()</c> a letter
    /// (<c>a.</c>, <c>A.</c>) or a roman numeral (<c>i.</c>, <c>I.</c>), followed by the list's
    /// delimiter (<c>.</c> or <c>)</c>).
    /// </summary>
    internal static string OrderedMarker(int number, char bulletType, char delimiter)
    {
        var text = bulletType switch
        {
            'a' => ToLetters(number, 'a'),
            'A' => ToLetters(number, 'A'),
            'i' => ToRoman(number)?.ToLowerInvariant(),
            'I' => ToRoman(number),
            _ => null,
        } ?? number.ToString(CultureInfo.InvariantCulture);
        return text + (delimiter == ')' ? ')' : '.');
    }

    // 1 → a, 26 → z, 27 → aa, as browsers number lower-alpha lists.
    private static string? ToLetters(int number, char first)
    {
        if (number < 1)
            return null;

        var letters = new StringBuilder();
        for (; number > 0; number = (number - 1) / 26)
            letters.Insert(0, (char)(first + (number - 1) % 26));
        return letters.ToString();
    }

    private static readonly (int Value, string Numeral)[] RomanNumerals =
    [
        (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
        (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
    ];

    private static string? ToRoman(int number)
    {
        if (number is < 1 or > 3999)
            return null;

        var numeral = new StringBuilder();
        foreach (var (value, symbol) in RomanNumerals)
        {
            for (; number >= value; number -= value)
                numeral.Append(symbol);
        }
        return numeral.ToString();
    }
}
