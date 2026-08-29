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
/// This is the "push new data in" entry point: call
/// <see cref="Observe(EntityHealthReport, EntitySample?, string?)"/> once per
/// <c>IEntityStreamMonitor.Observe</c> call, from whichever thread produced the report.
/// <see cref="Items"/> is an <see cref="ObservableCollection{T}"/>, so it must only be mutated on
/// the thread a bound UI expects — marshal to that thread before calling
/// <see cref="Observe(EntityHealthReport, EntitySample?, string?)"/> or <see cref="RenderTick"/> if
/// the report was produced elsewhere. This mirrors the recommended shape in
/// <c>docs/threading.md</c>: observe off-thread, hand only the immutable report (and, here, the
/// sample) across, and do the UI-affecting work on the UI thread.
/// </remarks>
public sealed class EntityHealthCollection
{
    private readonly Dictionary<EntityKey, EntityHealthItemViewModel> _byKey = new Dictionary<EntityKey, EntityHealthItemViewModel>();
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

    /// <summary>Records one observed report, creating or updating the row for its key.</summary>
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
    public void Observe(EntityHealthReport report, EntitySample? sample = null, string? groupTag = null)
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
            Items.Add(item);
        }

        item.Apply(report, sample);
        item.Color = Colors.Resolve(report, _renderCount);
    }

    /// <summary>
    /// Advances the render counter and re-resolves every row's colour, so a configured
    /// <see cref="FlashCadence"/> animates. Call this on a timer from the UI thread.
    /// </summary>
    public void RenderTick()
    {
        _renderCount++;

        foreach (EntityHealthItemViewModel item in Items)
        {
            EntityHealthReport? report = item.LatestReport;
            if (report != null)
            {
                item.Color = Colors.Resolve(report, _renderCount);
            }
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
        Items.Clear();
    }
}
