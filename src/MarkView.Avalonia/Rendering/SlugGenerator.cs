// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

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

    // The normalized base slug (before any -N suffix) of every slug generated, in generation order,
    // so TruncateTo can unwind _seen.
    private readonly List<string> _generated = [];

    /// <summary>
    /// The number of slugs generated since the last <see cref="Reset"/>, less those
    /// <see cref="TruncateTo"/> dropped.
    /// </summary>
    public int Count => _generated.Count;

    /// <summary>
    /// Generates the slug for a heading, appending <c>-1</c>, <c>-2</c>, ... when the same text was
    /// seen before.
    /// </summary>
    public string GenerateSlug(string headingText)
    {
        var slug = Normalize(headingText);
        _generated.Add(slug);
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

    /// <summary>
    /// Forgets every slug generated after the first <paramref name="count"/>, so the next
    /// <see cref="GenerateSlug"/> returns what it would have if only those had been generated.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative or greater than <see cref="Count"/>.</exception>
    public void TruncateTo(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, Count);

        for (var i = _generated.Count - 1; i >= count; i--)
        {
            var slug = _generated[i];
            if (_seen[slug] == 0)
                _seen.Remove(slug);
            else
                _seen[slug]--;
        }
        _generated.RemoveRange(count, _generated.Count - count);
    }

    /// <summary>Forgets every slug generated so far.</summary>
    public void Reset()
    {
        _seen.Clear();
        _generated.Clear();
    }

    /// <summary>
    /// Mirrors GitHub's own heading-anchor algorithm (Markdig's <c>LinkHelper.UrilizeAsGfm</c>):
    /// keep letters/digits/hyphen/underscore, map a literal space to a hyphen, drop everything else.
    /// Unlike a naive slugifier this does NOT collapse consecutive separators — GitHub doesn't
    /// either, so e.g. "C# Tips &amp; Tricks" intentionally becomes "c-tips--tricks" (double
    /// hyphen), matching the anchor GitHub itself generates for the same heading.
    /// </summary>
    private static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);

        foreach (char ch in text)
        {
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
                sb.Append(char.ToLowerInvariant(ch));
            else if (ch == ' ')
                sb.Append('-');
            // else: dropped — not a letter, digit, hyphen, underscore, or plain space
        }

        return sb.Length == 0 ? "section" : sb.ToString();
    }
}
