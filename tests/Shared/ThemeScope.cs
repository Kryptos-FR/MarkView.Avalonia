// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Styling;

namespace MarkView.Avalonia.Tests.Shared;

/// <summary>
/// Sets <see cref="Application.RequestedThemeVariant"/> for the duration of a test and restores
/// the previous value on dispose, so theme switches never leak into other tests.
/// </summary>
internal sealed class ThemeScope : IDisposable
{
    private readonly ThemeVariant? _saved;

    public ThemeScope(ThemeVariant initial)
    {
        _saved = Application.Current!.RequestedThemeVariant;
        Application.Current.RequestedThemeVariant = initial;
    }

    public void Switch(ThemeVariant variant) => Application.Current!.RequestedThemeVariant = variant;

    public void Dispose() => Application.Current!.RequestedThemeVariant = _saved;
}
