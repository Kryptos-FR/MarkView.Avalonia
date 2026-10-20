# Right-to-left text

Hebrew, Arabic and other right-to-left documents render the way they read: each block of the
document takes the direction of its own text, so a page can mix right-to-left and left-to-right
paragraphs, and code always stays left to right.

## How the direction is chosen

Each paragraph, heading, list, quote, alert, table and details block takes the direction of the
first letter of its text that has one, as HTML's `dir="auto"` does (rules P2 and P3 of the
[Unicode Bidirectional Algorithm](https://www.unicode.org/reports/tr9/)):

- A Hebrew, Arabic, Syriac… letter makes the block right to left: it starts on the right, list
  markers and the quote bar move to the right, and table columns run from right to left.
- A Latin, Cyrillic, Greek or CJK letter makes it left to right.
- Digits, punctuation, symbols and emoji decide nothing: a block made only of them (`2026`,
  `---`) takes the direction of the block around it, or the viewer's own `FlowDirection` at the
  top level.

A list, a quote, an alert or a table takes one direction as a whole, from its first block that has
one, so its markers, bar or columns stay on one side. A paragraph inside it whose own text reads the
other way keeps its own direction and is aligned toward the marker or the start of the cell, as a
browser lays out `dir="auto"` blocks inside a list.

Unlike a browser, the text of code spans, math, autolinks, raw HTML tags and images is skipped
while looking for the first letter: it is technical text that rarely says which way the prose
around it reads, so a Hebrew paragraph that opens with `` `npm install` `` stays right to left.

## Code

Code blocks, math blocks, Mermaid diagrams and front matter are always left to right, wherever they
are. Inline code inside right-to-left text keeps its own order too: `foo()` shows as `foo()`, not
`()foo`. The viewer wraps it in a Unicode left-to-right isolate (U+2066…U+2069), the way HTML isolates
an element that has its own direction, so the sentence around it keeps its order: in
`` הריצו `foo()` 3 פעמים ``, the `3` stays after the code in reading order. Copying leaves the isolate
characters out.

## The HTML `dir` attribute

With the [HTML subset](configuration.md#html-subset), `dir="rtl"` and `dir="ltr"` set a direction
whatever the text says:

- On `<p>` and `<h1>`…`<h6>`, the direction of that paragraph or heading.
- On `<div>`, the direction of the blocks inside it that have no letter with a direction. Markdown
  blocks inside it that do have one still take the direction of their own text, as on GitHub, which
  renders every markdown block with `dir="auto"`.

Other values (`dir="auto"`) are ignored, since detection already does what they ask. A block with a
`dir` attribute is skipped when the block around it looks for its first letter, as HTML skips the
elements that have their own `dir`.

```markdown
<p dir="rtl">This English paragraph starts on the right.</p>
```

## Turning detection off

```xml
<mv:MarkdownViewer DetectTextDirection="False" />
```

Without detection, the whole document takes the viewer's own `FlowDirection` (inherited from its
parent, left to right by default), so an application that only shows right-to-left documents can
set `FlowDirection="RightToLeft"` on the viewer instead. Code blocks stay left to right and the
`dir` attribute still applies. Changing `DetectTextDirection` re-renders the document.

A custom pipeline renderer exposes the same switch as `AvaloniaRenderer.DetectTextDirection`.

## Table alignment

A column alignment in a pipe table (`:---`, `---:`) stays where it says: a left-aligned column is
aligned left even in a right-to-left table, as on GitHub. A column without an alignment starts on
the side the table starts on.

## Fonts

The viewer draws text with the fonts the application gives Avalonia. On the desktop, the system's
fonts usually cover Hebrew and Arabic. In the browser (WebAssembly), Avalonia has only the fonts the
application embeds, so letters the embedded font lacks show as boxes: embed a font for each script
the documents use and register it as a fallback. The demo's `DemoFonts.WithRightToLeftFonts()` does
this with Noto Sans Hebrew and Noto Sans Arabic, cut down to those scripts:

```csharp
AppBuilder.Configure<App>()
    .WithInterFont()
    .ConfigureFonts(fontManager => fontManager.AddFontCollection(
        new EmbeddedFontCollection(new Uri("fonts:Noto"), new Uri("avares://MyApp/Assets/Fonts"))))
    .With(new FontManagerOptions
    {
        FontFallbacks =
        [
            new FontFallback { FontFamily = new FontFamily("fonts:Noto#Noto Sans Hebrew") },
            new FontFallback { FontFamily = new FontFamily("fonts:Noto#Noto Sans Arabic") },
        ],
    });
```

## Limitations

- The direction is chosen per block, not per line or per sentence: a right-to-left phrase inside a
  left-to-right paragraph is ordered by the bidirectional algorithm within that paragraph.
