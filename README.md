# GameAttr

[![License: MPL-2.0](https://img.shields.io/badge/License-MPL--2.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Commit Convention](https://img.shields.io/badge/Commit-Conventional%20Commits-F05032?logo=git)](https://www.conventionalcommits.org/)
[![Tests](https://img.shields.io/badge/Tests-MSTest-006600?logo=microsoft)](GameAttr.Tests)

> **A generic, decoupled game attribute library** — computes attribute values using the formula `FinalValue = BaseValue × (1 + PercentBonus) + FlatBonus`, a modifier model rooted in classic RPGs like **Dungeons & Dragons** and **Diablo II**. Thread-safe, fully tested, and designed for easy integration into any C# game project.

[中文文档](README.zh-CN.md)

---

## Features

- **Generic by design** — `Attr<TKey, TModId, TValue>` supports any key type, modifier ID type, and numeric value type (`INumber<T>`).
- **Three modifier types** — `BaseValue`, `PercentBonus`, `FlatBonus` cover the full stack of base, percentage, and flat modifiers.
- **Thread-safe** — per-key locking for concurrent modifier reads/writes, plus a global lock for batch operations.
- **Cached reads** — `GetValue` caches its result with a generation counter that auto-invalidates on writes; repeated reads of unchanged attributes cost near-zero contention.
- **Rich modifier removal** — remove by `(key, type, modId)`, `(key, modId)` across all types, or `(modId)` globally.
- **Change event system** — `AttributeChanged` event fires on every effective mutation; one subscriber's exception never blocks others.
- **Zero coupling** — pure logic library with no dependency on any game engine or framework. Run tests in isolation.
- **Fully documented** — XML doc comments on all public APIs.
- **100% test coverage** — MSTest suite covering base values, percent/flat bonuses, removal semantics, enum keys, overwrites, edge cases, and event behavior.

---

## Formula

```
FinalValue = BaseValue × (1 + PercentBonus) + FlatBonus
```

Where:

| Modifier | Meaning | Example |
|---|---|---|
| `BaseValue` | Sum of all base value modifiers | Initial ATK 1000 |
| `PercentBonus` | Sum of all percentage modifiers, applied additively | 0.3 → +30% |
| `FlatBonus` | Sum of all flat modifiers, added after percentage | +50 |

**Example:** if `BaseValue = 1000`, `PercentBonus = 0.3`, `FlatBonus = 50`:  
`FinalValue = 1000 × (1 + 0.3) + 50 = 1350`

---

## Quick Start

```xml
<!-- Add from NuGet or reference the project directly -->
<PackageReference Include="GameAttr" Version="1.0.0" />
```

```csharp
using GameAttr;

// Create an attribute container: string keys, string modifier IDs, float values
Attr<string, string, float> attr = new();

// Set base value
attr.SetModifier("atk", ModifierType.BaseValue, "base", 1000);

// Apply bonuses
attr.SetModifier("atk", ModifierType.PercentBonus, "buff1", 0.2);  // +20%
attr.SetModifier("atk", ModifierType.PercentBonus, "buff2", 0.1);  // +10%
attr.SetModifier("atk", ModifierType.FlatBonus, "flat", 50);       // +50

// Get computed value
float final = attr.GetValue("atk");  // 1000 * (1 + 0.3) + 50 = 1350

// Remove a modifier
attr.RemoveModifier("atk", ModifierType.PercentBonus, "buff1");

// Subscribe to attribute changes
attr.AttributeChanged += args =>
{
    Console.WriteLine($"[{args.Key}] {args.ChangeType} → {args.NewValue}");
};
```

---

## API Overview

### `Attr<TKey, TModId, TValue>`

| Method / Event | Description |
|---|---|
| `SetModifier(key, type, modId, value)` | Set or overwrite a modifier |
| `GetValue(key)` | Get the computed attribute value (cached — re-reads only when modifiers change) |
| `RemoveModifier(key, type, modId)` | Remove a specific modifier |
| `RemoveModifier(key, modId)` | Remove a modifier by ID across all types for a key |
| `RemoveModifier(modId)` | Remove a modifier by ID globally across all keys |
| `RemoveAllModifiers(key)` | Remove all modifiers for a key |
| `Clear()` | Remove all modifiers |
| `ToString()` | JSON snapshot of all modifiers |
| `AttributeChanged` | **Event** — fires when a modifier mutation changes the computed value (see below) |

### `ModifierType`

- **`BaseValue`** — foundation of the attribute; all bonuses are applied on top of the sum of base values.
- **`PercentBonus`** — percentage of the base value (e.g., `0.1` = +10%). Multiple percent bonuses stack additively.
- **`FlatBonus`** — flat value added after percentage calculation.

### `AttrChangeType`

| Value | Description |
|---|---|
| `SetModifier` | A modifier was set or overwritten, causing the value to change |
| `RemoveModifier` | A single modifier was removed, causing the value to change |
| `RemoveAll` | All modifiers for a key were removed |
| `Clear` | All modifiers for all keys were cleared |

### `AttrChangedEventArgs<TKey, TValue>`

| Property | Type | Description |
|---|---|---|
| `Key` | `TKey` | The attribute key that changed |
| `ChangeType` | `AttrChangeType` | What kind of change occurred |
| `NewValue` | `TValue` | The recomputed value after the change |

### Event Safety

- Fires **outside** per-key and global locks — subscribing handlers can safely call `GetValue` (which returns fresh data, as the cache is invalidated before the event fires).
- Each subscriber is invoked individually via `GetInvocationList()` — one subscriber's exception does **not** prevent others from receiving the event.
- Setting a modifier to the same value as before is a no-op and does **not** fire `AttributeChanged`.

---

## Thread Safety

- **Per-key locking** for `SetModifier`, `GetValue`, `RemoveModifier`, `RemoveAllModifiers` — concurrent operations on different keys never block each other.
- **Global lock** for `Clear()` and `RemoveModifier(modId)` — ensures atomic cross-key operations.
- **Event lock** — `AttributeChanged` add/remove is serialized with a dedicated lock; event invocation occurs **outside** per-key and global locks to prevent deadlocks when handlers call back into the attribute.
- **GetValue caching** — each read result is cached alongside a generation counter that increments on every write to the same key. Subsequent `GetValue` calls check the generation first; if unchanged, the cached value is returned without acquiring the write lock, minimizing contention in read-heavy scenarios.
- Backed by `ConcurrentDictionary` for lock-free reads where possible.

---

## Running Tests & Coverage

Prerequisites:

```bash
dotnet add package coverlet.collector
dotnet tool install --global dotnet-reportgenerator-globaltool
```

On **Windows**, run the convenience script:

```bash
cd GameAttr.Tests
.\run-coverage.cmd
```

This runs tests with coverage collection, generates an HTML report via ReportGenerator, and opens it in your browser.

Or manually:

```bash
dotnet test GameAttr.Tests/GameAttr.Tests.csproj --collect:"XPlat Code Coverage"
```

---

## License

[Mozilla Public License 2.0](LICENSE) © Suntion
