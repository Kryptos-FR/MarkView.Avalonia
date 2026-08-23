# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

MarkView.Avalonia is a Markdig-powered markdown viewer control for Avalonia UI v12. See [README.md](README.md) for the public API and feature list, and [docs/README.md](docs/README.md) for the full documentation index (getting started, configuration, extensions, theming, text selection, custom extensions).

## Build and test

```bash
dotnet restore --locked-mode        # CI uses locked NuGet lock files — regenerate with `dotnet restore --force-evaluate` if a PackageVersion changes
dotnet build
dotnet test                                                # run all tests (headless Avalonia)
dotnet test --filter "FullyQualifiedName~MarkdownViewer"   # run a single class/method
```

- Target framework: net10.0, nullable enabled, implicit usings (`Directory.Build.props`).
- Package versions are centrally managed in `Directory.Packages.props` — add new deps there, not inline `Version=` attributes in `.csproj`.
- Test runner: xUnit v3 + `Avalonia.Headless.XUnit`. **Any test that constructs an Avalonia object must use `[AvaloniaFact]`/`[AvaloniaTheory]`**, not `[Fact]`/`[Theory]` — even a bare property set on `TextBlock`/`StackPanel`/`Run` touches `AvaloniaPropertyDictionaryPool` and throws `IndexOutOfRangeException` without platform init. Plain `[Fact]` is only safe when a test constructs zero Avalonia objects (e.g. `SlugGeneratorTests`, `TextMateHighlighterTests`).
- Known upstream flake: `AvaloniaUI/Avalonia#20664` — headless test session cleanup can resume on a different thread, causing sporadic `Dispatcher.VerifyAccess()` failures in `[AvaloniaFact]` tests.
- Coverage via coverlet (`coverlet.runsettings`), reported by `.github/workflows/coverage.yml`.
- Mutation testing via Stryker (`dotnet-stryker`, pinned in `.config/dotnet-tools.json`, one `stryker-config.json` per test project), reported by `.github/workflows/mutation.yml`.
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
  → Grid { StackPanel (root), DocumentSelectionLayer }   ← this becomes MarkdownViewer.Content
```

`Content` is hosted by `PART_ScrollViewer`, the named part inside `MarkdownViewer`'s default `ControlTemplate` (shipped in `MarkdownTheme.axaml`); `viewer.Content` itself is the `Grid`, not a `ScrollViewer`.

- `AvaloniaRenderer` (`src/MarkView.Avalonia/Rendering/AvaloniaRenderer.cs`) extends Markdig's `RendererBase` and owns a push/pop stack of `IContainer` — either a `BlockContainer` (wraps a `Panel`) or an `InlineContainer` (wraps an `InlineCollection`). Block renderers call `WriteBlock`, inline renderers call `WriteInline`.
- Block renderers live in `Rendering/Blocks/`, inline renderers in `Rendering/Inlines/`. Registration order in `AvaloniaRenderer.LoadRenderers()` matters when one Markdig type extends another — e.g. `AlertBlockRenderer` must precede `QuoteBlockRenderer` because `AlertBlock` extends `QuoteBlock` and Markdig dispatches to the first renderer whose `Accept()` matches. Extension packages that share a base Markdig type (`MathExtension`/`MermaidExtension`, both fencing off `FencedCodeBlock`) sidestep this by scanning for the first renderer that would already accept their type and inserting just before it, so behavior is independent of `.UseXxx()` call order.
- Extension packages implement `IMarkViewExtension.Register(AvaloniaRenderer)` and are activated per-viewer via `viewer.UseXxx()` or globally via `MarkdownViewerDefaults.Extensions.AddXxx()`. See [docs/custom-extensions.md](docs/custom-extensions.md) for the three extension points (renderers, image loaders, code highlighters) in detail.
- Image loading is deferred to `AttachedToVisualTree` to avoid cancelling in-flight loads during layout; the HTTP client is a shared static instance.
- Theme switching is live: `IThemeAwareCodeHighlighter.HighlightVariant()` updates tokens in place without a full document rebuild.
- Cross-block text selection is owned entirely by `DocumentSelectionLayer` (a transparent overlay `Control`), not by individual `TextBlock`s — see [docs/text-selection.md](docs/text-selection.md).

## Conventions

- CSS-style class names: `markdown-*` (e.g. `markdown-h1`, `markdown-code-block`) — see [docs/theming.md](docs/theming.md) for the full list.
- Each package that needs overridable colours ships its own `Themes/<Package>Theme.axaml` (`AvaloniaResource` glob in the `.csproj`), included via `StyleInclude` — standalone, does not import `MarkdownTheme.axaml`. Code paths that can't rely on `DynamicResource` (e.g. Mermaid's SVG baked at render time) read the brush via `Application.Current.TryGetResource` and fall back to a hardcoded literal if the theme isn't included. `MarkView.Avalonia.Math` ships no theme file at all — formulas are baked to bitmaps via `CSharpMath.SkiaSharp`, so colours are picked directly from `Application.Current.ActualThemeVariant` and re-baked on theme change.
- File naming: `<Element>Renderer.cs`, `<Feature>Extension.cs`, `<Feature>Highlighter.cs`. Namespaces mirror folder structure.
- `InternalsVisibleTo` in `src/MarkView.Avalonia/AssemblyInfo.cs` grants `MarkView.Avalonia.Tests` and `MarkView.Avalonia.Svg` access to internals — extend that list rather than making things `public` just to reach them from a new test/extension project.
