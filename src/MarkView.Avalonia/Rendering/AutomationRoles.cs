// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Gives rendered controls the roles assistive technology needs to understand a document's
/// structure. Panels and borders get a <see cref="NoneAutomationPeer"/> by default, which keeps
/// them out of the control and content views a screen reader walks.
/// </summary>
internal static class AutomationRoles
{
    /// <summary>Exposes <paramref name="control"/> to screen readers as <paramref name="role"/>.</summary>
    internal static void Set(Control control, AutomationControlType role)
    {
        AutomationProperties.SetControlTypeOverride(control, role);
        AutomationProperties.SetAccessibilityView(control, AccessibilityView.Content);
    }

    /// <summary>Hides a purely visual control, such as a bullet, from screen readers.</summary>
    internal static void Hide(Control control) =>
        AutomationProperties.SetAccessibilityView(control, AccessibilityView.Raw);
}
