# Why your async method deadlocks

You're calling `.Result` on a task from a UI thread, which blocks the thread the continuation needs. Here is what happens step by step, and three ways to fix it.

## What happens

1. `LoadAsync()` starts and awaits an HTTP call.
2. The `await` captures the UI synchronization context so the continuation runs on the UI thread.
3. Your button handler calls `.Result`, which **blocks the UI thread** until the task completes.
4. The HTTP call finishes and posts its continuation to the UI thread, which is blocked. Deadlock.

## Fix 1: go async all the way

Make the handler `async void` (acceptable for event handlers only) and await the task:

```csharp
private async void OnClick(object? sender, RoutedEventArgs e)
{
    try
    {
        var data = await LoadAsync();
        Items.Clear();
        foreach (var item in data)
            Items.Add(item);
    }
    catch (HttpRequestException ex)
    {
        Status = $"Failed: {ex.Message}";
    }
}
```

This is the recommended fix because it keeps the UI responsive and the exception flow intact.

## Fix 2: avoid capturing the context

Inside library code, add `ConfigureAwait(false)` so the continuation does not need the UI thread:

```csharp
var response = await client.GetAsync(url).ConfigureAwait(false);
var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
```

> [!WARNING]
> This only helps if *every* await in the chain uses it. One missing call and the deadlock is back.

## Fix 3: run on the thread pool

As a last resort, wrap the call: `Task.Run(() => LoadAsync()).Result`. The task then completes on a pool thread and never needs the blocked one. It still blocks the UI, so prefer fix 1.

| Fix | Keeps UI responsive | Touches library code | Risk |
|---|---|---|---|
| Async all the way | Yes | No | Low |
| `ConfigureAwait(false)` | No | Yes | Medium |
| `Task.Run` wrapper | No | No | High |

## Summary

- Never block on a task from the UI thread.
- Prefer `async` event handlers over `.Result` or `.Wait()`.
- Use `ConfigureAwait(false)` in libraries, not as a fix for application deadlocks.

Let me know if you want me to look at the `LoadAsync` implementation itself; there may be a second capture inside it.
