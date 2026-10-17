// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;

using MarkView.Avalonia.Automation;
using MarkView.Avalonia.Rendering.Inlines;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// The keyboard and screen-reader stand-in for one <see cref="MarkdownHyperlink"/>. A link is a
/// <see cref="Span"/> inside a <see cref="MarkdownSelectableTextBlock"/>, so it can wrap across lines
/// but can't take focus or have an automation peer of its own. This control, hosted by
/// <see cref="LinkFocusLayer"/>, sits over the link's text: it takes focus when the user tabs to the
/// link, draws a focus ring around each line of the link's text, activates the link on Enter, and is
/// exposed to screen readers as a hyperlink inside its paragraph.
/// </summary>
internal sealed class LinkFocusTarget : Control
{
    private const double RingThickness = 2;

    private readonly Action<MarkdownHyperlink> _activate;
    private Rect[] _lines = [];
    private Point? _textBlockOrigin;
    private Size _textBlockSize;

    static LinkFocusTarget()
    {
        AffectsRender<LinkFocusTarget>(IsFocusedProperty, ThemeVariantScope.ActualThemeVariantProperty);
    }

    public LinkFocusTarget(MarkdownSelectableTextBlock textBlock, MarkdownHyperlink link, int start, int length,
        Action<MarkdownHyperlink> activate)
    {
        TextBlock = textBlock;
        Link = link;
        Start = start;
        Length = length;
        _activate = activate;

        Focusable = true;
        // Tab order is the viewer's (see MarkdownViewer.ICustomKeyboardNavigation), not the visual tree's.
        IsTabStop = false;
        // Pointer input goes to the text underneath, as if this control weren't there.
        IsHitTestVisible = false;
        // The ring follows the link's lines; a theme adorner would box their union instead.
        FocusAdorner = null;
    }

    /// <summary>The text block whose inlines hold <see cref="Link"/>.</summary>
    public MarkdownSelectableTextBlock TextBlock { get; }

    public MarkdownHyperlink Link { get; }

    /// <summary>The link's first character position in <see cref="TextBlock"/>'s text layout.</summary>
    public int Start { get; }

    /// <summary>The number of character positions the link spans.</summary>
    public int Length { get; }

    /// <summary>The rectangle of each line of the link's text, relative to this control.</summary>
    internal IReadOnlyList<Rect> Lines => _lines;

    /// <summary>
    /// What a screen reader announces: the link's text, with an embedded image read as its alt
    /// text (a badge link holds only an image), or the link's address when there is no text at all.
    /// </summary>
    public string AccessibleName
    {
        get
        {
            var sb = new StringBuilder();
            AppendName(sb, Link.Inlines);
            var name = sb.ToString().Trim();
            return name.Length > 0 ? name : Link.NavigateUri?.OriginalString ?? string.Empty;
        }
    }

    private static void AppendName(StringBuilder sb, InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run: sb.Append(run.Text); break;
                case LineBreak: sb.Append(' '); break;
                case Span span: AppendName(sb, span.Inlines); break;
                case InlineUIContainer { Child: { } child }:
                    sb.Append(AutomationProperties.GetName(child) is { Length: > 0 } name
                        ? name
                        : MarkdownSelection.GetCopyText(child));
                    break;
            }
        }
    }

    /// <summary>Opens the link, as a click on it would.</summary>
    public void Activate() => _activate(Link);

    /// <summary>
    /// The rectangle that bounds all of the link's lines, relative to <see cref="LinkFocusLayer"/>,
    /// as of the last <see cref="UpdateGeometry"/>. The layer arranges this control there.
    /// </summary>
    internal Rect GeometryBounds { get; private set; }

    /// <summary>
    /// Recomputes where the link's lines are, relative to <paramref name="layer"/>, and returns
    /// whether <see cref="GeometryBounds"/> changed. Only re-measures the text when
    /// <see cref="TextBlock"/> moved or was resized since the last call, as this runs after every
    /// layout pass.
    /// </summary>
    internal bool UpdateGeometry(Visual layer)
    {
        var previous = GeometryBounds;
        if (!TextBlock.IsEffectivelyVisible || TextBlock.TranslatePoint(default, layer) is not { } origin)
        {
            _textBlockOrigin = null;
            SetGeometry([], default);
            return previous != default;
        }

        if (origin == _textBlockOrigin && TextBlock.Bounds.Size == _textBlockSize)
            return false;
        _textBlockOrigin = origin;
        _textBlockSize = TextBlock.Bounds.Size;

        var textOrigin = origin + new Vector(TextBlock.Padding.Left, TextBlock.Padding.Top);
        var rects = TextBlock.TextLayout.HitTestTextRange(Start, Length)
            .Where(r => r.Width > 0)
            .Select(r => r.Translate(textOrigin))
            .ToArray();

        // Union treats an empty rectangle as nothing, so this starts from the first line.
        var bounds = default(Rect);
        foreach (var rect in rects)
            bounds = bounds.Union(rect);
        SetGeometry(Array.ConvertAll(rects, r => r.Translate(-(Vector)bounds.Position)), bounds);
        return bounds != previous;
    }

    private void SetGeometry(Rect[] lines, Rect bounds)
    {
        GeometryBounds = bounds;
        SetLines(lines);
    }

    private void SetLines(Rect[] lines)
    {
        // Stryker disable all : headless tests have no renderer to observe a repaint
        if (lines.AsSpan().SequenceEqual(_lines)) return;
        _lines = lines;
        InvalidateVisual();
        // Stryker restore all
    }

    protected override Size MeasureOverride(Size availableSize) => default;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Enter || e.KeyModifiers != KeyModifiers.None) return;
        Activate();
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        if (!IsFocused) return;
        var pen = new Pen(RingBrush(), RingThickness);
        foreach (var line in _lines)
            context.DrawRectangle(null, pen, line.Inflate(RingThickness / 2), RingThickness, RingThickness);
    }

    /// <summary>FluentTheme's focus ring colour, or plain black or white when it isn't loaded.</summary>
    private IBrush RingBrush() =>
        this.TryFindResource("SystemControlFocusVisualPrimaryBrush", ActualThemeVariant, out var resource)
        && resource is IBrush brush
            ? brush
            : FallbackRingBrush(ActualThemeVariant);

    internal static IBrush FallbackRingBrush(ThemeVariant variant) =>
        variant == ThemeVariant.Dark ? Brushes.White : Brushes.Black;

    protected override AutomationPeer OnCreateAutomationPeer() => new LinkAutomationPeer(this);
}
