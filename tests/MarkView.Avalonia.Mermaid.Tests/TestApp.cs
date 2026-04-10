// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(MarkView.Avalonia.Mermaid.Tests.TestApp))]

namespace MarkView.Avalonia.Mermaid.Tests;

public class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
