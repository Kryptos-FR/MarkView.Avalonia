// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace MarkView.Avalonia.Parsing;

/// <summary>
/// The <c>width</c> and <c>height</c> attributes of an HTML <c>&lt;img&gt;</c>, in pixels, stored as the data of
/// the image's <c>LinkInline</c> (keyed by this type). Like the <c>![alt](url =WxH)</c> size hint, they cap the
/// image's size; either can be missing.
/// </summary>
internal readonly record struct HtmlImageSize(int? Width, int? Height);
