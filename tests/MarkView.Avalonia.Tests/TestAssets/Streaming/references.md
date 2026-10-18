# Release notes with references

This release follows the [migration guide][guide] and the [API reference][api]. If you skipped the
previous version, read [the changelog] first, and see [Avalonia] for the framework itself.

The HTML export now honours the CSS of the host theme[^export], and every MCP server listed in the
[registry][] is supported. The logo below comes from the [project site][site]:

![MarkView logo][logo]

## What changed

- Links written as [reference links][guide] resolve once their definition arrives.
- Footnotes such as this one[^streaming] render in their own section at the end.
- Abbreviations like HTML, CSS and MCP show their meaning on hover.
- Images use a [reference definition][logo] too.

| Feature | Status | Notes |
|---|---|---|
| Reference links | Done | See [guide] |
| Footnotes | Done | Rendered last |
| HTML export | Beta | Uses CSS from the theme |

```csharp
var session = viewer.BeginStreaming();
session.Append("[text][ref]\n\n[ref]: https://example.com");
session.Complete();
```

1. Define each reference once.
2. Use it as `[text][ref]`, `[ref][]` or `[ref]`.
3. Put the definitions anywhere, even after the last paragraph.

> [!NOTE]
> A quote can use a reference as well: [the changelog].

Thanks to everyone who reported issues on the [tracker][issues].

[^export]: The export reads the theme's brushes when it runs, so a dark theme exports dark.
[^streaming]: Footnote definitions, like link definitions, can arrive at the very end of a stream.

*[HTML]: Hyper Text Markup Language
*[CSS]: Cascading Style Sheets
*[MCP]: Model Context Protocol

<!-- The link definitions come last, the longest at the very end: a chunk boundary inside a URL must
     be followed by updates that add no definition, so a block kept with the partial URL would show. -->

[guide]: https://example.com/docs/migration "Migration guide"
[api]: https://example.com/api/reference 'API reference'
[the changelog]: https://example.com/changelog (Changelog)
[Avalonia]: https://avaloniaui.net "Avalonia UI"
[site]: https://example.com/markview "MarkView"
[logo]: https://example.com/images/logo.png "The MarkView logo"
[registry]: https://example.com/registry/servers?category=documentation&transport=stdio&sort=name&order=ascending&page=1&per_page=100
[issues]: https://example.com/issues?state=open&sort=newest&labels=streaming,rendering,links&milestone=next-release&assignee=none&per_page=100
