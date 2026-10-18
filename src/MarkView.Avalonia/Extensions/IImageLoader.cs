// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Media;

namespace MarkView.Avalonia.Extensions;

/// <summary>
/// Loads an image from a URL.  Implementations are tried in list order;
/// the first one whose <see cref="CanLoad"/> returns <c>true</c> is used.
/// </summary>
public interface IImageLoader
{
    bool CanLoad(string url);
    Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads <paramref name="url"/>, reading its content through <paramref name="resources"/>: the viewer's
    /// <see cref="MarkdownViewer.ResourceResolver"/>, which falls back to the viewer's own loading of
    /// <c>avares://</c>, <c>file://</c> and <c>http(s)://</c> URLs. This is the method the viewer calls.
    /// Implement it so that images from the application's own sources (a zip archive, an authenticated
    /// server) load as well; by default it calls <see cref="LoadAsync(string, CancellationToken)"/>, which
    /// ignores the resolver.
    /// </summary>
    Task<IImage?> LoadAsync(string url, IResourceResolver resources, CancellationToken cancellationToken = default) =>
        LoadAsync(url, cancellationToken);
}
