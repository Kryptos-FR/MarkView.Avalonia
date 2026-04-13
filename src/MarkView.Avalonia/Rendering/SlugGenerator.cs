// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Globalization;
using System.Text;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Generates GitHub-style kebab-case anchor IDs from heading text.
/// Tracks duplicates and appends -1, -2, etc. as needed.
/// </summary>
/// <remarks>
/// TODO: expose ISlugGenerator interface and allow injection via AvaloniaRenderer
/// to support alternative anchor schemes (e.g. GitLab, Gitea, or user-defined).
/// </remarks>
public class SlugGenerator
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

    /// <summary>
    /// Single-pass normalisation: lowercase + filter + whitespace/hyphen collapsing.
    /// Eliminates the four intermediate strings produced by the previous regex pipeline.
    /// </summary>
    private static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        bool pendingHyphen = false;

        foreach (char rawCh in text)
        {
            char ch = char.ToLowerInvariant(rawCh);
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);

            bool isKeepable =
                cat is UnicodeCategory.LowercaseLetter
                    or UnicodeCategory.UppercaseLetter  // after ToLowerInvariant — kept for safety
                    or UnicodeCategory.TitlecaseLetter
                    or UnicodeCategory.ModifierLetter
                    or UnicodeCategory.OtherLetter
                    or UnicodeCategory.DecimalDigitNumber
                    or UnicodeCategory.LetterNumber
                    or UnicodeCategory.OtherNumber;

            if (isKeepable)
            {
                if (pendingHyphen && sb.Length > 0)
                    sb.Append('-');
                pendingHyphen = false;
                sb.Append(ch);
            }
            else if (char.IsWhiteSpace(ch) || ch == '-')
            {
                // Defer hyphen until next keepable char; handles runs + leading/trailing
                if (sb.Length > 0)
                    pendingHyphen = true;
            }
            // else: stripped — not a letter, digit, whitespace, or hyphen
        }

        return sb.ToString();
    }
}
