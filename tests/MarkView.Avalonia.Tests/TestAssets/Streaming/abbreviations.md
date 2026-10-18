# Glossary of a streamed answer

The viewer renders HTML-like markdown with a GPU-backed renderer. Every MCP server it talks to
returns JSON, and the UI thread applies each update. A link to the [spec][] and a footnote[^1]
are defined before the abbreviations below.

- The HTML export keeps the CSS of the host theme.
- An MCP tool returns JSON or plain text.
- The GPU draws what the UI thread laid out.

| Term | Where it appears |
|---|---|
| HTML | Export |
| JSON | MCP responses |

```text
GPU and UI inside a code block are not abbreviations.
```

> [!TIP]
> A quote can mention the GPU too.

[spec]: https://example.com/spec "The specification"
[^1]: Footnotes and link definitions come before the abbreviations here.

<!-- The abbreviations come last, with long texts: a chunk boundary inside a definition's text must be
     followed by updates that add no other definition, so a paragraph kept with the partial text
     would show. -->

*[HTML]: Hyper Text Markup Language, the language of web pages and of this viewer's export
*[CSS]: Cascading Style Sheets, the styling language the export copies from the host theme
*[MCP]: Model Context Protocol, the protocol between an assistant and the tools it calls
*[JSON]: JavaScript Object Notation, the text format MCP servers return their results in
*[GPU]: Graphics Processing Unit, the processor that draws the rendered document on screen
*[UI]: User Interface, everything the user sees and interacts with in the application window
