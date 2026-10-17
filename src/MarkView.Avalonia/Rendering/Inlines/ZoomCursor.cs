// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// The zoom-in cursor shown over an image that opens in the viewer's lightbox when clicked: a
/// magnifying glass with a plus, black outlined in white so it shows on any image. Avalonia has no
/// standard zoom cursor, so this one is drawn pixel by pixel, which works on every platform
/// (including the browser, which turns it into a CSS cursor).
/// </summary>
internal static class ZoomCursor
{
    // '#' black, 'o' white, '.' transparent.
    internal static readonly string[] Pixels =
    [
        "......................",
        ".....ooooooo..........",
        "....o#######o.........",
        "...o###ooo###o........",
        "..o##ooooooo##o.......",
        ".o##oooo#oooo##o......",
        ".o##oooo#oooo##o......",
        ".o#ooooo#ooooo#o......",
        ".o#oo#######oo#o......",
        ".o#ooooo#ooooo#o......",
        ".o##oooo#oooo##o......",
        ".o##oooo#oooo##o......",
        "..o##ooooooo##oo......",
        "...o###ooo#####oo.....",
        "....o#######o###oo....",
        ".....ooooooooo###oo...",
        ".............oo###oo..",
        "..............oo###oo.",
        "...............oo###o.",
        "................oo##o.",
        ".................oooo.",
        "......................",
    ];

    /// <summary>The centre of the lens, where the cursor points.</summary>
    internal static readonly PixelPoint HotSpot = new(8, 8);

    private static Cursor? s_instance;

    /// <summary>The cursor, created on first use (it needs the platform).</summary>
    public static Cursor Instance => s_instance ??= new Cursor(CreateBitmap(), HotSpot);

    /// <summary>Draws <see cref="Pixels"/> into a bitmap.</summary>
    internal static Bitmap CreateBitmap()
    {
        var size = Pixels.Length;
        var pixels = GetPixels();
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            // The bitmap copies the pixels.
            // Stryker disable once Arithmetic : the headless platform keeps no pixel data, so no test can see the stride
            return new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Premul, handle.AddrOfPinnedObject(),
                new PixelSize(size, size), new Vector(96, 96), size * 4);
        }
        finally
        {
            // Stryker disable once Statement : leaving the buffer pinned changes nothing a test can see
            handle.Free();
        }
    }

    /// <summary>Returns <see cref="Pixels"/> row by row, as premultiplied BGRA.</summary>
    internal static int[] GetPixels()
    {
        var size = Pixels.Length;
        var pixels = new int[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                pixels[y * size + x] = Pixels[y][x] switch
                {
                    '#' => unchecked((int)0xFF000000),
                    'o' => unchecked((int)0xFFFFFFFF),
                    _ => 0,
                };
            }
        }
        return pixels;
    }
}
