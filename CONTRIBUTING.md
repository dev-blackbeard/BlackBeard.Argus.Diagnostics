# Contributing

## Before anything else: this repository is public

Argus is a diagnostic library for geospatial entity streams and it has no idea what the entities
are. Keeping it that way is the first constraint on every change.

**Nothing domain-specific enters this repository** — not in code, comments, test fixtures, docs,
sample data, commit messages, branch names or file names.

- Real geographic coordinates of any kind. Samples and tests use the synthetic origin
  `(0.0, 0.0)` and metric offsets from it.
- Force-disposition or defence vocabulary.
- Real geometry, spacing, entity counts or update rates.
- Threshold values tuned to any real deployment.
- Employer names, team names, project or protocol codenames.

Run the checker before you push:

```bash
scripts/check-public-hygiene
```

It is wired into CI and must stay green. If you have the private repository alongside, point it
at that repository's extra denylist too:

```bash
ARGUS_EXTRA_DENYLIST=/path/to/private/config/hygiene-denylist.txt scripts/check-public-hygiene
```

> If you are ever unsure whether something belongs in public, it does not. Moving code outward
> later is trivial; unpublishing is not.

---

## Building

```bash
scripts/build              # hygiene, pack, build, test — the whole sequence
```

or by hand:

```bash
scripts/pack-local         # MUST come first; fills ./artifacts/local-feed
dotnet build Argus.sln
dotnet test Argus.sln
```

`samples/` and `tests/Argus.Package.Tests` reference the packed `.nupkg` files, not the projects.
That is deliberate — it is what catches a type accidentally left internal, a dependency that
leaked into the nuspec, or a target framework that does not resolve — and it means the feed has
to exist before the solution restores.

---

## The rules that are not negotiable

These are architecture, not style. A change that breaks one of them is a change to the
architecture and needs to be argued as such.

1. `Argus.Core` emits diagnostics, never colours or UI strings. No UI type in Core's public API.
2. `Argus.Core` has zero dependency `PackageReference` entries. An MSBuild target fails the
   build if one appears; `Argus.Package.Tests` re-checks the packed artifact.
3. Colour and label presentation lives only in `Argus.Graphics`, whose sole external dependency
   is `Microsoft.Maui.Graphics` — the standalone netstandard2.0 package, not
   `Microsoft.Maui.Controls`.
4. `HealthFlags` is a `[Flags]` enum. Conditions are not mutually exclusive and **detection
   never suppresses**. No `else if` between detectors.
5. Every threshold is a named, XML-documented member of `DetectorThresholds`. No magic numbers
   in detector bodies.
6. A detector lacking its inputs reports `NotEvaluable`, never "healthy". Unsupplied
   `EntitySample` fields are `null`, never `0`.
7. Findings are self-describing: flag name, one-line definition, measured value, expected value
   or range.
8. The public API surface is locked with `PublicApiAnalyzers`.
9. Core and Testing multi-target `netstandard2.0;net8.0`. Graphics targets `netstandard2.0`.
10. Deterministic builds, SourceLink, symbol packages, central package management.

---

## Adding a detector

The full checklist is at the end of [`docs/detector-catalogue.md`](docs/detector-catalogue.md).
The short version:

1. Add the flag to `HealthFlags`, next free bit. **Never renumber.**
2. Add its definition and category to `HealthFlagInfo`.
3. Add any threshold to `DetectorThresholds` — named, documented, with the reasoning for the
   default. If the right value depends on the deployment, default to `null` and report
   `NotEvaluable` until it is configured.
4. Implement `IDetector`, or derive from `NotImplementedDetector` with a `// TODO(argus): <FLAG>`
   marker if you are only declaring it.
5. Register it in `DetectorCatalogue.CreateAll`.
6. Document it in `docs/detector-catalogue.md`.
7. Add a golden case, or add the flag to `GoldenCases.Pending`. The pending list is asserted to
   match the unimplemented set exactly, so the tests will tell you.

### Writing a detector

- **Stateless.** All per-entity state lives on `EntityTrack`, reached through the context.
- **Compare against the last *valid* sample**, not the last one seen. `DetectorContext` gives
  you both, and the distinction is a regression test.
- **Say why it was not evaluable.** Name the missing field or the unconfigured threshold. A
  reason of "could not evaluate" is not a reason.
- **Quote the number you compared against.** `HealthFinding.Quantity`, `Range` and `AtMost`
  format them consistently, in the invariant culture.
- **Write the false-positive note before the code.** If you cannot say what would make it fire
  when it should not, the detector is not ready. That note goes in the catalogue.

---

## Tests

| project | references | what it proves |
|---|---|---|
| `Argus.Core.Tests` | projects | Behaviour, including one regression test per known prototype defect. |
| `Argus.Graphics.Tests` | projects | The facade, and the required call site verbatim. |
| `Argus.Golden.Tests` | projects | Injector → exact flag set, locked. |
| `Argus.Package.Tests` | **packages** | The packed artifacts are usable and dependency-clean. |

`Argus.Graphics.Tests/RequiredCallSiteTests.cs` contains an application call site copied
character for character. **Treat a compile error there as a regression in the facade, not as a
test that needs updating.**

Golden tests assert flag sets *exactly*, not "contains". A detector that fires on the bad input
and on six other things is not working — spurious findings are how a diagnostic tool loses the
argument it exists to win.

---

## Style

- File-scoped namespaces, four-space indent, braces always. `.editorconfig` has the rest.
- British spelling in prose and identifiers where the two differ (`normalised`, `quantisation`),
  except where a BCL name forces otherwise.
- XML documentation on every public member — findings are only self-describing if the contracts
  that carry them say what they mean.
- Comments explain *why*. The code already says what.

## Commits

Describe the diagnostic behaviour, never the domain. `fix: compare against the last valid sample,
not the last seen` is a good message. Anything that would fail `scripts/check-public-hygiene` in
a file would also fail it in a commit message — and commit messages cannot be unpublished either.
