// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace MarkView.Avalonia.Extensions;

/// <summary>
/// Opens the resources a document refers to: the document of <see cref="MarkdownViewer.Source"/> and its
/// images. Set one on <see cref="MarkdownViewer.ResourceResolver"/> (or
/// <see cref="MarkdownViewerDefaults.ResourceResolver"/>) to read them from somewhere the viewer cannot,
/// such as a zip archive, the application's own storage or a server that needs credentials.
/// </summary>
/// <remarks>
/// URIs reach the resolver absolute, relative ones resolved first against <see cref="MarkdownViewer.BaseUri"/>
/// (or the folder of <see cref="MarkdownViewer.Source"/>), so a document read from a custom scheme such as
/// <c>zip://manual/index.md</c> finds its images at <c>zip://manual/...</c>. Images given as <c>data:</c> URIs
/// never reach it. A resolver can be called from any thread and for several resources at once.
/// </remarks>
public interface IResourceResolver
{
    /// <summary>
    /// Opens the resource at <paramref name="uri"/> for reading, or returns <c>null</c> to let the viewer
    /// open it itself (<c>avares://</c>, <c>file://</c>, <c>http://</c> and <c>https://</c>). The viewer
    /// disposes of the stream. A resource that cannot be read (missing, forbidden) should throw: the viewer
    /// then shows the image's placeholder, or keeps the current document.
    /// </summary>
    /// <param name="uri">The absolute URI of the resource, with no fragment.</param>
    /// <param name="cancellationToken">Cancelled when the resource is no longer needed.</param>
    /// <returns>
    /// The resource's content, or <c>null</c> when the resolver does not handle <paramref name="uri"/>. A task
    /// that has already completed when it is returned (e.g. from <see cref="Task.FromResult{TResult}"/>) lets
    /// <see cref="MarkdownViewer.Source"/> render its document before the property setter returns.
    /// </returns>
    Task<Stream?> OpenAsync(Uri uri, CancellationToken cancellationToken = default);
}
