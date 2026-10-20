// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Interactivity;

namespace MarkView.Avalonia;

/// <summary>
/// Event args for <see cref="MarkdownViewer.SavingImage"/>, raised when the user asks to save an image or a
/// diagram, before the save picker opens. A handler can change the suggested file name, or cancel the
/// picker, for instance to save the picture itself with <see cref="WriteAsync"/>.
/// </summary>
public sealed class SavingImageEventArgs : RoutedEventArgs
{
    private readonly Func<MarkdownImageFormat, Stream, Task> _write;

    internal SavingImageEventArgs(string suggestedFileName, IReadOnlyList<MarkdownImageFormat> formats,
        Func<MarkdownImageFormat, Stream, Task> write)
    {
        SuggestedFileName = suggestedFileName;
        Formats = formats;
        _write = write;
    }

    /// <summary>
    /// The name the save picker suggests, without an extension: the last segment of an image's address
    /// (<c>image</c> without one), or <c>diagram</c> for a diagram.
    /// </summary>
    public string SuggestedFileName { get; set; }

    /// <summary>
    /// The formats the picture can be saved in, the one the picker suggests first: SVG then PNG for a picture
    /// drawn as vectors with its SVG document at hand, PNG alone otherwise.
    /// </summary>
    public IReadOnlyList<MarkdownImageFormat> Formats { get; }

    /// <summary>
    /// Set to <c>true</c> not to open the save picker, for instance when the handler saves the picture itself.
    /// </summary>
    public bool Cancel { get; set; }

    /// <summary>
    /// Writes the picture to <paramref name="stream"/> in <paramref name="format"/>, one of <see cref="Formats"/>.
    /// Call it on the UI thread: a picture is rendered with the UI's own drawing.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="format"/> is not one of <see cref="Formats"/>.</exception>
    public Task WriteAsync(MarkdownImageFormat format, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!Formats.Contains(format))
            throw new ArgumentException($"The picture cannot be saved as {format}.", nameof(format));
        return _write(format, stream);
    }
}
