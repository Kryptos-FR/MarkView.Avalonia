// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// The fold state of a foldable heading (<see cref="AvaloniaRenderer.AreHeadingsFoldable"/>). A heading's
/// section is the blocks that follow it in its panel up to the next heading of the same or a higher level
/// (a smaller number); collapsing the heading hides them, nested sections included, while the heading
/// itself stays. The heading carries the <c>markdown-heading-foldable</c> class, and the <c>:expanded</c>
/// pseudo-class while its section is shown; its <see cref="HeadingFoldToggle"/> points right when collapsed
/// and down when expanded.
/// </summary>
/// <remarks>
/// Headings are siblings of the blocks they introduce, so folding hides those siblings
/// (<see cref="Visual.IsVisible"/>) rather than a container. <see cref="Apply"/> only shows again the
/// blocks it hid itself, so a block hidden for another reason stays hidden.
/// </remarks>
internal sealed class HeadingFold
{
    private static readonly AttachedProperty<HeadingFold?> StateProperty =
        AvaloniaProperty.RegisterAttached<Control, HeadingFold?>("State", typeof(HeadingFold));

    private static readonly AttachedProperty<bool> IsFoldedAwayProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsFoldedAway", typeof(HeadingFold));

    private readonly Control _heading;
    private readonly HeadingFoldToggle _toggle;
    private readonly Action<bool>? _expandedChanged;

    private HeadingFold(Control heading, HeadingFoldToggle toggle, int level, bool isExpanded, Action<bool>? expandedChanged)
    {
        _heading = heading;
        _toggle = toggle;
        Level = level;
        heading.Classes.Add("markdown-heading-foldable");
        SetState(isExpanded);
        _expandedChanged = expandedChanged;
        toggle.Toggled += Toggle;
        heading.SetValue(StateProperty, this);
    }

    /// <summary>The heading's level, 1 to 6.</summary>
    public int Level { get; }

    /// <summary>
    /// Whether the heading's section is shown. Changing it shows or hides the section at once and reports
    /// the new state to the callback given to <see cref="Attach"/>.
    /// </summary>
    public bool IsExpanded
    {
        get => _toggle.IsExpanded;
        set
        {
            if (value == IsExpanded) return;
            SetState(value);
            _expandedChanged?.Invoke(value);
            if (_heading.GetVisualParent() is Panel panel)
                Apply(panel);
        }
    }

    /// <summary>Shows the section if hidden, hides it otherwise.</summary>
    public void Toggle() => IsExpanded = !IsExpanded;

    private void SetState(bool isExpanded)
    {
        _toggle.IsExpanded = isExpanded;
        ((IPseudoClasses)_heading.Classes).Set(":expanded", isExpanded);
    }

    /// <summary>
    /// Makes <paramref name="heading"/> a foldable heading of <paramref name="level"/> whose
    /// <paramref name="toggle"/> folds its section. <paramref name="expandedChanged"/>, when given, is called
    /// with the new state each time the section is shown or hidden. A section that starts collapsed is
    /// hidden by the next <see cref="Apply"/> on the heading's panel.
    /// </summary>
    public static HeadingFold Attach(Control heading, HeadingFoldToggle toggle, int level, bool isExpanded, Action<bool>? expandedChanged = null) =>
        new(heading, toggle, level, isExpanded, expandedChanged);

    /// <summary>Returns the fold state of a heading, or <c>null</c> when it is not foldable.</summary>
    public static HeadingFold? Get(Control control) => control.GetValue(StateProperty);

    /// <summary>
    /// Hides every child of <paramref name="panel"/> inside the section of a collapsed heading, and shows again
    /// the others that an earlier call hid.
    /// </summary>
    public static void Apply(Panel panel)
    {
        // The level of the collapsed heading whose section the walk is in, or 0 outside any.
        var collapsedLevel = 0;
        foreach (var child in panel.Children)
        {
            var fold = Get(child);
            if (fold is not null && fold.Level <= collapsedLevel)
                collapsedLevel = 0;

            SetFoldedAway(child, collapsedLevel != 0);
            if (fold is { IsExpanded: false } && collapsedLevel == 0)
                collapsedLevel = fold.Level;
        }
    }

    private static void SetFoldedAway(Control child, bool foldedAway)
    {
        if (foldedAway == child.GetValue(IsFoldedAwayProperty))
            return;
        if (foldedAway)
        {
            // A block hidden for another reason stays as it is, and stays hidden once the section is shown.
            if (!child.IsVisible) return;
            child.IsVisible = false;
            child.SetValue(IsFoldedAwayProperty, true);
        }
        else
        {
            child.IsVisible = true;
            child.ClearValue(IsFoldedAwayProperty);
        }
    }

    /// <summary>
    /// Expands the collapsed headings whose sections hide <paramref name="control"/> or one of its ancestors,
    /// so that it can be shown (e.g. a heading navigated to by its anchor, or a find match). A heading that is
    /// itself the target is shown but keeps its own section folded. Returns <c>true</c> when one was expanded.
    /// </summary>
    public static bool Reveal(Control control)
    {
        var expanded = false;
        for (Visual? visual = control; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is not Control child || !child.GetValue(IsFoldedAwayProperty) || child.GetVisualParent() is not Panel panel)
                continue;

            // The headings above the child whose sections contain it: walking back, each heading of a lower
            // level than the last one met.
            var level = Get(child)?.Level ?? int.MaxValue;
            for (var i = panel.Children.IndexOf(child) - 1; i >= 0 && level > 1; i--)
            {
                if (Get(panel.Children[i]) is not { } fold || fold.Level >= level)
                    continue;
                level = fold.Level;
                if (!fold.IsExpanded)
                {
                    fold.IsExpanded = true;
                    expanded = true;
                }
            }
        }
        return expanded;
    }
}
