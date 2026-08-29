# Interface contract

What crosses the fence between Argus and the application, and what never does.

---

## The fence

Argus is a diagnostic library for geospatial entity streams. It observes samples and emits
findings. It does not know what the entities are, and that ignorance is load-bearing in two
directions: it is what makes the library reusable, and it is what makes it publishable.

```
   application  ──── EntitySample / TEntity ────▶  Argus.Core   ──── HealthFinding ────▶  application
                                                        │
                                                        └──── EntityHealthReport ────▶  Argus.Graphics ──▶ Color
```

## What crosses inward

| Type | Carries | Notes |
|---|---|---|
| `EntitySample` | position, attitude, velocity, timestamps, sequence number, raw fields | Every measurement field is nullable. **Unsupplied means `null`, never `0`.** |
| `TEntity` (any application type) | position and identity, read through an accessor | Argus never holds a reference to it beyond the call. |
| `MonitorOptions` | behaviour: history sizes, eviction, name candidates, accessors | Set once at composition. |
| `DetectorThresholds` | every number a detector compares against | Data, not code. Bind from configuration. |
| `GroupTickContext` | one tick's group statistics | Built once per tick by the caller. |

## What crosses outward

| Type | Carries | Notes |
|---|---|---|
| `HealthFinding` | flag, definition, measured, expected, outcome, detector id | Self-describing: readable without this repository. |
| `EntityHealthReport` | the findings, the flag unions, the counters | `Flags` and `NotEvaluableFlags` are separate and both matter. |
| `HealthFlags` | the conditions | A `[Flags]` enum. Values are permanent. |
| `EntityTrack` | what Argus remembers about an entity | Read-only to callers. |

## What never crosses

- **Colours, brushes, fonts, or any other UI type**, in either direction, through `Argus.Core`.
  `Argus.Core.Tests` asserts that Core's referenced assemblies include nothing presentational
  and that no exported type is named for presentation. Presentation lives in `Argus.Graphics`
  and consumes reports; nothing in Core consumes presentation.
- **Domain knowledge.** Argus has no notion of what kind of thing an entity is, what it is
  doing, or whether it should be doing it. Every judgement it makes is against a configured
  threshold or a mathematical invariant.
- **A default for a deployment-specific gate.** Thresholds whose right value depends on the
  deployment default to `null`, and the detectors that need them report `NotEvaluable` until
  they are configured. A plausible-looking library default would be a fabricated number that
  every consumer inherits and most never look at.
- **An invented position.** If Argus cannot resolve a position from an application type it
  throws `EntityAccessorException` at first use. It never substitutes zero. See below.

---

## Resolving a position from an application type

Three routes, tried in order.

**1. `TEntity : IArgusEntity`.** A cast and four property reads. Fastest, checked by the
compiler, and requires the application's model types to reference Argus.

**2. A delegate on `MonitorOptions.Accessors`.**

```csharp
options.Accessors.Register<MyEntity>(e => e.Id, e => e.Lat, e => e.Lon, e => e.Alt);
```

Use this when the model types cannot reference Argus, or when the runtime has no JIT — route 3
compiles expression trees and route 2 does not.

**3. Convention.** Public properties or fields matched, case-insensitively, against
`MonitorOptions.LatitudeCandidates`, `LongitudeCandidates`, `AltitudeCandidates` and
`IdentityCandidates`. Matched once per type, compiled into an expression tree, cached in a
`ConcurrentDictionary` on the options.

**If all three fail, Argus throws**, with a message naming the type, the candidates it tried and
the three fixes. It does not return `(0, 0)`.

That refusal is not fastidiousness. A fabricated position at the origin is a *legal-looking*
position: it enters group centroids and drags them, it trips jump detection on the following
tick, and it produces confident findings about an entity nobody ever measured. The library's
entire value is that its findings can be trusted; one invented position undermines that more
than a hundred missing ones.

Identity is resolved too, and its absence is not fatal but is not free either: without identity
the entity cannot be excluded from its own group centroid, so the group detectors report
`NotEvaluable` rather than silently comparing an entity against a centroid it is inside.

---

## The compatibility facade

`Argus.Graphics.IEntityHealthMonitor.SetStatusColor` exists to keep one existing application
call site compiling. Its shape is dictated by that call site and is not otherwise defensible:

- The `out` parameter sits at position nine because the original call passes it positionally
  after eight named arguments.
- The method is generic in both the identifier and the entity so that type inference keeps the
  call site character-identical.
- It rebuilds group state per call, cached by collection reference identity within a staleness
  window, because the call site is per entity and the group statistics are per tick.

`Argus.Graphics.Tests.RequiredCallSiteTests` holds the call site verbatim. Treat a compile
error there as a report that the facade has regressed.

**New code should use `IEntityStreamMonitor` directly**: build one `GroupTickContext` per tick,
call `Observe` per entity, and read the findings rather than a colour.

---

## Compatibility promises

- **`HealthFlags` values are permanent.** Never renumber; a persisted finding or a golden test
  locks them. New conditions take the next free bit.
- **The public API surface is locked** with `Microsoft.CodeAnalysis.PublicApiAnalyzers`.
  Changing it requires editing `PublicAPI.Unshipped.txt`, which makes "we will revisit the
  interface later" a deliberate act rather than drift. (See the backlog item in `CLAUDE.md`:
  the analyzer's diagnostics are suppressed until the baseline has been populated once.)
- **`Argus.Core` has no dependencies**, enforced from both sides — an MSBuild guard on the
  project and an assertion in `Argus.Package.Tests` against the packed nuspec.
- **`Argus.Graphics` depends only on `Microsoft.Maui.Graphics`**, the standalone netstandard2.0
  package, never `Microsoft.Maui.Controls`.
- **Core and Testing target `netstandard2.0` and `net8.0`; Graphics targets `netstandard2.0`.**
