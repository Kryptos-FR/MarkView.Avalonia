// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;

namespace MarkView.Avalonia.Demo;

public static class DemoFonts
{
    /// <summary>
    /// Adds Noto Sans Hebrew and Noto Sans Arabic (Regular and Bold, cut down to their scripts) as fallbacks
    /// for the letters Inter lacks. A browser build only has the fonts the application embeds, so without
    /// them the right-to-left page would show boxes; a desktop build would otherwise depend on the system's fonts.
    /// </summary>
    public static AppBuilder WithRightToLeftFonts(this AppBuilder builder) => builder
        .ConfigureFonts(fontManager => fontManager.AddFontCollection(
            new EmbeddedFontCollection(new Uri("fonts:Noto"), new Uri("avares://MarkView.Avalonia.Demo/Assets/Fonts"))))
        .With(new FontManagerOptions
        {
            FontFallbacks =
            [
                new FontFallback { FontFamily = new FontFamily("fonts:Noto#Noto Sans Hebrew") },
                new FontFallback { FontFamily = new FontFamily("fonts:Noto#Noto Sans Arabic") },
            ],
        });
}
