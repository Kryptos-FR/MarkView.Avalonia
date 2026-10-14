// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using TextMateSharp.Grammars;
using TextMateSharp.Registry;

namespace MarkView.Avalonia.SyntaxHighlighting;

/// <summary>
/// Loads TextMate grammars by language id and caches them. Grammars do not depend on the colour
/// theme, so the dark and light highlighters share one instance and each grammar is parsed and
/// compiled only once.
/// </summary>
internal sealed class TextMateGrammars
{
    private readonly Registry _registry;

    // Grammar cache: language id → grammar (null = unsupported)
    private readonly Dictionary<string, IGrammar?> _grammarCache = new(StringComparer.OrdinalIgnoreCase);

    public TextMateGrammars(ThemeName theme)
    {
        Options = new RegistryOptions(theme);
        _registry = new Registry(Options);
    }

    public RegistryOptions Options { get; }

    public IGrammar? Get(string language)
    {
        if (_grammarCache.TryGetValue(language, out var cached))
            return cached;

        var scopeName = Options.GetScopeByLanguageId(language);
        // Stryker disable once block : LoadGrammar(null) returns null and the next line caches it, so result and caching are identical; only one extra LoadGrammar call differs
        if (scopeName == null)
        {
            _grammarCache[language] = null;
            return null;
        }

        var grammar = _registry.LoadGrammar(scopeName);
        _grammarCache[language] = grammar;
        return grammar;
    }
}
