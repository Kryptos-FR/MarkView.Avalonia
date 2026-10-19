// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls.Documents;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Scales every font size inside an element: the element's own, and every size a style, a renderer or the
/// application sets on an element or inline below it, theme presets and custom styles included.
/// </summary>
/// <remarks>
/// <see cref="TextElement.FontSizeProperty"/> is coerced on every <see cref="StyledElement"/> to its value times
/// the inherited <see cref="ScaleProperty"/>, which is 1 outside a scaled element. Only values set on an element
/// are coerced: an inherited font size was already scaled where it was set, so it is never scaled twice.
/// </remarks>
internal static class TextScaling
{
    /// <summary>The factor applied to the font sizes of an element and of everything below it.</summary>
    public static readonly AttachedProperty<double> ScaleProperty =
        AvaloniaProperty.RegisterAttached<StyledElement, double>("TextScale", typeof(TextScaling), 1.0, inherits: true,
            validate: scale => scale > 0 && double.IsFinite(scale));

    static TextScaling()
    {
        // Controls find this metadata on StyledElement. Inlines find the property's own on TextElement first,
        // so they get theirs on Inline, the base of every inline.
        TextElement.FontSizeProperty.OverrideMetadata<StyledElement>(CoercedMetadata());
        TextElement.FontSizeProperty.OverrideMetadata<Inline>(CoercedMetadata());
        ScaleProperty.Changed.AddClassHandler<StyledElement>((element, _) =>
        {
            // An element without a font size of its own inherits one that is already scaled. Coercing it
            // anyway would turn the default size into a value of its own and cut it off from its parent.
            if (element.IsSet(TextElement.FontSizeProperty))
                element.CoerceValue(TextElement.FontSizeProperty);
        });
    }

    private static StyledPropertyMetadata<double> CoercedMetadata() => new(
        // Avalonia validates the default before merging it with the base metadata, so it is repeated here.
        defaultValue: TextElement.FontSizeProperty.GetDefaultValue(typeof(StyledElement)),
        coerce: (element, size) => size * element.GetValue(ScaleProperty));

    /// <summary>
    /// Registers the coercion. A font size set before then is never scaled, so this must run before the first
    /// document is built.
    /// </summary>
    public static void Register()
    {
        // The static constructor does the work, once.
    }

    /// <summary>
    /// Scales the font sizes of <paramref name="element"/> and of everything below it by <paramref name="scale"/>,
    /// starting from <paramref name="fontSize"/> (the size it would inherit).
    /// </summary>
    public static void Apply(StyledElement element, double scale, double fontSize)
    {
        element.SetValue(ScaleProperty, scale);
        // The text that only inherits a font size takes it from here, scaled by the coercion.
        if (scale == 1)
            element.ClearValue(TextElement.FontSizeProperty);
        else
            element.SetValue(TextElement.FontSizeProperty, fontSize);
    }
}
