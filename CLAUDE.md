# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

MarkView.Avalonia is a Markdig-powered markdown viewer control for Avalonia UI v12. See [README.md](README.md) for the public API and feature list, and [docs/README.md](docs/README.md) for the full documentation index (getting started, configuration, extensions, theming, text selection, custom extensions).

## Build and test

```bash
dotnet restore --locked-mode        # CI uses locked NuGet lock files — regenerate with `dotnet restore --force-evaluate` if a PackageVersion changes
dotnet build
dotnet test                                                                              # run all tests (headless Avalonia)
dotnet test --project tests/MarkView.Avalonia.Tests --filter-class "*.MarkdownViewer*"   # run matching classes
dotnet test --project tests/MarkView.Avalonia.Tests --filter-method "*Avares_source*"    # run matching methods
```

- `dotnet test` runs in Microsoft.Testing.Platform mode with the xUnit v3 runner: filter with `--filter-class` / `--filter-method` / `--filter-namespace` (`*` wildcards at either end). VSTest-style `--filter "FullyQualifiedName~X"` and MSBuild switches such as `-nodeReuse:false` are forwarded to the test host, which then reports "Zero tests ran" (exit code 5) — set `MSBUILDDISABLENODEREUSE=1` in the environment instead.
- Target framework: net10.0, nullable enabled, implicit usings (`Directory.Build.props`).
- Package versions are centrally managed in `Directory.Packages.props` — add new deps there, not inline `Version=` attributes in `.csproj`.
- Test runner: xUnit v3 + `Avalonia.Headless.XUnit`. **Any test that constructs an Avalonia object must use `[AvaloniaFact]`/`[AvaloniaTheory]`**, not `[Fact]`/`[Theory]` — even a bare property set on `TextBlock`/`StackPanel`/`Run` touches `AvaloniaPropertyDictionaryPool` and throws `IndexOutOfRangeException` without platform init. Plain `[Fact]` is only safe when a test constructs zero Avalonia objects (e.g. `SlugGeneratorTests`, `TextMateHighlighterTests`).
- Known upstream flake: `AvaloniaUI/Avalonia#20664` — headless test session cleanup can resume on a different thread, causing sporadic `Dispatcher.VerifyAccess()` failures in `[AvaloniaFact]` tests.
- Coverage via `Microsoft.Testing.Extensions.CodeCoverage` (`testconfig.json`, passed explicitly with `--config-file` in the workflow — routing it through `TestingPlatformCommandLineArguments` in `Directory.Build.props` is NOT honored by this SDK's `dotnet test` MTP mode), measured by the Ubuntu job of `.github/workflows/build.yml`.
- Mutation testing via Stryker (`dotnet-stryker`, pinned in `.config/dotnet-tools.json`, one `stryker-config.json` per test project), reported by `.github/workflows/mutation.yml`. On pull requests it only runs for the packages the PR touches, with `--since` (only mutants in changed files and those covered by changed tests); pushes to main run it in full.
- CI (`.github/workflows/ci.yml`): a `changes` job plans the run from the changed paths, and the **Gate** job is the single required check (skipped jobs count as passing). Docs-only PRs run nothing; drafts only build and test on Ubuntu. Labels: `ci-full` (AOT on all OSes and a full mutation run; `pr-labels.yml` runs it as soon as the label is added) and `ci-run-on-draft` (normal suites on a draft, from its next push). Publishing a GitHub release builds the tag and pushes the packages to NuGet.org.
- Test project pattern: inherit `RenderTestBase`, call `Render(markdown)`, and traverse the returned root `StackPanel → children` with `Assert.IsType<T>()` / `.OfType<T>()`. When testing through a full `MarkdownViewer` instead, `viewer.Content` is the rendered `Grid` directly — traverse `Grid → StackPanel → children`; the `ScrollViewer` (`PART_ScrollViewer`) lives in the control's `ControlTemplate`, not in `Content`, and is only reachable via `OnApplyTemplate`/`GetVisualDescendants()` once the control has a template applied (e.g. the `MarkdownTheme.axaml` include). `TestApp.cs` bootstraps `FluentTheme` + `AvaloniaHeadlessPlatformOptions` — copy it when adding a new test project.

## Architecture

Five independently-shipped NuGet packages under `src/`, each with a matching project under `tests/`:

| Package | Role |
|---|---|
| `MarkView.Avalonia` | Core control (`MarkdownViewer`), renderer pipeline, default theme |
| `MarkView.Avalonia.SyntaxHighlighting` | TextMate grammar highlighting; replaces `CodeBlockRenderer` |
| `MarkView.Avalonia.Svg` | SVG image loading; inserted at the front of the image loader chain |
| `MarkView.Avalonia.Mermaid` | Mermaid diagrams rendered to SVG via `Mermaider` (pure .NET, no browser) |
| `MarkView.Avalonia.Math` | LaTeX (`$…$`/`$$…$$`) rendered to bitmaps via `CSharpMath.SkiaSharp`; opt-in (`UseMath()`) since `$` is common in plain prose |

Rendering pipeline (markdown string → control tree):

```
ImageSizePreprocessor  ("![alt](url =WxH)" normalisation)
  → Markdig.Parse(text, pipeline)
  → IMarkViewExtension[].Register(renderer)   ← extension packages plug in here
  → pipeline.Setup(renderer)
  → AvaloniaRenderer.Render(document)
  → Grid { StackPanel (root), DocumentSelectionLayer, LinkFocusLayer }   ← this becomes MarkdownViewer.Content
```

`Content` is hosted by `PART_ScrollViewer`, the named part inside `MarkdownViewer`'s default `ControlTemplate` (shipped in `MarkdownTheme.axaml`); `viewer.Content` itself is the `Grid`, not a `ScrollViewer`.

`RenderMarkdown()` runs `CreateRenderer()` → render → `InstallDocument()`; a streaming session (`BeginStreaming()`) replaces the middle step with `Rendering/LiveDocument.cs`, which re-renders only the open top-level blocks and truncates the renderer and both overlay layers by per-block counts; unless `BeginStreaming(healIncompleteSyntax: false)`, each update first heals the text (`Rendering/MarkdownHealer.cs` finds the tail block of the current parse, a paragraph or ATX heading, and appends the closers of its open inline syntax to the parse input only, with a second parse only when closers were added); independently of healing, each update sets `AvaloniaRenderer.ProvisionalStart` so `IsProvisional` is true for the block that contains the end of the text and the blocks around it (the final update heals nothing and renders nothing as provisional). The Mermaid and Math block renderers render a provisional block as a plain code block (Mermaid's own standard path; Math delegates to the renderer that would otherwise accept the fenced block).

- `AvaloniaRenderer` (`src/MarkView.Avalonia/Rendering/AvaloniaRenderer.cs`) extends Markdig's `RendererBase` and owns a push/pop stack of `IContainer` — either a `BlockContainer` (wraps a `Panel`) or an `InlineContainer` (wraps an `InlineCollection`). Block renderers call `WriteBlock`, inline renderers call `WriteInline`.
- Block renderers live in `Rendering/Blocks/`, inline renderers in `Rendering/Inlines/`. Registration order in `AvaloniaRenderer.LoadRenderers()` matters when one Markdig type extends another — e.g. `AlertBlockRenderer` must precede `QuoteBlockRenderer` because `AlertBlock` extends `QuoteBlock` and Markdig dispatches to the first renderer whose `Accept()` matches. Extension packages that share a base Markdig type (`MathExtension`/`MermaidExtension`, both fencing off `FencedCodeBlock`) sidestep this by scanning for the first renderer that would already accept their type and inserting just before it, so behavior is independent of `.UseXxx()` call order.
- Extension packages implement `IMarkViewExtension.Register(AvaloniaRenderer)` and are activated per-viewer via `viewer.UseXxx()` or globally via `MarkdownViewerDefaults.Extensions.AddXxx()`. See [docs/custom-extensions.md](docs/custom-extensions.md) for the three extension points (renderers, image loaders, code highlighters) in detail.
- Image loading is deferred to `AttachedToVisualTree` to avoid cancelling in-flight loads during layout; the HTTP client is a shared static instance.
- `Source` and images read their bytes through `MarkdownViewer.ResourceResolver` (else `MarkdownViewerDefaults.ResourceResolver`), wrapped in `FallbackResourceResolver` so that a `null` answer falls back to `DefaultResourceResolver` (`avares`, `file`, `http(s)`; `Extensions/DefaultResourceResolver.cs`). Image loaders get it through the `IImageLoader.LoadAsync(url, resources, ct)` default interface method, and the image cache keys on it.
- Every link and image URL goes through `AvaloniaRenderer.ResolveUrl(url, kind)`, which resolves it against `BaseUri` and applies `UrlTransform` (viewer, else `MarkdownViewerDefaults`); `null` means dropped (a link renders as its text, an image as its failure placeholder). A clicked link no handler handled is launched only when its scheme is in `MarkdownViewerDefaults.LaunchableLinkSchemes` (`http`, `https`, `mailto`); tests replace the launcher through the internal `MarkdownViewer.LaunchUri`.
- In-viewer navigation and Back/Forward live in `MarkdownViewer.Navigation.cs`: a same-origin `.md`/`.markdown` link no handler handled becomes `Source` (`SetCurrentValue`), each successfully loaded `Source` and each followed heading link is a history entry that keeps its scroll offset; `OnSourceLoaded` records the entry, or restores the offset when Back/Forward set `Source`.
- Theme switching is live: an `IDualThemeCodeHighlighter` (the TextMate one) tokenises each line once with both variants' colours, and a theme switch only recolours the existing runs through shared per-block brushes (`Rendering/Blocks/HighlightedCode.cs`, used by both the TextMate and Mermaid code block renderers). A plain `IThemeAwareCodeHighlighter` gets its runs rebuilt via `HighlightVariant()` instead.
- Cross-block text selection is owned entirely by `DocumentSelectionLayer` (a transparent overlay `Control`), not by individual `TextBlock`s — see [docs/text-selection.md](docs/text-selection.md).
- Find in document (`MarkdownViewer.Find`/`FindNext`/`FindPrevious`) searches the same registered entries in `DocumentSelectionLayer`, which draws the matches under the selection — see [docs/find-in-document.md](docs/find-in-document.md).
- Links are `Span`s, so they can't take focus or have automation peers. `LinkFocusLayer` (another overlay) hosts one focusable `LinkFocusTarget` per link, kept over the link's text, and `MarkdownViewer` implements `ICustomKeyboardNavigation` to Tab through them in reading order. Each target's Hyperlink peer is reported as a child of its paragraph's peer — see [docs/accessibility.md](docs/accessibility.md).

## Conventions

- CSS-style class names: `markdown-*` (e.g. `markdown-h1`, `markdown-code-block`) — see [docs/theming.md](docs/theming.md) for the full list.
- Each package that needs overridable colours ships its own `Themes/<Package>Theme.axaml` (`AvaloniaResource` glob in the `.csproj`), included via `StyleInclude` — standalone, does not import `MarkdownTheme.axaml`. Code paths that can't rely on `DynamicResource` (e.g. Mermaid's SVG baked at render time) read the brush via `Application.Current.TryGetResource` and fall back to a hardcoded literal if the theme isn't included. `MarkView.Avalonia.Math` ships no theme file at all — formulas are baked to bitmaps via `CSharpMath.SkiaSharp`, so colours are picked directly from `Application.Current.ActualThemeVariant` and re-baked on theme change.
- Renderers rebuild baked-in colours (Mermaid SVG, Math bitmaps, theme-aware highlighter runs) via the public `ThemeTracking.ReapplyOnThemeChange(element, rebuild)`, which listens to the element's own inherited theme and, while the element is hidden (`IsEffectivelyVisible` false, e.g. a viewer on an inactive tab), waits for the next layout pass that finds it visible. Never subscribe to `Application.Current` events from a renderer — a document that is never shown or is replaced would never unsubscribe, so the application would keep it alive.
- Colours: every literal colour key in `MarkdownTheme.axaml` needs a value in `GitHubTheme.axaml` as well (`GitHubThemeContrastTests` fails otherwise), and the pairs it draws belong in `ThemeContrastTests` / `GitHubThemeContrastTests` (4.5:1 text, 3:1 borders that carry meaning, 7:1 for the `MarkdownHighContrast*` palette). The `:high-contrast` styles (`MarkdownViewer.HighContrastMode`) come last in each theme file so they win over the styles above them; a preset that recolours an element repeats them at its end — see [docs/accessibility.md](docs/accessibility.md#high-contrast).
- Renderers that emit non-text controls (images, formulas, diagrams) set the public `MarkdownSelection.CopyText` attached property so selection copies a text equivalent (alt text, `$…$`/`$$…$$` source, fenced Mermaid source). Block controls without it are skipped by selection; see [docs/text-selection.md](docs/text-selection.md).
- File naming: `<Element>Renderer.cs`, `<Feature>Extension.cs`, `<Feature>Highlighter.cs`. Namespaces mirror folder structure.
- Each package that exposes internals to tests grants `InternalsVisibleTo` in its `AssemblyInfo.cs` (core additionally grants `MarkView.Avalonia.Svg`, `MarkView.Avalonia.SyntaxHighlighting` and `MarkView.Avalonia.Mermaid`) — extend those lists rather than making things `public` just to reach them from a test/extension project.
- Shared test helpers (`WaitUntilAsync`, `ThemeScope`, `LoopbackHttpServer`) live in `tests/Shared/` and are compiled into every test project via `tests/Directory.Build.props`.
