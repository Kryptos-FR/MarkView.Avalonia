<think>
Okay, the user wants to know how to **stream** markdown into a viewer. Let me think about what they need.

First, the API:

- `BeginStreaming()` returns a session.
- `Append()` adds text, from any thread.
- `Complete()` applies the final update.

## Edge cases

What about a code block that mentions the closing tag?

```xml
<think>not the end</think>
</think>
```

> A quote, with a [link](https://example.com/streaming "Streaming") inside.
> It continues here.

1. Healing closes `inline code and **bold
2. Lists keep growing

Let me also check the heading anchors, like [the edge cases](#edge-cases).

So the answer should cover the session, healing and the final update.
</think>

Here is how to stream markdown into a `MarkdownViewer`:

1. Call `BeginStreaming()` to start a session.
2. `Append()` each chunk as it arrives.
3. Call `Complete()` at the end.

## Notes

Updates render only the blocks that changed.
