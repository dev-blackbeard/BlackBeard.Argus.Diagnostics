# Detector catalogue

The specification. Every condition Argus can report, what it means, how it is decided, and what
makes it fire when it should not.

This file is the source of truth for the catalogue — not a comment block, not a numbered list in
a method. The prototype's detector comments ran 1, 3, 4, and nobody could tell whether detector
2 had been removed deliberately, folded into another, or lost. Detectors here are identified by
their flag, never by a number, and every entry is either implemented or explicitly marked as a
stub.

**Status.** `implemented` runs. `stub` is declared in `DetectorCatalogue`, throws if called
directly, and is skipped by the registry. Set `MonitorOptions.IncludeUnimplementedDetectors` to
have stubs appear in reports as `NotEvaluable` findings, so the gap is visible rather than
silent.

**Three outcomes, not two.** Every detector returns `Flagged`, `Healthy`, or `NotEvaluable`.
The third is not a failure mode; it is the honest answer when an input is missing, and it is
never reported as healthy.

---

## Temporal

Faults of ordering, repetition and cadence. These matter first because they corrupt every
quantity derived from the stream: a consumer computing velocity from consecutive positions is
wrong in a way that has nothing to do with the positions.

### `NonPositiveDeltaTime` — implemented
*The interval between this sample and the previous sample for the same entity was zero or
negative, so no rate can be derived from it.*

**Method.** Compare arrival times with the previous arrival, valid or not.
**Evaluable when.** At least one earlier sample exists for the entity.
**False positives.** A host clock stepping backwards — an NTP correction, a VM resuming from a
snapshot. Real, and worth knowing about.
**Notes.** Frequently accompanies `OutOfOrderSequence`; see that entry for why the pair is more
informative than either alone.

### `DuplicateSample` — implemented
*This sample repeats the previous sample's payload exactly, so it carries no new information.*

**Method.** Compare every measurement field against the previous arrival within
`DuplicatePayloadEpsilon`. Identity, arrival time and sequence number are excluded: a producer
that retransmits with a fresh sequence number is still saying nothing new.
**Evaluable when.** At least one earlier sample exists.
**False positives.** A genuinely stationary entity whose fields are all constant. Distinguish by
whether the entity reports a non-zero velocity — that is `FrozenEntity`'s job, and the two
firing together is a stronger diagnosis than either.

### `OutOfOrderSequence` — implemented
*This sample's sequence number is lower than one already observed.*

**Method.** Compare against the highest sequence number seen. A repeated number is reported here
too — not strictly "out of order", but the same class of fault, never correct, and not worth a
catalogue entry of its own.
**Evaluable when.** The protocol supplies a sequence number and one has been seen before.
**False positives.** Sequence-number wrap-around, if the producer's counter is narrower than the
field. The `SequenceGap` entry says more about this.

### `SequenceGap` — **stub**
*One or more sequence numbers between the previous sample and this one never arrived.*

**Method.** Difference against the highest observed, allowing `SequenceGapTolerance`.
**Evaluable when.** The protocol supplies sequence numbers.
**Care required.** Wrap-around. A counter that is a fixed-width integer rolls over, and treating
that as a gap of four billion is worse than not checking at all. The counter width has to be an
input, not an assumption.

### `FrozenEntity` — **stub**
*Position has not changed across several consecutive samples while reported velocity claims
motion.*

**Method.** Count consecutive samples moving less than `StaticPositionEpsilonMeters`
(`EntityTrack.StaticSampleRun` already maintains this) and flag at `FrozenSampleRun` while the
reported ground speed exceeds `FrozenReportedSpeedMetersPerSecond`.
**Evaluable when.** Reported velocity is supplied. Without it, `NotEvaluable` — a stationary
position on its own is not a fault.
**Notes.** The contradiction between two claims the sample makes about itself is the entire
finding. This is what a stuck source looks like from the consumer's side.

### `UpdateRateDrift` — **stub**
*The observed interval has drifted away from the interval the stream is expected to hold.*

**Method.** Compare `EntityTrack.MeanUpdateIntervalSeconds` — an exponentially weighted mean,
so it costs O(1) per sample — against `ExpectedUpdateIntervalSeconds`, allowing
`UpdateRateDriftTolerance`.
**Evaluable when.** `ExpectedUpdateIntervalSeconds` is configured.
**False positives.** Bursty transports. Use the moving mean, never the instantaneous interval.

### `ClockSkew` — **stub**
*The source timestamp and the arrival timestamp disagree by more than the permitted skew.*

**Method.** Difference the two, compare with `MaxClockSkewSeconds`.
**Evaluable when.** The protocol carries a source timestamp and `MaxClockSkewSeconds` is set.
**Notes.** Report the *trend*, not just the magnitude. A constant offset is transport latency; a
growing one is a clock that is not disciplined. Different problems, different owners.

---

## Encoding and framing

**Highest priority in the catalogue.** The stream originates from a serial protocol marshalled
into structs, so the faults that dominate are faults of representation — and those are the ones
that render as entirely plausible values. See `corruption-taxonomy.md` for the mechanisms.

### `NonFiniteValue` — implemented
*A numeric field carried NaN, an infinity, or a subnormal value.*

**Method.** Inspect every supplied field, and `RawFields` when present. Subnormals count when
`TreatSubnormalAsNonFinite` is set.
**Evaluable when.** The sample supplies at least one numeric field.
**False positives.** Essentially none. Nothing physical is NaN, and nothing measured is
subnormal.
**Notes.** The cheapest and least ambiguous check there is, which is why it leads the category.
A NaN in a position is proof the bytes were never a position.

### `ByteOrderSwap` — **stub**
*Implausible as read, plausible with the bytes reversed.*

**Method.** Reinterpret the field's eight bytes in the opposite order and test plausibility.
**Evaluable when.** The field is present.
**Care required.** The inference runs one way only. Implausible-as-read becoming
plausible-when-swapped is evidence; plausible-as-read becoming implausible-when-swapped is not,
because many plausible values survive a swap.

### `FixedPointScaleError` — **stub**
*The magnitude matches a fixed-point representation of the value rather than the value.*

**Method.** Test against `FixedPointScaleFactor` in both directions, within
`EncodingMatchRelativeTolerance`.
**Evaluable when.** The field is present.
**Notes.** Nearly unambiguous — the candidate interpretations differ by seven orders of
magnitude. The finding must say *which* direction it saw; the two have opposite fixes.

### `RadiansAsDegrees` — **stub**
*Angular values confined to a range consistent with radians, in a field specified in degrees.*

**Method.** Count consecutive samples with every angular field inside
`RadiansSuspicionBound`; flag at `RadiansSuspicionMinimumSamples`.
**Evaluable when.** Angular fields are present and enough consecutive samples have been seen.
**False positives.** An entity genuinely near the origin and pointing near north. Indistinguishable
on any single sample, which is exactly why the evidence has to be a run. Note that Argus's own
synthetic harness generates around the origin, so this check needs care in tests.

### `AxisSwap` — **stub**
*Latitude and longitude transposed.*

**Method.** Test whether the transposed pair is markedly more plausible. The strongest evidence
is the group: the swapped pair landing near the group centroid while the pair as read does not.
**Evaluable when.** Both values are present; the group route additionally needs a tick context.
**False positives.** Two small values are plausible in either order. A latitude beyond ninety is
an easy case and a rare one.

### `FieldShift` — **stub · highest value in the catalogue**
*Every field's magnitude matches the field a fixed number of bytes away.*

**Method.** For each offset in `FieldShiftByteOffsets`, reinterpret `RawFields` as though every
field had been read that far from its true position, and test whether all of them — a proportion
of `FieldShiftAgreementFraction`, which defaults to all — become simultaneously plausible.
**Evaluable when.** `RawFields` is supplied with offsets. Without the frame layout this cannot
be reasoned about at all, which is a good argument for populating `RawFields` at the decode
boundary.
**Why it matters.** A frame read one field along yields values that are individually plausible
and collectively wrong: latitude holds a longitude, altitude holds a velocity, and the resulting
entity is somewhere real, at a believable height, moving continuously. No per-field check sees
it. The inference works because a single cause explains every field at once, and independent
faults do not do that.
**Read first.** `corruption-taxonomy.md`, the field-shift section. It is the longest section in
that document for a reason, and it explains why altitude is usually the first field to give the
fault away.

### `QuantisationCollapse` — **stub**
*Positional resolution coarsened abruptly.*

**Method.** Track the smallest non-zero step between consecutive positions; flag when it grows
by more than `QuantisationCoarseningFactor`.
**Evaluable when.** Enough consecutive valid positions exist to establish a prior step.
**Notes.** A property of a sequence, not of a sample. The *transition* is the finding —
resolution that was always coarse is a design decision, not a fault.

### `SentinelValue` — **stub**
*A value characteristic of uninitialised or filler memory.*

**Method.** Test against exact zero, minus one, all-bits-set, and the extremes of the type.
`(0, 0)` is governed by `TreatZeroIslandAsSentinel`.
**Evaluable when.** The field is present.
**False positives.** An entity genuinely at the origin, or at exactly zero altitude. See
`corruption-taxonomy.md` for why rejection is nonetheless the conservative side of the trade.

---

## Kinematic

Motion plausibility derived from consecutive positions. Every comparison is against the last
**valid** sample, never the last one seen.

### `Teleport` — implemented
*Moved further between samples than the absolute distance gate permits, regardless of elapsed
time.*

**Method.** Great-circle distance from the previous valid position against
`MaxTeleportDistanceMeters`.
**Evaluable when.** The gate is configured, this position is usable, and an earlier valid
position exists.
**Notes.** Half of a pair. A distance gate catches a slow entity jumping across a tick boundary
— which a rate gate forgives whenever the interval is long enough to make the implied speed
acceptable — and misses a fast entity drifting steadily. `ImplausibleSpeed` is the inverse.
**Run both. Neither subsumes the other.**

### `ImplausibleSpeed` — implemented
*The speed implied by consecutive positions exceeds the rate gate.*

**Method.** Distance from the previous valid position divided by the interval since that
sample — not since the last arrival — against `MaxSpeedMetersPerSecond`.
**Evaluable when.** The gate is configured and the interval since the previous valid sample is
positive.
**Notes.** Dividing by the wrong interval is how a diagnostic tool manufactures its own
findings: if three of the last four samples were unusable, the displacement happened over four
intervals.

### `ImplausibleAcceleration` — **stub**
*The change in derived speed exceeds the acceleration gate.*

**Method.** Difference against `EntityTrack.LastDerivedSpeedMetersPerSecond` over the interval,
against `MaxAccelerationMetersPerSecondSquared`.
**Evaluable when.** Two prior valid samples exist and the gate is configured.

### `ImplausibleAltitudeRate` — **stub**
*The rate of altitude change exceeds the vertical gate.*

**Method.** Difference altitude over the interval, against `MaxAltitudeRateMetersPerSecond`.
**Evaluable when.** Altitude is supplied on both samples and the gate is configured.
**Notes.** Worth separating from horizontal speed: the vertical channel is often encoded
differently — different width, different scale, sometimes a different datum — so it fails
independently.

### `Jitter` — **stub**
*Oscillating about a mean rather than describing a path.*

**Method.** Over `JitterWindowSamples` recent points, compare the root-mean-square deviation
from their mean against `MaxJitterMeters`. Separate dither from motion by testing whether
successive displacements correlate — a moving entity's steps point the same way, a dithering
one's cancel.
**Evaluable when.** The window is full and `MaxJitterMeters` is configured.

### `VelocityMismatch` — **stub**
*Reported velocity and position-derived velocity disagree.*

**Method.** Compare reported ground speed with derived speed against
`MaxVelocityMismatchMetersPerSecond`.
**Evaluable when.** Reported velocity is supplied, a derived speed exists, and the gate is set.
**Notes.** One of the few detectors that cross-checks two independently encoded fields against
each other, which makes it valuable out of proportion to its simplicity: it catches a fault in
either channel without needing to know which is right.

---

## Attitude

### `AttitudeOutOfRange` — **stub**
*Roll, pitch or yaw outside its defined range.*

**Method.** Compare against `MaxRollDegrees`, `MaxPitchDegrees`, `MaxYawDegrees`.
**Evaluable when.** The angle is supplied.
**Notes.** Report which angle and which bound. A pitch beyond a quarter turn and a yaw beyond a
full turn are different faults with different likely causes.

### `AttitudeWrapDiscontinuity` — **stub**
*An angle jumped across a wrap boundary by more than a continuous rotation could account for.*

**Method.** `Geo.AngularDifferenceDegrees` so a rotation through north reads as a small change,
then compare against `MaxAttitudeStepDegrees`.
**Evaluable when.** The angle is supplied on both samples.
**Notes.** Catches producer and consumer disagreeing about a convention — zero to three-sixty
against plus or minus a half turn — which shows up only at the boundary and therefore only
intermittently, which is what makes it maddening to reproduce.

### `NonNormalisedQuaternion` — implemented
*The quaternion's magnitude differs from one by more than the permitted tolerance.*

**Method.** Root-sum-square of the four components against one, within
`QuaternionNormTolerance`.
**Evaluable when.** All four components are supplied and finite.
**False positives.** Essentially none. Unit magnitude is a definition, not a convention, and the
tolerance exists for accumulated floating-point error rather than for a range of acceptable
magnitudes.
**Notes.** An excellent encoding canary. A quaternion whose components have been shifted,
rescaled or byte-swapped almost never still has unit magnitude, so this fires alongside an
encoding flag and corroborates it — which is precisely why detection must not suppress.

### `HeadingCourseMismatch` — **stub**
*Reported heading disagrees with the course derived from consecutive positions.*

**Method.** `Geo.BearingDegrees` over the last valid pair against reported heading, within
`MaxHeadingCourseDifferenceDegrees`, and only above
`HeadingCourseMinimumSpeedMetersPerSecond`.
**Evaluable when.** Heading is supplied, a valid pair exists, and the entity is moving fast
enough for the derived course to mean anything.
**False positives.** A slow or stationary entity, where the derived course is positional noise.
Hence the minimum speed.

---

## Group

Relationships between an entity and the other entities in its tick. All of these consume a
`GroupTickContext`, which excludes invalid entities, excludes the entity under test, uses a
vector mean that is antimeridian- and pole-safe, and requires `MinimumGroupContributors`.

### `CohesionBreak` — **stub**
*The group's spread has grown beyond its cohesion radius.*

**Method.** `GroupTickContext.SpreadMeters` against `GroupCohesionRadiusMeters`.
**Evaluable when.** Enough contributors and the radius is configured.
**Notes.** A property of the tick rather than of the entity, so every entity in the group
receives the same finding. That is correct and deliberate: the condition is about the group.

### `GroupOutlier` — implemented
*This entity lies further from the group centroid than the radius permits.*

**Method.** Great-circle distance from the centroid of the *other* valid contributors — an O(1)
vector subtraction from the tick's accumulated sum — against `GroupOutlierRadiusMeters`.
**Evaluable when.** A tick context was supplied, identities resolved, this position is usable,
the radius is configured, and at least `MinimumGroupContributors` remain after self-exclusion.
**False positives.** A genuinely dispersed group. This is a threshold about the deployment, not
about the world, which is why it has no default.
**Notes.** Three separate defects in the prototype's version of this check are documented on the
implementation. The short version: including the entity under test understates its own distance
by a factor of one over *n*, and including invalid entities turns one broken entity into *n*
findings, none of which are about the entity that is broken.

### `FormationCollapse` — **stub**
*The group's geometric arrangement has degenerated.*

**Method.** Flag when `GroupTickContext.SpreadMeters` falls below `GroupCollapseSpreadMeters` —
every contributor reporting nearly the same position, which is what a stuck or duplicated source
looks like from the group's point of view — and when the arrangement loses structure entirely.
**Evaluable when.** Enough contributors and the threshold is configured.

---

## Adding a detector

1. Add the flag to `HealthFlags` with the next free bit. **Never renumber**: a persisted finding
   or a golden test locks the values.
2. Add its one-line definition to `HealthFlagInfo.Definitions` and its category to
   `HealthFlagInfo.Categories`. `Defect8_EveryCatalogueFlagHasADefinition` fails without both.
3. Add any threshold it needs to `DetectorThresholds`, named, XML-documented, with the reasoning
   for the default — or `null` if the number is deployment-specific, in which case the detector
   reports `NotEvaluable` until configured.
4. Implement `IDetector`, or derive from `NotImplementedDetector` with a
   `// TODO(argus): <FLAG>` marker if you are only declaring it.
5. Register it in `DetectorCatalogue.CreateAll`.
6. Add an entry here.
7. Add a golden case in `Argus.Golden.Tests`, or add the flag to `GoldenCases.Pending`. That
   list is asserted to match the set of unimplemented detectors exactly, so this step is not
   optional — the test fails until you do it.
