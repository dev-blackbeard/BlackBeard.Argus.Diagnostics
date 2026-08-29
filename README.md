# Argus

**Stream diagnostics for geospatial entity feeds.**

Argus taps a stream of 6DOF entity samples — position, attitude, linear and angular velocity —
and emits structured, self-describing findings about what is wrong with it.

It has no idea what the entities are. That is deliberate: every judgement it makes is against a
configured threshold or a mathematical invariant, never against domain knowledge, which is what
makes it reusable across whatever is producing the stream.

```
Argus.Core      zero dependencies · netstandard2.0 · net8.0
Argus.Graphics  Core + Microsoft.Maui.Graphics · netstandard2.0
Argus.Testing   corruption-injection harness · netstandard2.0 · net8.0
```

---

## Why looking at the map is not enough

The usual way to check a stream is to render it and look. That catches the faults that are easy
to catch and misses the ones that matter, for four reasons worth stating precisely:

1. **The viewport shows part of the world.** An entity that has moved outside it is not observed
   to be wrong; it is not observed at all.
2. **A one-tick anomaly does not survive to the next frame.** At a typical update rate a
   corrupted sample is on screen for a few tens of milliseconds. Nobody sees it — and it still
   propagates into everything derived from consecutive positions.
3. **The eye checks continuity, not correctness.** A smoothly moving entity looks right whether
   or not it is where the producer said it was.
4. **A map has no memory and no arithmetic.** It cannot tell you that the smallest positional
   step in this stream became sixteen times coarser at 14:02.

And the deeper problem: when a stream originates from a serial protocol marshalled into structs
and piped over a socket, **the faults that dominate are faults of encoding and framing, not of
physics** — and those produce values that are *entirely plausible*. A frame read eight bytes off
puts a longitude in the latitude field and a velocity in the altitude field. Every value is in
range. The map draws it without complaint. The entity is simply not where it should be.

Argus exists so that two teams either side of a stream can argue from evidence rather than
impressions — which is also why the core and its documentation are public. The team producing
the stream can read the detector definition that generated a finding without needing access to
anything.

See [`docs/corruption-taxonomy.md`](docs/corruption-taxonomy.md) for what each fault looks like
on the wire.

---

## Quickstart

```csharp
using Argus.Configuration;
using Argus.Contracts;
using Argus.Pipeline;
using Argus.State;

var options = new MonitorOptions();

// Deployment gates have no defaults. A detector without its gate reports NotEvaluable rather
// than inventing a number, so these are yours to set.
options.Thresholds.MaxTeleportDistanceMeters = 1000.0;   // absolute distance gate
options.Thresholds.MaxSpeedMetersPerSecond   = 300.0;    // rate gate — set both, see below
options.Thresholds.GroupOutlierRadiusMeters  = 50000.0;

var monitor = new EntityHealthMonitor(options);

// Once per tick, not once per entity.
GroupTickContext tick = monitor.CreateTickContext(samples, tickTimeUtc);

foreach (EntitySample sample in samples)
{
    EntityHealthReport report = monitor.Observe(sample, tick);

    foreach (HealthFinding finding in report.FlaggedFindings())
    {
        Console.WriteLine(finding);
        // Teleport: measured 12043.2 m, expected at most 1000 m - The entity moved further
        // between consecutive samples than the absolute distance gate permits, regardless of
        // how much time elapsed.
    }
}
```

Every finding carries the flag name, a one-line definition, the measured value and the expected
value or range. That redundancy is the point: a finding has to be readable by somebody who
cannot look up what the flag means.

### Set both gates

`MaxTeleportDistanceMeters` and `MaxSpeedMetersPerSecond` are not alternatives.

- An **absolute distance gate** catches a slow entity that jumps across a tick boundary, and
  misses a fast entity drifting steadily.
- A **rate gate** does exactly the inverse.

Neither subsumes the other. Configure both.

### Three outcomes, not two

A detector returns `Flagged`, `Healthy`, or `NotEvaluable` — the last when a field it needs was
not supplied or a threshold it needs is not configured. It is never reported as healthy.
"We did not check" and "we checked and it was fine" are different claims, and a report that
conflates them invites the reader to conclude something was verified when it was not.

```csharp
if (report.IsHealthy && report.IsFullyEvaluated) { /* actually verified */ }
```

---

## Conditions are not mutually exclusive

`HealthFlags` is a `[Flags]` enum and detection never suppresses. An entity can be a group
outlier *and* have jumped *and* carry a non-normalised quaternion — and the three together are a
far more specific diagnosis than any one of them. Presentation reduces that set to a single
colour, by explicit severity precedence, in `Argus.Graphics` and nowhere else.

---

## Corruption injection

`Argus.Testing` damages a synthetic stream the way a wire actually does — reversing bytes,
shifting field offsets, rescaling fixed-point integers, dropping and reordering frames — so
detectors are tested against faults rather than against assertions about faults.

```csharp
var damaged = new InjectedStreamSource(
    new SyntheticStreamSource(new ScenarioDefinition()),
    seed: 1,
    sampleInjectors: new[] { new ByteShiftInjector(byteShift: 8) });
```

Scenario inputs — origin, spacing, entity count, update rate — are parameters with neutral
defaults. Real values belong to whoever is consuming the library, in their own configuration.

---

## Command line

```
argus replay capture.jsonl --format csv --out findings.csv
```

Replays a JSON Lines capture and writes self-describing findings. Exit code 1 when anything is
flagged, so it drops into a pipeline.

---

## Building

```bash
scripts/pack-local        # fills ./artifacts/local-feed
dotnet test Argus.sln
```

The order matters. `samples/` and `tests/Argus.Package.Tests` consume the built `.nupkg` files
rather than the projects, so a type accidentally left internal or a dependency that leaked into
the nuspec fails here rather than in a consumer's build three months from now. `scripts/build`
does the whole sequence including the hygiene check.

---

## Documentation

| | |
|---|---|
| [`docs/corruption-taxonomy.md`](docs/corruption-taxonomy.md) | What each fault looks like on the wire. Written for the team producing the stream. |
| [`docs/detector-catalogue.md`](docs/detector-catalogue.md) | Flag → meaning → method → false-positive notes. |
| [`docs/interface-contract.md`](docs/interface-contract.md) | What crosses the fence, and what never does. |
| [`docs/threading.md`](docs/threading.md) | The concurrency contract, stated rather than inferred. |
| [`docs/adr/0001-core-emits-diagnostics-not-colors.md`](docs/adr/0001-core-emits-diagnostics-not-colors.md) | Why the core returns findings and not a colour. |

---

## Status

Early. The contracts, the pipeline, the package plumbing and one reference detector per category
are in place; most of the catalogue is declared and not yet implemented. Stubs are visible
rather than absent — set `MonitorOptions.IncludeUnimplementedDetectors` and they appear in every
report as `NotEvaluable`, so it is always possible to see what is *not* being checked.

`docs/detector-catalogue.md` marks each entry `implemented` or `stub`.

## Licence

Apache-2.0. See [LICENSE](LICENSE).
