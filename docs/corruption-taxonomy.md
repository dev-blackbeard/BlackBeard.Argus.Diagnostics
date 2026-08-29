# Corruption taxonomy

**Audience: the team producing the stream, and anyone consuming it.**

This document describes what each class of stream fault looks like *on the wire*, why it is
hard to see, and what evidence distinguishes it from the others. It contains no thresholds and
no tuned values — those are deployment-specific and live wherever the deployment lives. It is
about mechanisms.

It exists because the two teams either side of a stream generally cannot see the same thing. The
producing team sees a struct being marshalled and a socket being written. The consuming team
sees entities on a map. Between those two views, the most damaging faults are invisible from
both: they produce values that are *entirely plausible*.

---

## The premise: this is an encoding problem, not a physics problem

The stream originates from a serial protocol, marshalled into structs, piped over TCP or UDP,
and unmarshalled on the far side. Almost every field is a fixed-width binary number at a fixed
offset in a frame.

That shape determines which faults are likely, and it is not the shape people expect. The
intuitive failure modes — an entity moving impossibly fast, a position on the wrong continent —
are the *easy* cases, because they are visible. The characteristic failures of a
marshalled-struct protocol are quieter:

- The bytes are correct and the **order** is wrong.
- The bytes are correct and the **offset** is wrong.
- The bytes are correct and the **scale** is wrong.
- The bytes are correct and the **units** are wrong.
- The bytes were never written at all, and what is read is whatever was in the buffer.

In all five, every individual field passes every individual sanity check. A latitude is between
minus ninety and ninety. An altitude is a positive number of metres. A map draws them without
complaint. **The plausibility of a corrupt value is the problem, not an incidental feature of
it.**

This is also why "just look at the map" does not work as verification, and why that failure is
worth stating precisely rather than as a slogan:

1. **The viewport shows part of the world.** An entity that has moved outside it is not
   observed to be wrong; it is not observed at all.
2. **A one-tick anomaly does not survive to the next frame.** At a typical update rate a
   corrupted sample is on screen for a few tens of milliseconds. Nobody sees it. It nonetheless
   propagates into anything derived from consecutive positions.
3. **The eye checks continuity, not correctness.** A smoothly moving entity looks right whether
   or not it is where the producer said it was.
4. **A map has no memory and no arithmetic.** It cannot tell you that the smallest positional
   step in this stream became sixteen times coarser at 14:02.

Argus taps the stream after the consumer has decoded it and emits structured findings, so both
teams argue from the same evidence.

---

## Field shift — the one-byte-misalignment signature

**This is the most valuable detector in the catalogue and the least obvious. It is worth the
length.**

### The mechanism

A frame is a sequence of fixed-width fields at fixed offsets. Suppose the layout is:

```
offset  0   8       16        24       32
        |   |       |         |        |
        lat lon     alt       vel_n    vel_e      (doubles, 8 bytes each)
```

Now suppose the reader's idea of where the payload starts is wrong by eight bytes — because a
header grew by a field, because a version byte was added and the struct padded, because a
length prefix was consumed twice, because two frames were coalesced in a stream socket and the
reader resynchronised at the wrong place. The reader now decodes:

```
reader's "lat"  <- the bytes that are really lon
reader's "lon"  <- the bytes that are really alt
reader's "alt"  <- the bytes that are really vel_n
```

Every field it produces is a **real number that was correctly encoded**. Nothing is NaN.
Nothing is out of range, necessarily. The reader has simply read the right values into the
wrong names.

### Why this is the worst case

Consider what arrives at the consumer. Latitude now holds a longitude — a value that will be
in range as long as the real longitude was within plus or minus ninety, which covers half the
world. Longitude now holds an altitude — a value of a few hundred or a few thousand, which is
*out* of longitude's range and therefore catchable, or *in* range if the entity is low.
Altitude now holds a velocity, which is a small positive or negative number and completely
plausible as an altitude.

So the fault produces an entity at a position that is somewhere real, at an altitude that is
believable, moving in a way that is continuous from tick to tick — because the underlying
fields are themselves continuous. **It looks like an entity. It is just not the right entity,
in the right place, at the right height.**

And it is *systematic*: every entity in the stream shifts identically, so the group moves
coherently. Group cohesion checks pass. The formation is intact. It is simply in the wrong
place, or the wrong shape, in a way that no single-field check can see.

A shift of four bytes rather than eight is worse still. Now each "double" is assembled from the
low half of one field and the high half of the next. The resulting values are usually wild —
enormous, tiny, or subnormal — because the exponent bits come from one field's mantissa. That
version of the fault is *easier* to catch, and the NaN and subnormal checks usually see it. The
eight-byte version, aligned to the field boundary, is the dangerous one.

### The inference that catches it

The insight is that no single field can settle the question, and **the whole frame can**.

For each candidate shift *k* (the offsets in `DetectorThresholds.FieldShiftByteOffsets`), ask:
if every field had been read *k* bytes away from where it should have been, would *all* of them
simultaneously become more plausible than they are as read?

That question is answerable because the fields have different characteristic magnitudes:

| field | plausible magnitude | implausible as |
|---|---|---|
| latitude | 0 to 90 | an altitude of thousands |
| longitude | 0 to 180 | a velocity near zero for a moving entity |
| altitude | 0 to tens of thousands | a latitude, if it exceeds 90 |
| velocity component | 0 to hundreds | an altitude |

The magnitudes overlap, which is exactly why a per-field check cannot decide. A value of 120
is a fine longitude and an impossible latitude and a plausible altitude and a plausible
velocity. Asked alone, it says nothing.

Asked together, it says a great deal. The strength of the inference is not that any one field
looks wrong; it is that **one single cause explains all of them at once**. Independent faults
do not do that. For a three-field frame, the probability that a random corruption makes every
field simultaneously implausible in its own slot *and* simultaneously plausible in the slot
eight bytes over is negligible. That coincidence is the signature.

Hence `DetectorThresholds.FieldShiftAgreementFraction` defaults to `1.0` — all of them. An
inference that explains most of the fields is a weaker claim than the individual per-field flags
the detector would already be raising, and it would fire on ordinary noise.

### Where it shows first

**Altitude is usually the first field to give it away**, and it is worth knowing why.

Altitude has the narrowest plausible range relative to its neighbours. It is bounded below by
roughly zero and above by a number that is small compared with a scaled longitude. So the
moment a longitude-magnitude value lands in the altitude slot — a value in the hundreds when
altitude is normally in the thousands, or in the tens of thousands when longitude is a degree
value scaled by a fixed-point factor — the discrepancy is visible in a way it never is for
latitude and longitude, which happily impersonate each other.

The practical consequence: **an altitude that has become oddly correlated with longitude is the
canary**. If the altitude channel starts tracking longitude's magnitude, suspect a shift before
suspecting the altitude source.

### What a finding must say

A field-shift finding is asserting something specific and falsifiable, so it must carry:

- the shift that explains the frame, in bytes;
- for each field, the value as read and the value it would have under that shift;
- what makes each of those implausible and plausible respectively.

That is enough for the producing team to check the struct layout on their side against the
frame length, which is usually a five-minute answer.

### What to check on the producing side

- Did a field get added to, or removed from, a header or the struct?
- Did the compiler's padding change — an alignment attribute, a field reordering, a change of
  target architecture?
- Is the reader framing on a length prefix, a delimiter, or a fixed size? A fixed-size reader
  on a stream socket resynchronises at the wrong offset after a single truncated frame and
  never recovers.
- Are two frames ever coalesced by the transport? TCP has no message boundaries. A reader that
  assumes one read equals one frame will eventually be wrong by exactly one frame's worth of
  bytes, which is the same fault at a larger stride.

---

## Byte-order (endianness) mismatch

**Mechanism.** Producer and consumer disagree about the order of the bytes within a field. The
eight bytes of the double are reversed.

**What it looks like.** A reversed double is dominated by what happens to the exponent, which
lives in the high bits and ends up in the low bits. The result is almost always one of: an
enormous number, a number so small it is subnormal, or NaN. Small, "clean" values are the
exception — and they are the dangerous exception, because a byte-reversed value that happens to
be plausible is indistinguishable from a real one.

**The evidence.** Asymmetric, and the asymmetry matters. A value that is *implausible as read*
and *plausible byte-swapped* is evidence of a swap. A value that is plausible as read and
implausible swapped is **not** evidence of anything — many plausible values remain plausible
when swapped, so the test only carries information in one direction.

**Not to be confused with.** Field shift. Both produce implausible values. A byte-order
mismatch affects every field independently and identically; a field shift moves values between
fields while leaving each value itself intact. If the *set* of values in the frame is unchanged
and only their assignment to fields has moved, it is a shift, not a swap.

---

## Fixed-point scale error

**Mechanism.** Angular values are commonly transmitted as integers scaled by a fixed factor —
ten to the seventh is conventional for degrees, giving roughly centimetre resolution. Either
the reader forgets to divide, or it divides something that was already in degrees.

**What it looks like.** Values that are the right shape and the wrong magnitude by exactly the
scale factor. Reading raw gives latitudes in the hundreds of millions. Dividing twice gives
latitudes of a few millionths of a degree — which is a position a few centimetres from the
origin, and therefore looks like an entity sitting at the origin.

**The evidence.** Divide or multiply by the scale factor and check whether the result is
plausible where the original was not. Unlike the byte-order test, this one is nearly
unambiguous, because the two candidate interpretations differ by seven orders of magnitude and
almost nothing is plausible at both.

**The direction matters.** Reading raw and dividing twice are different bugs with opposite
fixes, and a finding that does not say which one it saw is only half a finding.

---

## Radians supplied where degrees are expected

**Mechanism.** A conversion missed, usually at a boundary where one library speaks radians and
another degrees.

**What it looks like.** Everything is in range, everything is finite, everything is continuous.
The entity is simply in the wrong place — about fifty-seven times closer to the origin than it
should be, along both axes. On a map, this is an entity somewhere else, not an entity whose
units are wrong.

**The evidence.** Angular values confined to plus or minus a little over pi, sustained across
many samples. The bound is the giveaway: a degree value that never leaves that range is either
radians or an entity that has genuinely never left a small region near the origin.

**Why a single sample cannot decide.** Those two cases are identical on any one sample. The
detector requires a run (`RadiansSuspicionMinimumSamples`) before it will say anything, because
the evidence *is* the run. A synthetic stream generated around the origin — as Argus's own test
harness generates — is exactly the false positive this guards against, which is a good reason to
be careful and a bad reason to skip the check.

---

## Latitude/longitude axis swap

**Mechanism.** A transposition, at any layer: a struct field order, a constructor's argument
order, a serialiser's property order, a coordinate library that puts longitude first.

**What it looks like.** If either value exceeds ninety, it is trivially detectable — the
latitude is out of range. If both are small, the swapped position is a perfectly ordinary
position and nothing about the sample says which ordering was intended.

**The evidence.** The group. If the *swapped* pair lands near the group centroid and the pair
as read does not, the transposition explains both the entity's position and its membership of
the group, and that is an argument. Nothing about the entity in isolation is.

**Why "longitude first" is a real convention.** Several widely used formats put longitude
before latitude. This is not a mistake anyone is being careless about; it is two defensible
conventions meeting.

---

## Float32 quantisation collapse

**Mechanism.** A stage somewhere in the pipeline stores a position in a 32-bit float — a
graphics buffer, an interop struct, a database column, a serialisation format.

**What it looks like.** Nothing, on any single sample. A float has roughly seven significant
decimal digits; a degree of latitude is roughly a hundred kilometres; so a position stored as a
float is correct to within metres and no longer correct to within centimetres. Every position
is plausible. Every position is close to right.

What changes is the *step*. Positions snap to a lattice, so consecutive samples of a slow entity
report either no movement or a jump of one lattice step. Anything deriving velocity from
consecutive positions produces a staircase: alternating zero and a spike, averaging out to
roughly the right answer while being wrong at every individual moment.

**The evidence.** The smallest non-zero step between consecutive positions, tracked over time.
It is a property of a sequence, not of a sample, which is why nothing that looks at one frame
can see it. The transition is what to look for — resolution coarsening *abruptly* is a pipeline
change; resolution that was always coarse is a design decision.

---

## Sentinel and uninitialised values

**Mechanism.** A field is read that was never written: a struct allocated and not populated, a
frame shorter than expected, an optional field absent, a "no data" convention the reader does
not know about.

**What it looks like.** Exact zero, minus one, all-bits-set patterns, the extremes of the type.

**The special case that matters: `(0, 0)`.** This is simultaneously a legal position and the
commonest filler value in computing, and **nothing about the value itself distinguishes the two**.
Argus treats it as a sentinel by default (`DetectorThresholds.TreatZeroIslandAsSentinel`),
because in a stream originating from a marshalled struct the prior strongly favours "zeroed
buffer". The trade is explicit: rejecting a real entity that is exactly at the origin costs one
`NotEvaluable`; accepting an uninitialised frame costs a fabricated jump on the following tick
and a poisoned group centroid on this one.

**The second-order damage is the real cost.** A single unusable position that is allowed into
state produces a *fabricated* fault on the next tick — the entity appears to jump from the
origin to wherever it actually is, at a speed determined by nothing but the tick rate. This is
why Argus tracks the last *valid* sample separately from the last *seen* sample, and why an
unusable sample is never allowed to become the reference the next one is measured against.

---

## Ordering, loss and cadence

These are transport faults rather than encoding faults, and they are worth separating from each
other because they have different owners.

**Out-of-order arrival.** A datagram transport reorders, or two producers interleave. The
sample's sequence number is lower than one already seen. It usually appears together with a
non-positive interval — and the *pair* is the diagnosis. Out-of-order alone suggests a producer
numbering bug. A non-positive interval alone suggests a clock going backwards. Together they
mean reordering, which belongs to the transport.

**Duplicate.** The same payload twice. The stream carries less information than its rate
suggests. A consumer deriving velocity sees an entity that has stopped, which is plausible, and
a consumer computing an update rate sees a healthy one, which is false.

**Loss.** Sequence numbers skip. Only detectable *because* the producer numbers its frames —
without a sequence number, a missing sample is indistinguishable from an entity that was not
reported this tick, which is why the gap check reports `NotEvaluable` rather than "healthy" when
the field is absent.

**Cadence drift.** The interval between samples moves away from what the stream is specified to
hold. Rarely a fault in itself; usually a symptom of something upstream saturating.

**Clock skew.** The producer's timestamp and the arrival time disagree. The useful signal is the
*drift* of that difference rather than its size: a constant offset is transport latency, and a
growing one is an undisciplined clock. Different problems, different owners.

---

## What Argus deliberately does not claim

- **It does not know what the entities are.** Every plausibility judgement it makes is against
  a configured threshold or a mathematical invariant, never against domain knowledge. This is
  what makes it reusable, and it is also a limit: Argus cannot tell you that an entity is doing
  something it should not be doing, only that the stream is describing it inconsistently.
- **It does not distinguish "not checked" from "checked and fine".** Or rather, it refuses to:
  a detector missing its inputs reports `NotEvaluable`, and that is a different outcome from
  healthy. A report that conflates them invites the reader to conclude something was verified
  when it was not, which is worse than no report.
- **It does not decide who is at fault.** A finding is evidence about the stream. Whether the
  cause is on the producing side, in the transport, or in the consumer's own decoding is
  usually clear from the finding, and Argus does not assert it.
