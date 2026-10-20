// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Styling;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Finds the control themes renderers give their controls (<c>MarkdownParagraphTheme</c>,
/// <c>MarkdownCodeBlockTheme</c>, ...) by resource key, once per key, and keeps them current.
/// </summary>
/// <remarks>
/// A control theme only costs memory on the controls it is set on, where a style costs on every
/// control of its selector's type, so the themes carry the document's look; they read their values
/// from resource keys, which theme presets redefine. A key no theme defines gets an empty theme of its
/// own, so that <see cref="Refresh"/> can swap the real one in once it is defined.
/// </remarks>
internal sealed class ThemeLookup
{
    private readonly Func<string, object?> _find;
    private readonly Dictionary<string, ControlTheme> _themes = new(StringComparer.Ordinal);
    private readonly HashSet<ControlTheme> _placeholders = new(ReferenceEqualityComparer.Instance);

    /// <param name="find">Returns the resource with the given key, or <c>null</c>.</param>
    public ThemeLookup(Func<string, object?> find) => _find = find;

    /// <summary>A lookup in the application's resources, for a renderer that belongs to no viewer.</summary>
    public static ThemeLookup ForApplication() => new(FindInApplication);

    /// <summary>
    /// Returns the resource <paramref name="key"/> found from <paramref name="element"/>, or from the
    /// application when the element is not in a tree that reaches it.
    /// </summary>
    public static object? Find(StyledElement element, string key) =>
        element.TryFindResource(key, out var value) ? value : FindInApplication(key);

    private static object? FindInApplication(string key) =>
        Application.Current is { } app && app.TryFindResource(key, out var value) ? value : null;

    /// <summary>Returns the theme with the resource key <paramref name="key"/>.</summary>
    public ControlTheme Get(string key)
    {
        if (!_themes.TryGetValue(key, out var theme))
        {
            theme = Resolve(key);
            _themes[key] = theme;
        }
        return theme;
    }

    /// <summary>
    /// Looks up every key again, and returns the themes that changed, old to new, or <c>null</c> when
    /// none did: a preset that defines a theme of its own was added or removed.
    /// </summary>
    public Dictionary<ControlTheme, ControlTheme>? Refresh()
    {
        Dictionary<ControlTheme, ControlTheme>? changed = null;
        foreach (var key in _themes.Keys.ToList())
        {
            var theme = _themes[key];
            var found = _find(key) as ControlTheme;
            if (found is null ? _placeholders.Contains(theme) : found == theme)
                continue;
            var replacement = found ?? Placeholder();
            (changed ??= [])[theme] = replacement;
            _themes[key] = replacement;
            _placeholders.Remove(theme);
        }
        return changed;
    }

    /// <summary>Gives the controls under <paramref name="root"/> the new themes in <paramref name="changed"/>.</summary>
    public static void Replace(ILogical root, Dictionary<ControlTheme, ControlTheme> changed)
    {
        foreach (var element in root.GetLogicalDescendants())
        {
            if (element is StyledElement { Theme: { } theme } styled && changed.TryGetValue(theme, out var replacement))
            {
                styled.Theme = replacement;
                // Restyled now, as a removed style would be, rather than on the next layout pass
                styled.ApplyStyling();
            }
        }
    }

    private ControlTheme Resolve(string key) => _find(key) as ControlTheme ?? Placeholder();

    private ControlTheme Placeholder()
    {
        var placeholder = new ControlTheme(typeof(StyledElement));
        _placeholders.Add(placeholder);
        return placeholder;
    }
}
