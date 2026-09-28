using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Argus.Contracts;
using Argus.Graphics;

namespace Argus.Controls;

/// <summary>
/// The sink a host application pushes observed reports into. Backs a bindable, ordered
/// collection of <see cref="EntityHealthItemViewModel"/>, one per distinct <see cref="EntityKey"/>.
/// </summary>
/// <remarks>
/// <para>
/// Two ways to push data in. <see cref="Observe(EntityHealthReport, EntitySample?, string?)"/> updates
/// the row and raises its change notifications immediately: simplest, and right for low rates.
/// For a high-rate stream, call <see cref="Record(EntityHealthReport, EntitySample?, string?)"/> per
/// report and <see cref="Flush()"/> once per UI batch or frame instead. <c>Record</c> keeps every
/// per-flag count exact but raises nothing; <c>Flush</c> publishes each touched row once, however
/// many reports it received, so a row updated ten times between frames costs one binding update,
/// not ten. <see cref="RenderTick"/> flushes too, so a host already driving it from a timer needs no
/// extra call.
/// </para>
/// <para>
/// Call <see cref="Observe(EntityHealthReport, EntitySample?, string?)"/> or <c>Record</c> once per
/// <c>IEntityStreamMonitor.Observe</c> call, from whichever thread produced the report.
/// <see cref="Items"/> is an <see cref="ObservableCollection{T}"/>, so it must only be mutated on
/// the thread a bound UI expects — marshal to that thread before calling
/// <see cref="Observe(EntityHealthReport, EntitySample?, string?)"/> or <see cref="RenderTick"/> if
/// the report was produced elsewhere. This mirrors the recommended shape in
/// <c>docs/threading.md</c>: observe off-thread, hand only the immutable report (and, here, the
/// sample) across, and do the UI-affecting work on the UI thread. <c>Record</c> and <c>Flush</c> are
/// UI-thread-only in exactly the same way.
/// </para>
/// </remarks>
public sealed class EntityHealthCollection
{
    private readonly Dictionary<EntityKey, EntityHealthItemViewModel> _byKey = new Dictionary<EntityKey, EntityHealthItemViewModel>();
    private readonly Queue<EntityHealthItemViewModel> _dirty = new Queue<EntityHealthItemViewModel>();
    private long _renderCount;

    /// <summary>Creates a collection with the default colour policy.</summary>
    public EntityHealthCollection()
        : this(new ColorPolicy())
    {
    }

    /// <summary>Creates a collection with a specific colour policy.</summary>
    /// <param name="colors">How reports become colours.</param>
    /// <exception cref="ArgumentNullException"><paramref name="colors"/> is <c>null</c>.</exception>
    public EntityHealthCollection(ColorPolicy colors)
    {
        if (colors == null)
        {
            throw new ArgumentNullException(nameof(colors));
        }

        Colors = colors;
        Items = new ObservableCollection<EntityHealthItemViewModel>();
    }

    /// <summary>The rows, in first-seen order. Bind a list control's items source to this.</summary>
    public ObservableCollection<EntityHealthItemViewModel> Items { get; }

    /// <summary>How reports become colours. Mutate before observations are in flight, not during.</summary>
    public ColorPolicy Colors { get; }

    /// <summary>
    /// How a row's just-arrived receipt indicator fades after each <see cref="Observe"/> call.
    /// Mutate before observations are in flight, not during.
    /// </summary>
    public ReceiptPulse Receipt { get; set; } = new ReceiptPulse();

    /// <summary>Records one observed report and publishes its row immediately.</summary>
    /// <param name="report">The report from <c>IEntityStreamMonitor.Observe</c>.</param>
    /// <param name="sample">
    /// The sample <paramref name="report"/> was produced from, for 6DOF display. Optional, since
    /// <see cref="EntityHealthReport"/> does not itself carry the sample that produced it — omit
    /// if the caller does not have it to hand.
    /// </param>
    /// <param name="groupTag">
    /// A disambiguator, so <paramref name="report"/>'s entity id does not have to be unique on its
    /// own. Combined with the entity id into the row's <see cref="EntityKey"/>; never inspected
    /// otherwise.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="report"/> is <c>null</c>.</exception>
    /// <remarks>
    /// Equivalent to <see cref="Record(EntityHealthReport, EntitySample?, string?)"/> followed by
    /// <see cref="Flush()"/>, so it also publishes any rows still pending from earlier
    /// <c>Record</c> calls.
    /// </remarks>
    public void Observe(EntityHealthReport report, EntitySample? sample = null, string? groupTag = null)
    {
        Record(report, sample, groupTag);
        Flush();
    }

    /// <summary>
    /// Records one observed report without raising any change notification: the row's per-flag
    /// counts are updated now, its bindable properties on the next <see cref="Flush()"/>.
    /// </summary>
    /// <param name="report">The report from <c>IEntityStreamMonitor.Observe</c>.</param>
    /// <param name="sample">The sample <paramref name="report"/> was produced from, if the caller has it.</param>
    /// <param name="groupTag">A disambiguator combined with the entity id into the row's <see cref="EntityKey"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="report"/> is <c>null</c>.</exception>
    /// <remarks>
    /// Every report must still go through here (or <see cref="Observe"/>), never only the latest
    /// one per entity: <see cref="EntityHealthItemViewModel.FlagCounts"/> is cumulative, and a
    /// report skipped here is a flag occurrence lost for good. What is coalesced is only the
    /// publishing. A row first seen here is added to <see cref="Items"/> when it is first flushed,
    /// in first-recorded order.
    /// </remarks>
    public void Record(EntityHealthReport report, EntitySample? sample = null, string? groupTag = null)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        var key = new EntityKey(report.EntityId, groupTag);

        EntityHealthItemViewModel? item;
        if (!_byKey.TryGetValue(key, out item))
        {
            item = new EntityHealthItemViewModel(key);
            _byKey[key] = item;
        }

        if (item.Accumulate(report, sample))
        {
            _dirty.Enqueue(item);
        }
    }

    /// <summary>How many rows have recorded reports that have not yet been flushed.</summary>
    public int PendingRowCount
    {
        get { return _dirty.Count; }
    }

    /// <summary>Publishes every row with recorded but unpublished reports.</summary>
    /// <returns>How many rows were published.</returns>
    public int Flush()
    {
        return Flush(int.MaxValue);
    }

    /// <summary>
    /// Publishes up to <paramref name="maxRows"/> rows with recorded but unpublished reports, oldest
    /// first. Lets a host spread a large backlog over several UI frames; see <see cref="PendingRowCount"/>.
    /// </summary>
    /// <param name="maxRows">The most rows to publish in this call.</param>
    /// <returns>How many rows were published.</returns>
    public int Flush(int maxRows)
    {
        int published = 0;
        while (published < maxRows && _dirty.Count > 0)
        {
            EntityHealthItemViewModel item = _dirty.Dequeue();
            if (!item.IsDirty)
            {
                continue; // Cleared while pending.
            }

            if (!item.IsPublished)
            {
                Items.Add(item);
            }

            item.Publish(Colors, _renderCount, Receipt);
            published++;
        }

        return published;
    }

    /// <summary>
    /// Advances the render counter and re-resolves every row's colour and receipt opacity, so a
    /// configured <see cref="FlashCadence"/> and <see cref="Receipt"/> both animate. Call this on
    /// a timer from the UI thread. Flushes any recorded rows first, so a host using
    /// <see cref="Record(EntityHealthReport, EntitySample?, string?)"/> can rely on this timer alone.
    /// </summary>
    public void RenderTick()
    {
        _renderCount++;
        Flush();

        foreach (EntityHealthItemViewModel item in Items)
        {
            EntityHealthReport? report = item.LatestReport;
            if (report != null)
            {
                item.Color = Colors.Resolve(report, _renderCount);
            }

            item.ReceiptOpacity = Receipt.Resolve(_renderCount - item.LastUpdatedRenderCount);
        }
    }

    /// <summary>Looks up the row for a key, if one exists.</summary>
    /// <param name="key">The key.</param>
    /// <param name="item">The row, if found; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> if a row exists for <paramref name="key"/>.</returns>
    public bool TryGetItem(EntityKey key, out EntityHealthItemViewModel? item)
    {
        return _byKey.TryGetValue(key, out item);
    }

    /// <summary>Removes every row and forgets every key.</summary>
    public void Clear()
    {
        _byKey.Clear();
        _dirty.Clear();
        Items.Clear();
    }
}
