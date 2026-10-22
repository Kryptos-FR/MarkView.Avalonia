// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Parsing;

/// <summary>
/// Turns the inline HTML tags of the HTML subset into the inlines they stand for: a pair of tags such as
/// <c>&lt;b&gt;…&lt;/b&gt;</c> into an <see cref="HtmlElementInline"/>, <c>&lt;a href&gt;…&lt;/a&gt;</c> into a
/// link and <c>&lt;code&gt;…&lt;/code&gt;</c> into a code span, like their markdown counterparts, and
/// <c>&lt;img&gt;</c> and <c>&lt;br&gt;</c> into an image and a line break. The <c>&lt;source&gt;</c> tags of a
/// <c>&lt;picture&gt;</c> go with the image of its <c>&lt;img&gt;</c> (see <see cref="HtmlPicture"/>).
/// </summary>
/// <remarks>
/// Tags are matched among the children of each container inline, like elements: an end tag closes the
/// innermost open element with its name, and the elements opened inside that one and not yet closed are left
/// open. A tag without a match, and any tag outside the subset, is left as the <see cref="HtmlInline"/> Markdig
/// parsed, which is not rendered; the text around it still shows. The content of a <c>&lt;script&gt;</c> or
/// <c>&lt;style&gt;</c> element is dropped with its tags.
/// </remarks>
internal static partial class HtmlInlineBuilder
{
    [GeneratedRegex(@"^\s*(?<value>\d{1,5})(?:px)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PixelsRegex();

    /// <param name="Tag">The start tag, or <c>null</c> for the container being rebuilt.</param>
    /// <param name="Target">The inline the element's content is added to.</param>
    /// <param name="Drops">Whether the content is dropped: inside a <c>&lt;script&gt;</c> or <c>&lt;style&gt;</c>.</param>
    private readonly record struct Frame(HtmlTag? Tag, ContainerInline Target, bool Drops);

    /// <summary>
    /// The inlines <paramref name="tokens"/> show, the text of an HTML block, as a container: text with its
    /// whitespace collapsed as HTML shows it (one space between words, across tags too, and none at the start
    /// or end of a line), and the tags built as <see cref="Process"/> does. <c>null</c> when they show nothing.
    /// </summary>
    public static ContainerInline? Build(IReadOnlyList<HtmlToken> tokens)
    {
        var texts = new string?[tokens.Count];

        // A space right after another one, or at the start of a line, does not show.
        var afterSpace = true;
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Tag is { } tag)
            {
                if (tag.IsEnd)
                    continue;
                if (tag.Name == "br")
                    afterSpace = true;
                else if (tag.Name == "img")
                    afterSpace = false;
                continue;
            }

            var text = tokens[i].Text;
            if (afterSpace)
                text = text.TrimStart(' ');
            if (text.Length > 0)
                afterSpace = text[^1] == ' ';
            texts[i] = text;
        }

        // Nor does one at the end of a line.
        var atLineEnd = true;
        for (var i = tokens.Count - 1; i >= 0; i--)
        {
            if (tokens[i].Tag is { } tag)
            {
                if (!tag.IsEnd && tag.Name == "br")
                    atLineEnd = true;
                else if (!tag.IsEnd && tag.Name == "img")
                    atLineEnd = false;
                continue;
            }

            if (atLineEnd)
                texts[i] = texts[i]!.TrimEnd(' ');
            atLineEnd &= texts[i]!.Length == 0;
        }

        var container = new ContainerInline();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Tag is not null)
                container.AppendChild(new HtmlInline(tokens[i].Raw));
            else if (texts[i]!.Length > 0)
                container.AppendChild(new LiteralInline(texts[i]!));
        }

        Process(container);
        return HasContent(container) ? container : null;
    }

    /// <summary>The inlines the HTML <paramref name="html"/> shows, as <see cref="Build"/> builds them.</summary>
    public static ContainerInline? Build(string html) => Build(HtmlToken.Tokenize(html));

    /// <summary>Builds the HTML tags among the children of <paramref name="container"/>, at any depth.</summary>
    public static void Process(ContainerInline container)
    {
        var hasHtml = false;
        for (var child = container.FirstChild; child is not null; child = child.NextSibling)
        {
            if (child is ContainerInline inner)
                Process(inner);
            else
                hasHtml |= child is HtmlInline;
        }
        if (!hasHtml)
            return;

        var children = container.ToArray();
        var tags = new HtmlTag?[children.Length];
        for (var i = 0; i < children.Length; i++)
        {
            if (children[i] is HtmlInline html)
                tags[i] = HtmlTag.Parse(html.Tag);
            // Removed rather than cleared: Clear() leaves each child's sibling links behind.
            children[i].Remove();
        }

        var matched = Match(tags);
        var frames = new Stack<Frame>();
        frames.Push(new Frame(null, container, Drops: false));
        for (var i = 0; i < children.Length; i++)
        {
            var tag = tags[i];
            if (!matched[i])
            {
                Append(frames.Peek(), tag is { IsEnd: false } ? CreateVoid(tag, children[i]) : children[i]);
            }
            else if (!tag!.IsEnd)
            {
                frames.Push(new Frame(tag, CreateElement(tag), frames.Peek().Drops || tag.Name is "script" or "style"));
            }
            else
            {
                var frame = frames.Pop();
                Close(frame, frames.Peek());
            }
        }
    }

    /// <summary>
    /// Pairs each end tag with the innermost element still open with its name, if any, which closes the elements
    /// opened inside that one too. Returns which tags are paired.
    /// </summary>
    private static bool[] Match(HtmlTag?[] tags)
    {
        var matched = new bool[tags.Length];
        var open = new List<int>();
        for (var i = 0; i < tags.Length; i++)
        {
            var tag = tags[i];
            if (tag is null || !IsElement(tag.Name))
                continue;

            if (!tag.IsEnd)
            {
                if (!tag.IsSelfClosing)
                    open.Add(i);
                continue;
            }

            var opening = open.FindLastIndex(o => tags[o]!.Name == tag.Name);
            if (opening < 0)
                continue;
            matched[open[opening]] = true;
            matched[i] = true;
            open.RemoveRange(opening, open.Count - opening);
        }
        return matched;
    }

    /// <summary>Whether <paramref name="name"/> is an element of the subset with content, between two tags.</summary>
    private static bool IsElement(string name) => name is "a" or "code" or "picture" or "script" or "style" || FormattingKind(name) is not null;

    private static HtmlElementKind? FormattingKind(string name) => name switch
    {
        "b" or "strong" => HtmlElementKind.Bold,
        "i" or "em" => HtmlElementKind.Italic,
        "del" or "s" or "strike" => HtmlElementKind.Strikethrough,
        "ins" or "u" => HtmlElementKind.Underline,
        "mark" => HtmlElementKind.Mark,
        "sub" => HtmlElementKind.Subscript,
        "sup" => HtmlElementKind.Superscript,
        "kbd" => HtmlElementKind.Keyboard,
        _ => null,
    };

    /// <summary>
    /// The inline a start tag without a matching end tag stands for: an image for <c>&lt;img&gt;</c>, a line
    /// break for <c>&lt;br&gt;</c>, otherwise the <see cref="HtmlInline"/> it was parsed into.
    /// </summary>
    private static Inline? CreateVoid(HtmlTag tag, Inline parsed) => tag.Name switch
    {
        "img" => CreateImage(tag),
        "br" => new LineBreakInline { IsHard = true },
        _ => parsed,
    };

    /// <summary>
    /// An image like the markdown <c>![alt](src)</c>, with the <c>width</c> and <c>height</c> attributes as its
    /// <see cref="HtmlImageSize"/>, or <c>null</c> without a <c>src</c>.
    /// </summary>
    private static LinkInline? CreateImage(HtmlTag tag)
    {
        if (string.IsNullOrWhiteSpace(tag.Attribute("src")))
            return null;

        var image = new LinkInline(tag.Attribute("src")!.Trim(), tag.Attribute("title") ?? string.Empty) { IsImage = true };
        if (tag.Attribute("alt") is { Length: > 0 } alt)
            image.AppendChild(new LiteralInline(alt));

        var size = new HtmlImageSize(Pixels(tag.Attribute("width")), Pixels(tag.Attribute("height")));
        if (size.Width is not null || size.Height is not null)
            image.SetData(typeof(HtmlImageSize), size);
        return image;
    }

    /// <summary>A length in pixels (<c>120</c> or <c>120px</c>), or <c>null</c> for anything else, such as a percentage.</summary>
    private static int? Pixels(string? value) =>
        value is not null && PixelsRegex().Match(value) is { Success: true } match
            ? int.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture)
            : null;

    /// <summary>The inline the content of an element is gathered into, until its end tag.</summary>
    private static ContainerInline CreateElement(HtmlTag tag)
    {
        if (FormattingKind(tag.Name) is { } kind)
            return new HtmlElementInline(kind);
        if (tag.Name == "a" && !string.IsNullOrWhiteSpace(tag.Attribute("href")))
            return new LinkInline(tag.Attribute("href")!.Trim(), tag.Attribute("title") ?? string.Empty);
        // The content of <code>, of <a> without a link, and of a dropped element.
        return new ContainerInline();
    }

    /// <summary>
    /// Adds the element of <paramref name="frame"/>, now closed, to <paramref name="parent"/>. A dropped element
    /// has no content and is unwrapped into nothing; its parent drops anything else.
    /// </summary>
    private static void Close(Frame frame, Frame parent)
    {
        if (frame.Tag!.Name == "code")
        {
            var text = new StringBuilder();
            AppendText(text, frame.Target);
            Append(parent, new CodeInline(text.ToString()));
        }
        else if (frame.Target.GetType() == typeof(ContainerInline))
        {
            if (frame.Tag.Name == "picture")
                AttachSources(frame.Target);
            // An <a> without a link, or a <picture>: only its content shows.
            foreach (var child in frame.Target.ToArray())
            {
                child.Remove();
                Append(parent, child);
            }
        }
        else
        {
            Append(parent, frame.Target);
        }
    }

    /// <summary>
    /// Gives the image of the <c>&lt;img&gt;</c> of a <c>&lt;picture&gt;</c>, among <paramref name="content"/>, the
    /// picture's <c>&lt;source&gt;</c> tags, as an <see cref="HtmlPicture"/>. The source tags themselves show nothing.
    /// </summary>
    private static void AttachSources(ContainerInline content)
    {
        var picture = new HtmlPicture();
        LinkInline? image = null;
        foreach (var inline in content)
        {
            if (inline is HtmlInline html && HtmlTag.Parse(html.Tag) is { Name: "source", IsEnd: false } source)
                picture.Add(source.Attribute("media"), source.Attribute("srcset"));
            else if (inline is LinkInline { IsImage: true } img)
                image ??= img;
        }
        if (image is not null && !picture.IsEmpty)
            image.SetData(typeof(HtmlPicture), picture);
    }

    private static void Append(Frame frame, Inline? inline)
    {
        if (inline is not null && !frame.Drops)
            frame.Target.AppendChild(inline);
    }

    /// <summary>The text of the inlines in <paramref name="container"/>, for a code span: a line break reads as a space.</summary>
    private static void AppendText(StringBuilder text, ContainerInline container)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    text.Append(literal.Content.AsSpan());
                    break;
                case HtmlEntityInline entity:
                    text.Append(entity.Transcoded.AsSpan());
                    break;
                case CodeInline code:
                    text.Append(code.Content);
                    break;
                case LineBreakInline:
                    text.Append(' ');
                    break;
                case ContainerInline inner:
                    AppendText(text, inner);
                    break;
            }
        }
    }

    /// <summary>Whether <paramref name="container"/> shows anything: text other than whitespace, or an image.</summary>
    private static bool HasContent(ContainerInline container)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal when !literal.Content.IsEmptyOrWhitespace():
                case CodeInline:
                case LinkInline { IsImage: true }:
                case ContainerInline inner when HasContent(inner):
                    return true;
            }
        }
        return false;
    }
}
