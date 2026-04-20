// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace MarkView.Avalonia.Extensions;

/// <summary>
/// Shared <see cref="HttpClient"/> instance for all image loaders.
/// A single static instance is sufficient — and recommended — to allow connection pooling
/// across <see cref="BitmapImageLoader"/>, <c>SvgImageLoader</c>, and <see cref="Rendering.Inlines.LinkInlineRenderer"/>.
/// </summary>
internal static class SharedHttpClient
{
    internal static readonly HttpClient Instance = new();
}
