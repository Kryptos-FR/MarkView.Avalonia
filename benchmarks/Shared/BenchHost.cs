// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia;

namespace MarkView.Benchmarks.Shared;

/// <summary>
/// Starts Avalonia once per process on the calling thread, with the Skia renderer and HarfBuzz text shaping
/// (not the headless stubs, whose text layout costs nothing and would hide most of the real work), the Inter
/// font for every library, and a 900×700 window that renders frames on demand.
/// </summary>
public static class BenchHost
{
    public const double WindowWidth = 900;
    public const double WindowHeight = 700;

    private static bool _started;

    public static void EnsureStarted(Action<Styles> addStyles)
    {
        if (_started)
            return;
        _started = true;
        BenchApp.AddStyles = addStyles;
        AppBuilder.Configure<BenchApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont()
            .With(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter" })
            .SetupWithoutStarting();
    }

    public static Window CreateWindow()
    {
        var window = new Window { Width = WindowWidth, Height = WindowHeight };
        window.Show();
        Settle();
        return window;
    }

    /// <summary>
    /// Runs the dispatcher and renders frames until <paramref name="isSettled"/> holds (and for at least two
    /// frames, so that work posted by the first layout pass is done too). A frame here is what a real window
    /// does on its render tick: layout, then drawing the scene with Skia into an off-screen surface.
    /// </summary>
    public static void Settle(Func<bool>? isSettled = null, int minFrames = 2)
    {
        var timeout = Stopwatch.StartNew();
        for (var frame = 0; ; frame++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            if (frame + 1 >= minFrames && (isSettled is null || isSettled()))
                return;
            if (timeout.Elapsed > TimeSpan.FromSeconds(60))
                throw new TimeoutException("The viewer did not settle within 60 s.");
            if (isSettled is not null && frame >= minFrames)
                Thread.Sleep(1); // background work (parsing on a thread pool) needs the CPU
        }
    }

    private sealed class BenchApp : Application
    {
        public static Action<Styles>? AddStyles;

        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
            AddStyles?.Invoke(Styles);
        }
    }
}
