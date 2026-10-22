// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace MarkView.Avalonia;

/// <summary>What a URL given to a <see cref="MarkdownUrlTransform"/> is used for.</summary>
public enum MarkdownUrlKind
{
    /// <summary>The target of a link: <c>[text](url)</c>, <c>&lt;url&gt;</c>, <c>&lt;a href&gt;</c>, a video link.</summary>
    Link,

    /// <summary>The address of an image to load: <c>![alt](url)</c>, <c>&lt;img src&gt;</c>, a video thumbnail.</summary>
    Image,
}

/// <summary>
/// Rewrites or drops the URL of a link or image while the document renders (see
/// <see cref="MarkdownViewer.UrlTransform"/>).
/// </summary>
/// <param name="url">
/// The URL, already resolved against <see cref="MarkdownViewer.BaseUri"/> (or the folder of
/// <see cref="MarkdownViewer.Source"/>) when it was relative; it stays relative without either. An e-mail
/// autolink comes as <c>mailto:</c>. Same-document anchors (<c>#heading</c>) are not given to the transform.
/// </param>
/// <param name="kind">Whether the URL is a link's target or an image's address.</param>
/// <returns>
/// The URL to use, which can be <paramref name="url"/> itself; or <c>null</c> to drop it: a link then renders
/// as its plain text and an image as its <c>markdown-image-failed</c> placeholder, without being loaded.
/// </returns>
public delegate string? MarkdownUrlTransform(string url, MarkdownUrlKind kind);
