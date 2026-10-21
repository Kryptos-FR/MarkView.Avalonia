---
title: "Versioning"
---

# Versioning

All five packages are released together under the same version, `12.MAJOR.MINOR`:

| Part | Meaning |
|---|---|
| `12` | The Avalonia major version the packages are built for. It changes only with Avalonia's, so MarkView for Avalonia 13 will start at 13.0.0. |
| `MAJOR` | Incremented for breaking changes: a removed or renamed API, or a change of behaviour that needs code changes. |
| `MINOR` | Incremented for every other release: new features and fixes. |

NuGet and update tools such as Dependabot read the version as `MAJOR.MINOR.PATCH`, so they treat a breaking release (12.3 → 12.4) as a minor update. To receive only compatible updates, use a floating version that fixes the second number, such as `12.4.*`, rather than `12.*`.

The [release notes](https://github.com/Kryptos-FR/MarkView.Avalonia/releases) of each breaking release list what changed and how to update; [Upgrading to 12.4](../upgrading.md) gathers the changes of the latest one.
