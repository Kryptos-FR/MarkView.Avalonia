// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;

namespace MarkView.Avalonia;

public partial class MarkdownViewer
{
    /// <summary>
    /// Defines the <see cref="IsNavigationEnabled"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsNavigationEnabledProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(IsNavigationEnabled), true);

    /// <summary>
    /// Defines the <see cref="CanGoBack"/> property.
    /// </summary>
    public static readonly DirectProperty<MarkdownViewer, bool> CanGoBackProperty =
        AvaloniaProperty.RegisterDirect<MarkdownViewer, bool>(nameof(CanGoBack), o => o.CanGoBack);

    /// <summary>
    /// Defines the <see cref="CanGoForward"/> property.
    /// </summary>
    public static readonly DirectProperty<MarkdownViewer, bool> CanGoForwardProperty =
        AvaloniaProperty.RegisterDirect<MarkdownViewer, bool>(nameof(CanGoForward), o => o.CanGoForward);

    /// <summary>A place the viewer went to: a document, and where it was scrolled when the reader left it.</summary>
    private sealed class HistoryEntry(Uri? source)
    {
        /// <summary>The document's <see cref="Source"/>, fragment included; <c>null</c> for the <see cref="Markdown"/> text.</summary>
        public Uri? Source { get; } = source;

        public Vector Offset { get; set; }
    }

    private readonly List<HistoryEntry> _history = [];
    private int _historyIndex = -1;
    // Set while Back or Forward sets Source: the load restores this offset rather than being recorded.
    private Vector? _historyOffset;

    /// <summary>
    /// Gets or sets whether a clicked link to another Markdown document (a path ending in <c>.md</c> or
    /// <c>.markdown</c>) of the same origin as the current one (same scheme and host as
    /// <see cref="BaseUri"/>, or as the folder of <see cref="Source"/>) opens in the viewer, as a new
    /// <see cref="Source"/>, rather than with the platform's launcher; and whether the mouse's back and
    /// forward buttons, <c>Alt+Left</c>/<c>Alt+Right</c> (<c>Cmd+[</c>/<c>Cmd+]</c> on macOS) and the
    /// keyboard's browser keys call <see cref="GoBack"/> and <see cref="GoForward"/>. A
    /// <see cref="LinkClicked"/> handler that handles the click still comes first. Defaults to <c>true</c>.
    /// </summary>
    public bool IsNavigationEnabled
    {
        get => GetValue(IsNavigationEnabledProperty);
        set => SetValue(IsNavigationEnabledProperty, value);
    }

    /// <summary>
    /// Gets whether <see cref="GoBack"/> has somewhere to go: a document shown before the current one, or a
    /// place in the document before a link to one of its headings was followed.
    /// </summary>
    public bool CanGoBack
    {
        get;
        private set => SetAndRaise(CanGoBackProperty, ref field, value);
    }

    /// <summary>Gets whether <see cref="GoForward"/> has somewhere to go, after <see cref="GoBack"/>.</summary>
    public bool CanGoForward
    {
        get;
        private set => SetAndRaise(CanGoForwardProperty, ref field, value);
    }

    /// <summary>
    /// Goes back to where the reader was before the last navigation: the previous document, scrolled where
    /// it was left, or the place in the document a heading link jumped from. Every <see cref="Source"/>
    /// that loaded is recorded, whoever set it, and so is every followed link to a heading. Setting
    /// <see cref="Source"/> to <c>null</c>, or the <see cref="Markdown"/> shown without one, starts the
    /// history again. Does nothing when <see cref="CanGoBack"/> is <c>false</c>.
    /// </summary>
    public void GoBack()
    {
        if (CanGoBack)
            GoTo(_historyIndex - 1);
    }

    /// <summary>
    /// Goes forward again to where <see cref="GoBack"/> came from. Does nothing when
    /// <see cref="CanGoForward"/> is <c>false</c>.
    /// </summary>
    public void GoForward()
    {
        if (CanGoForward)
            GoTo(_historyIndex + 1);
    }

    private void GoTo(int index)
    {
        SaveHistoryOffset();
        _historyIndex = index;
        UpdateHistoryState();
        var entry = _history[index];
        if (Content is not null && IsSameDocument(entry.Source, Source))
        {
            RestoreOffsetAfterRender(entry.Offset);
            return;
        }

        _historyOffset = entry.Offset;
        try
        {
            SetCurrentValue(SourceProperty, entry.Source);
        }
        finally
        {
            _historyOffset = null;
        }
    }

    /// <summary>Keeps where the current document is scrolled, for Back or Forward to return to it.</summary>
    private void SaveHistoryOffset()
    {
        if (_historyIndex >= 0 && _scrollViewer is not null)
            _history[_historyIndex].Offset = _scrollViewer.Offset;
    }

    /// <summary>
    /// Records that <paramref name="source"/> is now shown, after the current entry; the entries Forward
    /// would have gone to are dropped. A document shown again as is (e.g. reloaded) is not recorded twice,
    /// unless <paramref name="always"/> is set, as for a heading link that stays in the document.
    /// </summary>
    private void RecordHistory(Uri? source, bool always = false)
    {
        // Uri.Equals ignores fragments, which tell entries apart here.
        if (!always && _historyIndex >= 0 && _history[_historyIndex].Source?.OriginalString == source?.OriginalString)
            return;
        _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        _history.Add(new HistoryEntry(source));
        _historyIndex = _history.Count - 1;
        UpdateHistoryState();
    }

    /// <summary>Starts the history again from the <see cref="Markdown"/> text, if any is shown.</summary>
    private void ResetHistory()
    {
        _history.Clear();
        _historyIndex = -1;
        if (!string.IsNullOrEmpty(Markdown))
            RecordHistory(null);
        UpdateHistoryState();
    }

    private void UpdateHistoryState()
    {
        CanGoBack = _historyIndex > 0;
        CanGoForward = _historyIndex < _history.Count - 1;
    }

    // Layout first: the offset is only reachable once the new document has been measured.
    private void RestoreOffsetAfterRender(Vector offset) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_scrollViewer is not null)
                _scrollViewer.Offset = offset;
        }, DispatcherPriority.Loaded);

    /// <summary>
    /// Follows a same-document link to <paramref name="anchorId"/>, recording where the reader was for
    /// Back. A link to an anchor the document does not have does nothing.
    /// </summary>
    private void NavigateToAnchor(string anchorId)
    {
        if (!_anchors.ContainsKey(anchorId))
            return;
        SaveHistoryOffset();
        RecordHistory(Source, always: true);
        ScrollToAnchor(anchorId);
    }

    /// <summary>
    /// Opens <paramref name="uri"/> in the viewer when it is a Markdown document of the same origin as the
    /// current one (see <see cref="IsNavigationEnabled"/>): a heading of the current document is scrolled
    /// to, any other document becomes <see cref="Source"/>. Returns whether it did.
    /// </summary>
    private bool TryNavigate(Uri uri)
    {
        if (!IsNavigationEnabled || (BaseUri ?? InferBaseUri(Source)) is not { } origin
            || !string.Equals(uri.Scheme, origin.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.Authority, origin.Authority, StringComparison.OrdinalIgnoreCase))
            return false;

        var (document, fragment) = SplitFragment(uri);
        if (!document.AbsolutePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            && !document.AbsolutePath.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase))
            return false;

        if (Source is not null && IsSameDocument(uri, Source) && Content is not null)
        {
            if (fragment.Length > 0)
            {
                NavigateToAnchor(fragment);
            }
            else
            {
                SaveHistoryOffset();
                RecordHistory(Source, always: true);
                if (_scrollViewer is not null)
                    _scrollViewer.Offset = default;
            }
            return true;
        }

        SetCurrentValue(SourceProperty, uri);
        return true;
    }

    /// <summary>Whether two sources are the same document, whatever their fragments.</summary>
    private static bool IsSameDocument(Uri? a, Uri? b) =>
        a is null || b is null
            ? a is null && b is null
            : a.IsAbsoluteUri && b.IsAbsoluteUri && SplitFragment(a).Document == SplitFragment(b).Document;

    /// <summary>
    /// Splits the fragment off an absolute <paramref name="uri"/>. Avalonia parses <c>avares://</c> with
    /// <c>GenericUriParserOptions.NoFragment</c>, which keeps <c>#fragment</c> in the resource path, so its
    /// fragment starts at the first <c>#</c> of the original string.
    /// </summary>
    private static (Uri Document, string Fragment) SplitFragment(Uri uri)
    {
        if (uri.Scheme == "avares")
        {
            var hashIndex = uri.OriginalString.IndexOf('#');
            return hashIndex < 0 ? (uri, "") : (new Uri(uri.OriginalString[..hashIndex]), uri.OriginalString[(hashIndex + 1)..]);
        }
        return uri.Fragment.Length == 0 ? (uri, "") : (new Uri(uri.GetLeftPart(UriPartial.Query)), uri.Fragment[1..]);
    }

    /// <summary>Goes back or forward for the mouse's back and forward buttons.</summary>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Handled || !IsNavigationEnabled)
            return;
        if (e.InitialPressMouseButton == MouseButton.XButton1 && CanGoBack)
        {
            GoBack();
            e.Handled = true;
        }
        else if (e.InitialPressMouseButton == MouseButton.XButton2 && CanGoForward)
        {
            GoForward();
            e.Handled = true;
        }
    }

    /// <summary>Handles the keys that go back or forward; returns whether <paramref name="e"/> was one.</summary>
    private bool HandleNavigationKey(KeyEventArgs e)
    {
        if (!IsNavigationEnabled)
            return false;
        var back = e.Key == Key.BrowserBack
            || (e.Key == Key.Left && e.KeyModifiers == KeyModifiers.Alt)
            || (e.Key == Key.OemOpenBrackets && e.KeyModifiers == KeyModifiers.Meta);
        var forward = e.Key == Key.BrowserForward
            || (e.Key == Key.Right && e.KeyModifiers == KeyModifiers.Alt)
            || (e.Key == Key.OemCloseBrackets && e.KeyModifiers == KeyModifiers.Meta);
        if (back && CanGoBack)
            GoBack();
        else if (forward && CanGoForward)
            GoForward();
        else
            return false;
        return true;
    }
}
