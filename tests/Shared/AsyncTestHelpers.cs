// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Threading;

namespace MarkView.Avalonia.Tests.Shared;

/// <summary>
/// Dispatcher-pumping waits for headless tests whose code under test completes on a
/// background thread and posts its result back to the UI thread.
/// </summary>
internal static class AsyncTestHelpers
{
    /// <summary>Pumps the UI dispatcher until <paramref name="condition"/> holds or the timeout elapses.</summary>
    public static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5_000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (!condition())
        {
            if (Environment.TickCount64 > deadline)
                throw new TimeoutException($"Condition not met within {timeoutMs} ms.");
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
    }

    /// <summary>
    /// Pumps the UI dispatcher for a fixed number of short iterations. Only for asserting that
    /// something does NOT happen; positive assertions use <see cref="WaitUntilAsync"/>.
    /// </summary>
    public static async Task PumpAsync(int iterations = 20)
    {
        for (var i = 0; i < iterations; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
    }
}
