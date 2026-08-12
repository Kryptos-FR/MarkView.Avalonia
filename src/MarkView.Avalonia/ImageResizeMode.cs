// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace MarkView.Avalonia;

/// <summary>
/// Controls how images without an explicit "=WxH" size hint are scaled relative to
/// their container. Has no effect on images that specify an explicit size — those
/// always use <see cref="Avalonia.Media.StretchDirection.DownOnly"/> regardless of
/// this setting.
/// </summary>
public enum ImageResizeMode
{
    /// <summary>
    /// Scale down to fit the container width; never enlarge past the image's native
    /// resolution. Default.
    /// </summary>
    ScaleDownToFit,

    /// <summary>
    /// No scaling — render at native pixel size. May appear cropped if the image is
    /// wider than the viewer, since horizontal scrolling is disabled by default.
    /// </summary>
    Natural,

    /// <summary>
    /// Always fill the container width, enlarging small images if necessary.
    /// </summary>
    Fill,
}
