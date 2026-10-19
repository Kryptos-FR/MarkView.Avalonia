// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Generates GitHub-style kebab-case anchor IDs from heading text.
/// Tracks duplicates and appends -1, -2, etc. until the ID is unused, as GitHub does.
/// </summary>
/// <remarks>
/// TODO: expose ISlugGenerator interface and allow injection via AvaloniaRenderer
/// to support alternative anchor schemes (e.g. GitLab, Gitea, or user-defined).
/// </remarks>
public sealed class SlugGenerator
{
    // Every slug in use, with the last -N suffix tried on it as a base.
    private readonly Dictionary<string, int> _seen = new(StringComparer.Ordinal);

    // Every slug generated, in generation order, with its base (before any -N suffix) and the number of
    // suffixes tried on that base for it, so TruncateTo can unwind _seen.
    private readonly List<(string Base, string Slug, int Suffixes)> _generated = [];

    /// <summary>
    /// The number of slugs generated since the last <see cref="Reset"/>, less those
    /// <see cref="TruncateTo"/> dropped.
    /// </summary>
    public int Count => _generated.Count;

    /// <summary>
    /// Generates the slug for a heading, appending <c>-1</c>, <c>-2</c>, ... when it is already in use,
    /// until it is not: headings "A", "A" and "A-1" get <c>a</c>, <c>a-1</c> and <c>a-1-1</c>.
    /// </summary>
    public string GenerateSlug(string headingText) => GenerateFromBase(Normalize(headingText));

    /// <summary>
    /// The slugs generated so far, in order, each with its normalized base (the slug before any
    /// <c>-N</c> suffix).
    /// </summary>
    internal IReadOnlyList<(string Base, string Slug, int Suffixes)> Generated => _generated;

    /// <summary>
    /// Generates the slug for a normalized base (one from <see cref="Generated"/>), as
    /// <see cref="GenerateSlug"/> does for a heading text with that base.
    /// </summary>
    internal string GenerateFromBase(string baseSlug)
    {
        var slug = baseSlug;
        var suffixes = 0;
        while (_seen.ContainsKey(slug))
        {
            var suffix = _seen[baseSlug] + 1;
            _seen[baseSlug] = suffix;
            suffixes++;
            slug = $"{baseSlug}-{suffix}";
        }
        _seen[slug] = 0;
        _generated.Add((baseSlug, slug, suffixes));
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
            var (baseSlug, slug, suffixes) = _generated[i];
            _seen.Remove(slug);
            if (suffixes > 0)
                _seen[baseSlug] -= suffixes;
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
