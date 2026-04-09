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
}
