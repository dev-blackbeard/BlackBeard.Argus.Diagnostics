# CLAUDE.md — Argus (PUBLIC repository)

Argus is a diagnostic library for geospatial entity streams. It observes a stream of
6DOF samples (position, attitude, linear and angular velocity) and emits structured
findings describing what is wrong with the stream.

This repository is **public**. Everything below is a hard constraint, not a preference.

---

## Backlog

- [x] **Employer IP + disclosure sign-off.**
      Confirmed 2026-08-31: this is dev-blackbeard's own personal initiative, not a
      company or employer project, so no employer IP claim applies. Everything already
      here was authored fresh against the Part 0 rules below, with nothing ported from
      any private history.
- [ ] Lock the public API baseline: build once with a working SDK, apply the
      `RS0016` code fixes to populate `PublicAPI.Unshipped.txt`, then remove
      `RS0016;RS0017;RS0037` from `<NoWarn>` in `Directory.Build.props` so the
      analyzer is load-bearing (architecture rule 8).
- [ ] Implement the stubbed detectors (see `docs/detector-catalogue.md`; every stub
      carries a `// TODO(argus): <FLAG>` marker and throws `NotImplementedException`).
- [ ] Implement `FieldShiftDetector` — highest value in the catalogue. The reasoning
      is already written up in `docs/corruption-taxonomy.md`; the code is not.
- [ ] Extend `Argus.Golden.Tests` as detectors land. `PendingGoldenCases` is asserted
      to equal exactly the set of unimplemented detectors, so implementing one
      forces the golden case to move.
- [ ] Decide whether `Argus.Cli` ships as a `dotnet tool`.
- [ ] Add `net8.0-ios`/`net8.0-maccatalyst` to `Argus.Controls.Maui` once a macOS
      build agent is available. CI (`ubuntu-latest`/`windows-latest`) cannot build
      either TFM today — there is no Windows or Linux path to the iOS/MacCatalyst SDKs.
- [ ] Get `net8.0-windows10.0.19041.0` building for `Argus.Controls.Maui`. Attempted
      and reverted: the WinUI XAML compiler (`XamlCompiler.exe`, from the
      WindowsAppSDK `buildTransitive` targets) fails on `EntityHealthCollectionView.xaml`
      with `MSB3073` (exit code 1) and surfaces no further diagnostic anywhere in CI's
      log capture. `WindowsPackageType=None` was tried first on the theory that the
      compiler was treating the class library as a packaged app; it changed nothing —
      identical error, identical line. Needs a local repro with the real MAUI/WinUI
      tooling installed to get past "exit code 1" to an actual cause; six CI rounds of
      guessing stopped being worth it. Full failure history:
      `BlackBeard.Playbook`'s idiosyncrasies journal, §3.2.
      (Resolved by the same CI runs, so no longer open: whether `UseMaui=true`
      produces an explicit `Microsoft.Maui.Controls` `PackageReference` item —
      confirmed yes, since `ArgusGuardUiDependencies`' `PackageReference`-based scan
      caught it correctly and the Android build succeeded end to end.)

---

## 0. The public/private boundary

**No domain specifics ever enter this repository** — not in code, comments, test
fixtures, docs, sample data, commit messages, branch names or file names.

Hard denylist:

- Real geographic coordinates of any kind. Samples and tests use obviously synthetic
  origins — `(0.0, 0.0)` — or clearly fictional ones.
- Military, defence or force-disposition vocabulary: force colours, threat/hostile
  language, unit types, call signs, platform names, or "formation" used in a
  tactical sense.
- Real formation geometry, spacing values, entity counts or update rates.
- Tuned threshold values. Public defaults are neutral, conservative, and carry the
  reasoning for the number in XML docs. Deployment-specific gates default to `null`
  and report `NotEvaluable` until configured, rather than shipping a plausible number.
- Any employer name, internal team name, project codename, or protocol name.

Public vocabulary is neutral throughout: *entity*, *sample*, *group*, *stream*,
*origin*, *spacing*. Argus has no idea what the entities are. That is simultaneously
the discretion story and the reason it is reusable.

> If you are ever unsure whether something belongs in public, put it in the private
> repository. Moving code outward later is trivial; unpublishing is not.

Three words are structurally required by the design and are **not** tactical here:
`track` (the per-entity state record, `EntityTrack`/`TrackStore`), `formation`
(only in `FormationCollapse`, meaning "the group's geometric arrangement"), and
`contributor` (an entity that feeds the group centroid). `scripts/check-public-hygiene`
flags these for human review rather than failing on them; everything else on the
denylist fails the build.

`scripts/check-public-hygiene` is wired into CI and must stay green. It reads an
optional extra denylist from `$ARGUS_EXTRA_DENYLIST` — the file with the private
terms in it lives in the private repository and is passed in at CI time, never
committed here. A list of the words that must never appear in this repository would
itself be a disclosure if it were kept in it.

## 1. Dependency direction

A private, domain-specific repository consumes this one. **Never the reverse.** If
something here appears to need something from there, the abstraction is wrong — fix the
abstraction, by turning the thing the private side knows into an *input* on a public type.

This repository does not name the private one. Not in a path, not in a comment, not in a
CI file. Naming it would leak the one fact the separation is meant to keep, and it would
make this repository un-buildable on its own for anyone who clones only this half.

## 2. Architectural rules (non-negotiable)

1. `Argus.Core` emits **diagnostics**, never colours or UI strings. No UI type may
   appear in Core's public API.
2. `Argus.Core` has **zero** dependency `PackageReference` entries. An MSBuild target
   (`ArgusGuardNoPackageDependencies` in `Directory.Build.targets`) fails the build if
   one is introduced. Analyzer-only references carrying `PrivateAssets="all"` are
   permitted because they contribute nothing to the produced `.nupkg`; the guard
   enforces exactly that distinction, and `Argus.Package.Tests` re-checks it against
   the packed artifact.
3. Colour/label presentation and interactive UI are two different tiers, each with
   exactly one place it may live:
   - `Argus.Graphics` — passive colour/label primitives. Its sole external dependency
     is `Microsoft.Maui.Graphics`, the standalone netstandard2.0 package, **never**
     `Microsoft.Maui.Controls`.
   - `Argus.Controls` — portable per-entity view-model/aggregation state (uniqueness
     keying, cumulative per-flag counts, colour resolution) for a UI to bind to. Zero
     `Microsoft.Maui.Controls` dependency; it reaches `Color`/`ColorPolicy` only
     transitively, through a `ProjectReference` to `Argus.Graphics`.
   - `Argus.Controls.Maui` — the **one and only** project in this repository
     permitted to reference `Microsoft.Maui.Controls`. A thin MAUI `CollectionView`
     shell over `Argus.Controls`; it contains no diagnostics logic of its own.
   `ArgusGuardUiDependencies` in `Directory.Build.targets` enforces all three
   boundaries — including a check keyed on the `UseMaui` MSBuild property itself, not
   only on `PackageReference` items, since it isn't certain that property always
   produces one. Every other project, `Argus.Core` included, fails the build the
   moment it references either MAUI package or sets `UseMaui`.
4. `HealthFlags` is a `[Flags]` enum. Conditions are **not** mutually exclusive: an
   entity can be a group outlier *and* carry a non-normalised quaternion. Presentation
   picks a colour by severity precedence; **detection never suppresses**. No
   `else if` between detectors, ever.
5. Every threshold is a named, XML-documented member of `DetectorThresholds`. No
   magic numbers in detector bodies.
6. A detector lacking the fields it needs reports `NotEvaluable` — never "healthy".
   Unsupplied `EntitySample` fields are `null`, never `0`.
7. Findings are self-describing: each carries the flag name, a one-line
   human-readable definition, the measured value and the expected value/range. This
   is what makes a finding hard to dispute without repository access.
8. Public API surface is locked with `Microsoft.CodeAnalysis.PublicApiAnalyzers`, so
   "we'll revisit the interface later" is a deliberate act, not drift.
9. `Argus.Core` and `Argus.Testing` multi-target `netstandard2.0;net8.0`.
   `Argus.Graphics` and `Argus.Controls` target `netstandard2.0`. `Argus.Controls.Maui`
   is the one exception: `net8.0-android` only for now, because `Microsoft.Maui.Controls`
   cannot target netstandard2.0 at all. `net8.0-windows10.0.19041.0` was attempted and
   reverted — the WinUI XAML compiler fails on it with no diagnosable cause from CI alone
   (see backlog); `net8.0-ios`/`net8.0-maccatalyst` are separately blocked on this
   repository's CI having no macOS runner.
10. Deterministic builds, SourceLink, symbol packages, central package management.

### netstandard2.0 consequences

Core must compile for netstandard2.0, so: no `Math.Clamp`, no `double.IsFinite`, no
`System.HashCode`, no `Span<T>`, no `init` accessors (an `IsExternalInit` polyfill
would leak a compile error into netstandard2.0 consumers), no BCL nullable-analysis
attributes. Use `Geo.IsFinite`, explicit constructors and plain `get; set;` DTOs.

## 3. The compatibility facade

An existing application calls the monitor exactly like this, and this **must keep
compiling verbatim**:

```csharp
var StatusColor = _entityHealthMonitor.SetStatusColor(
    entityId: obj.Id,
    latitude: obj.LatitudeWgs84,
    longitude: obj.LongitudeWgs84,
    altitude: obj.Altitude,
    timestamp: DateTime.UtcNow,
    allEntities: someEntityCollection,
    teleportDistanceMeters: 1000,
    entityRadiusMeters: 50000, out string debugSubTitle);
```

`Argus.Graphics.Tests.RequiredCallSiteTests` contains that snippet character-for-character.
Treat it as a contract test: if it stops compiling, the change is wrong.

Consequences that constrain the signature:

- The last argument is **positional**, so `out string debugSubTitle` must remain
  parameter nine. `maxSpeedMetersPerSecond` is optional and therefore comes *after* it.
- `someEntityCollection` is an `IEnumerable<T>` of an application type Argus cannot
  reference, so the method is generic in both the id and the entity: type inference
  keeps the call site character-identical.
- Latitude/longitude/altitude are resolved from `TEntity` in this precedence order:
  1. `TEntity : IArgusEntity` — direct property access.
  2. An accessor delegate registered on `MonitorOptions.Accessors` (supplied to the
     monitor's constructor).
  3. Convention — a compiled expression tree over configurable property-name
     candidates (`Latitude`, `LatitudeWgs84`, `Lat`, …), cached per type in a
     `ConcurrentDictionary`.
  Resolution failure throws `EntityAccessorException` at first use, listing the names
  tried and the three ways to fix it. It **never** silently returns `0`.
- `maxSpeedMetersPerSecond` stays **alongside** `teleportDistanceMeters`. They catch
  different faults: an absolute distance gate flags a slow entity crossing a tick
  boundary and misses a fast entity drifting steadily; a rate gate does the inverse.
  Both, always.

The facade is a compatibility shim over a better interface. It is documented as such,
and `IEntityStreamMonitor` remains the primary API. New code calls
`IEntityStreamMonitor` directly and builds its `GroupTickContext` once per tick.

## 4. Prototype defects that are now acceptance criteria

Each has a named regression test. Do not regress them.

| # | Defect | Where it is fixed |
|---|--------|-------------------|
| 1 | `dt <= 0` early-returned *after* incrementing the sample counter, so stale and duplicate samples deflated the health percentage while raising no flag | `NonPositiveDeltaTimeDetector`, `DuplicateSampleDetector`, `OutOfOrderSequenceDetector`; counters split into `SamplesObserved`/`SamplesEvaluated`/`SamplesFlagged` |
| 2 | State was updated from invalid samples, so the tick after a `(0,0)` fabricated a jump | `EntityTrack.LastSeenSample` vs `EntityTrack.LastValidSample` |
| 3 | `if/else if` made results mutually exclusive; a jump masked an outlier | every detector runs, always (rule 4) |
| 4 | The group centroid included the entity under test and invalid entities | `GroupTickContext` — self-excluded, invalid-excluded, vector mean, `MinimumGroupContributors` |
| 5 | `.Count()`/`.Average()` re-enumerated a lazy sequence per entity per tick — O(n²) | `GroupTickContext` built once per tick; self-exclusion is an O(1) vector subtraction |
| 6 | The debug subtitle was assigned twice; the first assignment was dead | `SubtitleFormatter.Format` is the single source; the facade assigns once |
| 7 | The state dictionary never evicted and was not thread-safe | `TrackStore` — bounded, idle-evicting, `ConcurrentDictionary`-backed; contract in `docs/threading.md` |
| 8 | Detector comment numbering ran 1 → 3 → 4 | `docs/detector-catalogue.md` is the source of truth; detectors are identified by flag, never by number |
| 9 | A render-count colour flash cadence lived inside the detector | `Argus.Graphics.FlashCadence` — presentation state, presentation assembly |

## 5. Layout

```
src/Argus.Core         packed, zero dependencies, netstandard2.0;net8.0
src/Argus.Graphics     packed, Core + Microsoft.Maui.Graphics, netstandard2.0
src/Argus.Controls     packed, Core + Graphics, netstandard2.0, no Microsoft.Maui.Controls
src/Argus.Controls.Maui packed, Controls + Microsoft.Maui.Controls, net8.0-android only (see backlog)
src/Argus.Testing      packed, corruption-injection harness, netstandard2.0;net8.0
src/Argus.Cli          not packed, replays a capture to JSONL/CSV findings
samples/               PackageReference, never ProjectReference
tests/Argus.Package.Tests  PACKAGE reference from ./artifacts/local-feed
```

`samples/` and `tests/Argus.Package.Tests` consume the built `.nupkg` from
`./artifacts/local-feed`, not the projects — so a leaked type breaks the build here
rather than a consumer three months out. `scripts/pack-local` fills that feed.

Build order on a clean clone is therefore: `scripts/pack-local` first, then
`dotnet test Argus.sln`.

## 6. Conventions

- Detectors are stateless. All per-entity state lives on `EntityTrack`.
- A detector returns exactly one `DetectorResult`. `NotEvaluable` always carries a
  reason string naming the missing field.
- Stubs derive from `NotImplementedDetector`: `Evaluate` throws, `Status` is
  `DetectorStatus.NotImplemented`, and the registry skips them unless
  `MonitorOptions.IncludeUnimplementedDetectors` is set (which surfaces them as
  `NotEvaluable` findings so the gap is visible rather than silent).
- British spelling in prose and identifiers where the two differ (`normalised`,
  `quantisation`), except where a BCL name forces otherwise. `Color` keeps its
  American spelling because `Microsoft.Maui.Graphics.Color` does.
- Commit messages describe the diagnostic behaviour, never the domain.
