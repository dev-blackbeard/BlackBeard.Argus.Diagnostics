# Threading

Argus sits between a network thread and a UI thread. That boundary is where the prototype's
state handling broke, so the contract is stated explicitly rather than left to be inferred.

---

## The contract, in short

1. **`TrackStore` is safe for concurrent structural access.** Adding, removing, looking up and
   evicting entities can happen from any thread.
2. **One entity must not be observed from two threads at once.** `Observe` for a given entity id
   is single-writer.
3. **Different entities may be observed concurrently.**
4. **`EntitySample` is owned by whoever created it, until it is passed to `Observe`.** After
   that, do not mutate it: the monitor retains a reference as the entity's last-seen and
   possibly last-valid sample.
5. **`EntityHealthReport` and `HealthFinding` are immutable** and may be handed to any thread.
6. **`DetectorThresholds` and `MonitorOptions` must not be mutated while observations are in
   flight.** Configure them at composition. To vary thresholds per call, pass them to `Observe`
   — the facade does exactly this, cloning rather than mutating.

---

## Why the prototype's version was a defect

The state dictionary was a plain `Dictionary<string, TState>`, written from the network thread
and read from the UI thread with no synchronisation. Two failures follow, and the second is the
one that matters.

The first is ordinary: `Dictionary` is not safe for concurrent mutation. A resize racing with a
read can return the wrong entry, miss an entry that is present, or spin. Rare, load-dependent,
unreproducible.

The second is what makes it serious *here*. A missing or wrong entry does not crash — it looks
like a stream fault. An entity whose state vanished is treated as newly seen and produces no
comparison. An entity that got another's state produces a fabricated jump. **The diagnostic tool
becomes a source of false findings, in exactly the format it uses for real ones**, and the team
producing the stream is asked to explain a fault that never happened. There is no worse failure
mode for a tool whose entire purpose is to settle arguments with evidence.

`ConcurrentDictionary` fixes the structural half. The single-writer-per-entity rule fixes the
rest, and it is a rule rather than a lock because a lock per entity per sample would cost more
than it buys for a stream that is naturally partitioned by entity anyway.

---

## What the store guarantees

`TrackStore` uses a `ConcurrentDictionary` for structural safety and a lock for eviction sweeps.
Counters use interlocked operations.

**Eviction** is amortised: it runs every `TrackStore.EvictionInterval` touches rather than on
every one, so the common path stays a single dictionary lookup. Two rules apply, in order:

1. **Idle timeout** — `MonitorOptions.TrackIdleTimeout`, five minutes by default. Long enough
   that an entity dropping out and returning keeps its history.
2. **Hard capacity** — `MonitorOptions.MaxTrackedEntities`, ten thousand by default,
   least-recently-touched first.

The unbounded version of this was the prototype's other defect: nothing was ever removed, so
memory grew with the number of distinct identifiers the stream had *ever* mentioned, which for a
churning population has no bound.

`Evict` is public so a host that observes bursts separated by long silences can sweep on its own
schedule rather than making the next burst pay for the previous one.

---

## What the store does not guarantee

It does not make a single `EntityTrack` safe to mutate from two threads. `EntityTrack` is a
plain object with mutable fields, deliberately — it is written once per sample on the observing
thread, and making it thread-safe would cost an interlocked operation per field per sample to
protect against something the single-writer rule already prevents.

If your architecture genuinely observes one entity from two threads, use two monitors and merge
the reports. That is cheaper and clearer than making the state safe for a case that should not
arise.

---

## Reading state from another thread

`TryGetTrack` is safe to call from any thread. What you get back is a live object whose fields
may be updated while you read them, so:

- Reading a single field is safe — no field is wider than a reference or a `long`, and
  `double`/`long` fields are read atomically on the platforms Argus targets.
- Reading *several* fields is not consistent: you may see counters from one sample and a
  position from the next. For a debug overlay this is fine. For anything computing a derived
  quantity, use the `EntityHealthReport` from `Observe`, which is an immutable snapshot.

---

## The facade

`Argus.Graphics.EntityHealthMonitor` adds two pieces of shared state, both guarded by their own
lock:

- **The group tick context cache**, keyed by the reference identity of the collection passed in.
  Its failure mode is a miss, never a stale answer: a caller passing a fresh sequence per entity
  rebuilds every time and gets correct results at the original cost.
- **The per-call thresholds memo**, so the three gate values arriving on every call do not
  allocate a `DetectorThresholds` clone per entity.

`LastReport` is per-instance rather than per-entity and is a convenience for single-threaded
callers. Do not read it from a second thread.

---

## Recommended shape

```csharp
// Network thread, per tick:
GroupTickContext tick = monitor.CreateTickContext(samples, tickTimeUtc);
foreach (EntitySample sample in samples)
{
    EntityHealthReport report = monitor.Observe(sample, tick);
    reportSink.Publish(report);   // immutable; safe to hand anywhere
}

// UI thread:
Color color = colorPolicy.Resolve(report, renderCount);
```

One context per tick, one thread per entity, immutable reports across the boundary.
