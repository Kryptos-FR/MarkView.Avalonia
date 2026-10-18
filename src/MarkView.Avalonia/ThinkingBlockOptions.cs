// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace MarkView.Avalonia;

/// <summary>
/// Options for <see cref="MarkdownExtensions.UseThinkingBlocks"/>: which tags hold a language model's reasoning,
/// the titles of the block that shows it, and when it starts expanded.
/// </summary>
public sealed record ThinkingBlockOptions
{
    /// <summary>
    /// The names of the tags around the reasoning, matched without regard to case. Defaults to <c>think</c>
    /// (DeepSeek R1, Qwen, GLM and most open models), <c>thinking</c>, <c>reasoning</c>, <c>reason</c> and
    /// <c>thought</c>.
    /// </summary>
    public IReadOnlyList<string> TagNames { get; init; } = ["think", "thinking", "reasoning", "reason", "thought"];

    /// <summary>The title while the model is still thinking during a streaming session. Defaults to "Thinking…".</summary>
    public string ThinkingTitle { get; init; } = "Thinking…";

    /// <summary>
    /// The title once the reasoning is complete, when <see cref="DurationTitle"/> gives none. Defaults to "Thoughts".
    /// </summary>
    public string Title { get; init; } = "Thoughts";

    /// <summary>
    /// Formats the title of a block whose reasoning a streaming session saw start and end, from the time it
    /// took; <c>null</c> to always show <see cref="Title"/>. Defaults to <see cref="FormatDuration"/>
    /// ("Thought for 12 seconds").
    /// </summary>
    public Func<TimeSpan, string>? DurationTitle { get; init; } = FormatDuration;

    /// <summary>Whether the block is expanded while the model is still thinking. Defaults to <c>true</c>.</summary>
    public bool ExpandWhileThinking { get; init; } = true;

    /// <summary>
    /// Whether the block is expanded once the reasoning is complete, or in a document that is not streamed.
    /// Defaults to <c>false</c>: the block folds itself away when the answer starts. A block the reader expanded
    /// or collapsed keeps the state they chose.
    /// </summary>
    public bool ExpandWhenDone { get; init; }

    /// <summary>
    /// The default <see cref="DurationTitle"/>: "Thought for 1 second", "Thought for 12 seconds", or
    /// "Thought for 2m 5s" from a minute on.
    /// </summary>
    public static string FormatDuration(TimeSpan duration)
    {
        var seconds = Math.Max(1, (int)Math.Round(duration.TotalSeconds, MidpointRounding.AwayFromZero));
        if (seconds < 60)
            return seconds == 1 ? "Thought for 1 second" : $"Thought for {seconds} seconds";
        return $"Thought for {seconds / 60}m {seconds % 60}s";
    }
}
