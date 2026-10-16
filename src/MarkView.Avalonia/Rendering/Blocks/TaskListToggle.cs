// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Builds the clickable marker of a task-list item: a <see cref="ToggleButton"/> whose content is the
/// item's check glyph, so it keeps the look of the static marker.
/// </summary>
/// <remarks>
/// The toggle gets its own minimal control theme instead of the application's toggle-button theme,
/// whose chrome (padding, background, accent fill when checked, control font size) would not look
/// like a list marker. Kept apart from <see cref="ListRenderer"/> so the theme and its cursor are
/// only created once a document actually renders an interactive task list.
/// </remarks>
internal static class TaskListToggle
{
    private static readonly ControlTheme Theme = new(typeof(ToggleButton))
    {
        Setters =
        {
            // A transparent background makes the whole marker box hit-testable, not just the glyph.
            new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent),
            new Setter(InputElement.CursorProperty, new Cursor(StandardCursorType.Hand)),
            new Setter(TemplatedControl.TemplateProperty, new FuncControlTemplate<ToggleButton>((_, scope) =>
            {
                var presenter = new ContentPresenter { Name = "PART_ContentPresenter" };
                presenter.Bind(ContentPresenter.BackgroundProperty, new TemplateBinding(TemplatedControl.BackgroundProperty));
                presenter.Bind(ContentPresenter.ContentProperty, new TemplateBinding(ContentControl.ContentProperty));
                return presenter.RegisterInNameScope(scope);
            })),
        },
    };

    /// <summary>
    /// Creates a toggle showing <paramref name="glyph"/> in the given state. Toggling it updates the
    /// glyph, then calls <paramref name="toggled"/> with the new state.
    /// </summary>
    public static ToggleButton Create(TaskCheckGlyph glyph, bool isChecked, Action<bool> toggled)
    {
        var toggle = new ToggleButton
        {
            Theme = Theme,
            Content = glyph,
            IsChecked = isChecked,
        };
        toggle.Classes.Add("markdown-task-toggle");
        toggle.IsCheckedChanged += (_, _) =>
        {
            var nowChecked = toggle.IsChecked == true;
            glyph.IsChecked = nowChecked;
            toggled(nowChecked);
        };
        return toggle;
    }
}
