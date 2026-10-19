// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Interactivity;

namespace MarkView.Avalonia.Demo.MermaidStyles;

/// <summary>Asks for <see cref="MermaidStylesDialog"/> to compare the styles of a diagram.</summary>
/// <param name="code">The diagram's Mermaid source, without its fence.</param>
public sealed class MermaidStylesRequestedEventArgs(string code) : RoutedEventArgs(MermaidStylesDialog.RequestedEvent)
{
    /// <summary>The diagram's Mermaid source, without its fence.</summary>
    public string Code { get; } = code;
}
