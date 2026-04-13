# MarkView.Avalonia — Project Guidelines

A fully-featured, Markdig-powered markdown viewer control for Avalonia UI v12. Renders markdown as native Avalonia controls with a plugin-based extension architecture.

## Build and Test

```bash
dotnet build                    # build all projects
dotnet test                     # run all tests (headless Avalonia)
dotnet test --filter "FullyQualifiedName~MarkdownViewer"  # run specific tests
```

- **Framework:** .NET 10.0, nullable enabled, implicit usings
- **Test runner:** xUnit v3 + `Avalonia.Headless.XUnit`; use `[AvaloniaFact]` / `[AvaloniaTheory]` for UI tests
- **Coverage:** coverlet (see `coverlet.runsettings`)

## Architecture

Three independently-shipped packages in `src/`:

| Package | Role |
|---------|------|
| `MarkView.Avalonia` | Core control (`MarkdownViewer`), renderers, theme |
| `MarkView.Avalonia.SyntaxHighlighting` | TextMate grammar highlighting (replaces `CodeBlockRenderer`) |
| `MarkView.Avalonia.Mermaid` | Mermaid diagram blocks rendered as SVG via `Mermaider` (pure .NET, cross-platform) |

### Rendering pipeline

```
Markdown string
  → ImageSizePreprocessor  ("![alt](url =WxH)" normalisation)
  → Markdig.Parse(text, pipeline)
  → IMarkViewExtension[].Register(renderer)   ← extensions plug in here
  → pipeline.Setup(renderer)
  → AvaloniaRenderer.Render(document)
  → ScrollViewer { StackPanel (root) }
```

- **`AvaloniaRenderer`** extends `RendererBase`; manages a push/pop stack of `IContainer` (either `Panel` or `InlineCollection`).
- Block renderers live in `Rendering/Blocks/`, inline renderers in `Rendering/Inlines/`.
- To replace a renderer from an extension: `renderer.ObjectRenderers.ReplaceOrAdd<TRenderer>()`.

### Extension system

Implement `IMarkViewExtension` (single method: `void Register(AvaloniaRenderer)`):

```csharp
// Set a code highlighter
renderer.CodeHighlighter = new MyHighlighter();
// Prepend an image loader (priority — first CanLoad() match wins)
renderer.ImageLoaders.Insert(0, new MySvgLoader());
// Replace a block renderer
renderer.ObjectRenderers.ReplaceOrAdd<CodeBlockRenderer, MyCodeBlockRenderer>();
```

Activate on the viewer with a convenience extension method (pattern: `viewer.UseXxx()`).

## Conventions

- **CSS-style class names:** `markdown-*` (e.g. `markdown-h1`, `markdown-code-block`, `markdown-table-cell`)
- **File naming:** `<Element>Renderer.cs`, `<Feature>Extension.cs`, `<Feature>Highlighter.cs`
- **Namespaces** mirror folder structure
- **Default pipeline extensions** enabled by `UseSupportedExtensions()`: EmphasisExtras, AutoLinks, GridTables, PipeTables, TaskLists — see `MarkdownExtensions.cs`
- **Slug generation** follows GitHub style (kebab-case, Unicode-aware, deduped) via `SlugGenerator.cs`
- **Image loading** deferred to `AttachedToVisualTree` to avoid cancellation during layout; HTTP client is a shared static instance
- **Theme switching** is live: `IThemeAwareCodeHighlighter.HighlightVariant()` updates tokens in-place; no full redraw needed

## Styling

`Themes/MarkdownTheme.axaml` is an embedded resource. Consumers include it via:

```xml
<StyleInclude Source="avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml" />
```

Dark/light dictionaries use `DynamicResource`; monospace stack: Cascadia Code → Consolas → Courier New.

## Testing Patterns

- Inherit `RenderTestBase` and call `Render(markdown)` to get the root `StackPanel`.
- Traverse the control tree: `ScrollViewer → StackPanel → children`.
- Use `Assert.IsType<T>()`, `.OfType<T>()`, `Assert.Single()` for structural assertions.
- `TestApp.cs` bootstraps `FluentTheme` and `AvaloniaHeadlessPlatformOptions`; copy this pattern for new test projects.

## Known Limitations / Open TODOs

- **Cross-block text selection:** `DocumentSelectionLayer` (a transparent `Control` overlay in a single-cell `Grid`) owns all selection. Plain `TextBlock` children render text; the layer uses `TextBlock.TextLayout.HitTestPoint` / `HitTestTextRange` + `TranslatePoint` for coordinate mapping. `MarkdownViewer` exposes `SelectAll()`, `ClearSelection()`, `GetSelectedText()`, `CopyToClipboardAsync()`, and handles `Ctrl+A` / `Ctrl+C`. All block types are registered: paragraphs, headings, code blocks, blockquotes, list items (with markers), and table cells (tab-separated). Images and task checkboxes are skipped (inline `InlineUIContainer` — same as reference libraries).
- **`ISlugGenerator` injection:** Slug style is hardcoded to GitHub; exposing an interface for GitLab/Gitea/custom schemes is a TODO.

