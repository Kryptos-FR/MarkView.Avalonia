// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Data;
using Avalonia.Interactivity;

using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia;

[PseudoClasses(":truncated", ":expanded")]
[TemplatePart("PART_ExpandButton", typeof(Button))]
public partial class MarkdownViewer
{
    /// <summary>
    /// Defines the <see cref="MaxLines"/> property.
    /// </summary>
    public static readonly StyledProperty<int> MaxLinesProperty =
        AvaloniaProperty.Register<MarkdownViewer, int>(nameof(MaxLines), validate: value => value >= 0);

    /// <summary>
    /// Defines the <see cref="IsExpanded"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Defines the <see cref="ShowExpandButton"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> ShowExpandButtonProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(ShowExpandButton), true);

    /// <summary>
    /// Defines the <see cref="ExceedsMaxLines"/> property.
    /// </summary>
    public static readonly DirectProperty<MarkdownViewer, bool> ExceedsMaxLinesProperty =
        AvaloniaProperty.RegisterDirect<MarkdownViewer, bool>(nameof(ExceedsMaxLines), o => o.ExceedsMaxLines);

    private Button? _expandButton;
    private LineClamp? _lineClamp;

    /// <summary>
    /// Gets or sets the number of lines shown, for a preview: the viewer is only as tall as the
    /// document's first <see cref="MaxLines"/> lines, cut below the last of them, unless
    /// <see cref="IsExpanded"/> is <c>true</c>. Every text line counts, in headings, code blocks and
    /// table cells too, and so does each image, formula or diagram, whatever its height; lines side by
    /// side (the cells of a table row, a list marker and its text) count once. 0, the default, shows
    /// the whole document.
    /// </summary>
    /// <remarks>
    /// The hidden lines are still part of the document: <see cref="SelectAll"/> and copy include them,
    /// and <see cref="Find"/> counts their matches. Moving to a match among them with
    /// <see cref="FindNext"/> or <see cref="FindPrevious"/>, or following a link to a heading among
    /// them, sets <see cref="IsExpanded"/>. Tab skips their links.
    /// </remarks>
    public int MaxLines
    {
        get => GetValue(MaxLinesProperty);
        set => SetValue(MaxLinesProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the whole document is shown even when it has more than
    /// <see cref="MaxLines"/> lines. The expand button toggles it. Binds two-way by default, and is
    /// kept when the document changes (a new <see cref="Markdown"/> or a streaming update), so a host
    /// that reuses a viewer for another document resets it. Defaults to <c>false</c>.
    /// </summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the viewer shows its expand button below the document when the document
    /// has more than <see cref="MaxLines"/> lines: "Show more" while lines are hidden, then "Show less".
    /// Turn it off to provide your own, e.g. a whole preview card that opens the document. Defaults to
    /// <c>true</c>.
    /// </summary>
    public bool ShowExpandButton
    {
        get => GetValue(ShowExpandButtonProperty);
        set => SetValue(ShowExpandButtonProperty, value);
    }

    /// <summary>
    /// Gets whether the document has more than <see cref="MaxLines"/> lines, whether or not
    /// <see cref="IsExpanded"/> shows them. Lines are hidden while this is <c>true</c> and
    /// <see cref="IsExpanded"/> is <c>false</c>, when the viewer also has the <c>:truncated</c>
    /// pseudo-class. Updated by layout, so it is <c>false</c> until the document is laid out.
    /// </summary>
    public bool ExceedsMaxLines
    {
        get;
        private set => SetAndRaise(ExceedsMaxLinesProperty, ref field, value);
    }

    private void OnTruncationPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == ContentProperty)
        {
            if (_lineClamp is not null)
            {
                _lineClamp.ExceedsMaxLinesChanged -= OnExceedsMaxLinesChanged;
                _lineClamp.Dispose();
            }
            // A streaming session keeps its grid from one update to the next, so its clamp too.
            _lineClamp = change.GetNewValue<object?>() is Grid grid ? new LineClamp(grid) : null;
            if (_lineClamp is not null)
                _lineClamp.ExceedsMaxLinesChanged += OnExceedsMaxLinesChanged;
        }
        else if (change.Property != MaxLinesProperty && change.Property != IsExpandedProperty
                 && change.Property != ShowExpandButtonProperty)
        {
            return;
        }

        if (_lineClamp is not null)
        {
            _lineClamp.IsExpanded = IsExpanded;
            _lineClamp.MaxLines = MaxLines;
        }
        UpdateTruncationState();
    }

    private void OnExceedsMaxLinesChanged(object? sender, EventArgs e) => UpdateTruncationState();

    private void UpdateTruncationState()
    {
        ExceedsMaxLines = _lineClamp is { ExceedsMaxLines: true };
        PseudoClasses.Set(":truncated", ExceedsMaxLines && !IsExpanded);
        PseudoClasses.Set(":expanded", IsExpanded);
        if (_expandButton is not null)
            _expandButton.IsVisible = ShowExpandButton && ExceedsMaxLines;
    }

    private void AttachExpandButton(Button? button)
    {
        if (_expandButton is not null)
            _expandButton.Click -= OnExpandButtonClick;
        _expandButton = button;
        if (_expandButton is not null)
            _expandButton.Click += OnExpandButtonClick;
        UpdateTruncationState();
    }

    // SetCurrentValue keeps a binding on IsExpanded, which then hears of the change through its two-way mode.
    private void OnExpandButtonClick(object? sender, RoutedEventArgs e) =>
        SetCurrentValue(IsExpandedProperty, !IsExpanded);

    /// <summary>
    /// Expands the viewer when any part of <paramref name="bounds"/>, a rectangle in
    /// <paramref name="visual"/>'s coordinates, is below the lines shown, and lays it out again so
    /// that the scroll viewers around <paramref name="visual"/> can scroll to it.
    /// </summary>
    private void ExpandToShow(Visual visual, Rect bounds)
    {
        if (_lineClamp?.Hides(visual, bounds) != true)
            return;
        SetCurrentValue(IsExpandedProperty, true);
        UpdateLayout();
    }
}
