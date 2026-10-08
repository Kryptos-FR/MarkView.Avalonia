// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;

namespace MarkView.Avalonia.Tests;

/// <summary>
/// A minimal <see cref="MarkdownViewer"/> template that hosts the content in a
/// <c>PART_ScrollViewer</c>, without loading the theme resources. It omits the shipped theme's
/// Padding-to-Margin binding on the content presenter and the ScrollViewer background binding.
/// </summary>
internal static class ScrollingViewerTemplate
{
    public static FuncControlTemplate Create() => new((_, scope) =>
    {
        var presenter = new ContentPresenter { Name = "PART_ContentPresenter" };
        presenter.Bind(ContentPresenter.ContentProperty, new TemplateBinding(ContentControl.ContentProperty));
        return new ScrollViewer { Name = "PART_ScrollViewer", Content = presenter }.RegisterInNameScope(scope);
    });
}
