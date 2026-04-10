// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text.RegularExpressions;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Generates GitHub-style kebab-case anchor IDs from heading text.
/// Tracks duplicates and appends -1, -2, etc. as needed.
/// </summary>
/// <remarks>
/// TODO: expose ISlugGenerator interface and allow injection via AvaloniaRenderer
/// to support alternative anchor schemes (e.g. GitLab, Gitea, or user-defined).
/// </remarks>
public partial class SlugGenerator
{
    private readonly Dictionary<string, int> _seen = new(StringComparer.Ordinal);

    public string GenerateSlug(string headingText)
    {
        var slug = Normalize(headingText);
        if (_seen.TryGetValue(slug, out int count))
        {
            _seen[slug] = count + 1;
            slug = $"{slug}-{count + 1}";
        }
        else
        {
            _seen[slug] = 0;
        }
        return slug;
    }

    public void Reset() => _seen.Clear();

    private static string Normalize(string text)
    {
        // Lowercase
        text = text.ToLowerInvariant();
        // Remove characters that are not letters, digits, spaces, or hyphens
        text = InvalidCharacterRegex().Replace(text, "");
        // Replace whitespace runs with a single hyphen
        text = WhitespaceRegex().Replace(text, "-");
        // Collapse consecutive hyphens
        text = CollapseRegex().Replace(text, "-");
        // Trim leading/trailing hyphens
        return text.Trim('-');
    }

    [GeneratedRegex(@"[^\p{L}\p{N}\s\-]")]
    private static partial Regex InvalidCharacterRegex();
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
    [GeneratedRegex(@"-{2,}")]
    private static partial Regex CollapseRegex();
}
