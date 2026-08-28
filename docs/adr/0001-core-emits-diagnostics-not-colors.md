# ADR 0001 — Argus.Core emits diagnostics, not colours

- **Status:** accepted
- **Date:** 2026-08-26

## Context

Argus grew out of a prototype whose single public method took an entity's position and returned
a colour, with a debug subtitle as an `out` parameter. That signature was the whole interface:
the caller drew the entity in whatever colour came back.

It worked, in the sense that the screen showed something. It also made several things
impossible, and the impossibilities all had the same root.

**The reduction happened too early.** By the time the method returned, a set of conditions had
been collapsed to one colour. Everything the detector had worked out — which checks ran, which
were skipped for want of an input, what was measured, what was expected — was gone. The caller
could not log it, aggregate it, export it, or put it in a message to the team producing the
stream, because it no longer existed.

**Only one presentation was possible.** A colour is a rendering. Wanting the same diagnostics as
a CSV, a log line, a replay report or a test assertion meant either duplicating the detection
logic or making the CSV writer parse colours back into conditions.

**Detection acquired presentation state.** The prototype's colour flashed, on a cadence counted
in renders — and the render counter lived inside the detector. So a slower machine redrew less
often and produced different diagnostics from the same stream, and a headless replay, which
redraws never, produced results that could not be compared with a live run at all.

**Conditions became mutually exclusive.** One colour means one answer, so the checks were
chained with `else if`. An entity that had jumped could not also be reported as a group outlier
— and those two together are a far more specific diagnosis than either alone.

**It could not be published.** A library that returns a UI framework's type has that framework
as a dependency, and a diagnostic core that drags in an application model cannot be a small,
auditable, dependency-free package that the team producing the stream can read.

## Decision

`Argus.Core` emits diagnostics and knows nothing about presentation.

- Detection produces `HealthFinding` objects: a flag, its one-line definition, the measured
  value, the expected value, and an outcome of flagged, healthy or not-evaluable.
- `HealthFlags` is a `[Flags]` enum. Conditions accumulate; nothing suppresses anything.
- No UI type appears in Core's public API, and `Argus.Core` has zero dependency package
  references. Both are enforced by tests and an MSBuild guard rather than by convention.
- Colour, label and cadence live in `Argus.Graphics`, whose only external dependency is
  `Microsoft.Maui.Graphics` — the standalone netstandard2.0 package, not the application model.
- The reduction from many conditions to one colour still happens. It happens *last*, in
  `ColorPolicy`, by an explicit and configurable severity precedence.

## Consequences

**Good.**

- The same findings drive a map, a log, a CSV, a replay and a test, with no duplicated logic.
- Detection is deterministic and headless. A capture replayed offline produces exactly what the
  live run produced.
- Conditions co-occur, which is where the diagnostic value is: a jump *and* an outlier *and* a
  non-normalised quaternion is a specific story about a specific fault.
- `Argus.Core` is publishable, small and auditable. The team producing the stream can read the
  detector definitions that generated a finding without repository access, which is the point of
  the whole exercise.
- The severity precedence is written down, in one place, and can be changed without touching
  detection.

**Costs, accepted.**

- Existing callers wanted a colour. Hence the compatibility facade in `Argus.Graphics`, which is
  documented as a shim and kept compiling by a verbatim call-site test.
- Two types are called `EntityHealthMonitor`, in two namespaces. That is a deliberate migration
  aid — an application swaps a `using`, not every call site — and the ambiguity disappears with
  the last call site that needs it.
- A caller who wants only a colour now goes through one more layer.

## Alternatives considered

**Return both a colour and the findings.** Rejected: Core would still reference a UI type, so
the dependency and the publishability problems remain untouched.

**Make the colour type generic.** Rejected: it spreads a presentation concept through every
signature in Core to avoid naming it once in Graphics.

**Keep detection as-is and add a separate diagnostic path.** Rejected: two implementations of
the same checks drift, and the one people look at is the one that is wrong.
