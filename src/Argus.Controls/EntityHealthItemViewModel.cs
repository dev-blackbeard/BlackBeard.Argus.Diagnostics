using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Argus.Contracts;
using Argus.Graphics;
using Microsoft.Maui.Graphics;

namespace Argus.Controls;

/// <summary>
/// The presentation state for one row: the latest report, the correlated sample, cumulative
/// per-flag counts, and whether its detail section is expanded.
/// </summary>
/// <remarks>
/// Built and updated exclusively by <see cref="EntityHealthCollection"/> — there is no public way
/// to construct or mutate its report/sample/colour state directly, because the per-flag count
/// invariant (it reflects every report the collection has ever observed for this key) only holds
/// if nothing outside the collection can write to it out of order.
/// </remarks>
public sealed class EntityHealthItemViewModel : INotifyPropertyChanged
{
    private readonly Dictionary<HealthFlags, long> _flagCounts = new Dictionary<HealthFlags, long>();
    private EntityHealthReport? _report;
    private EntitySample? _sample;
    private Color _color = Colors.Transparent;
    private double _receiptOpacity;
    private bool _isExpanded;
    private IReadOnlyList<AlarmChipViewModel> _alarmChips = new List<AlarmChipViewModel>(0).AsReadOnly();
    private EntityHealthReport? _pendingReport;
    private EntitySample? _pendingSample;
    private bool _countsChanged;

    internal EntityHealthItemViewModel(EntityKey key)
    {
        Key = key;
        ToggleExpandedCommand = new ActionCommand(() => IsExpanded = !IsExpanded);
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The uniqueness key this row is keyed by.</summary>
    public EntityKey Key { get; }

    /// <summary>The entity's stable identity, as reported by the stream.</summary>
    public string EntityId
    {
        get { return Key.EntityId; }
    }

    /// <summary>The disambiguator supplied by whoever is feeding the collection, if any.</summary>
    public string? GroupTag
    {
        get { return Key.GroupTag; }
    }

    /// <summary>The most recent report observed for this key.</summary>
    public EntityHealthReport? LatestReport
    {
        get { return _report; }
        private set { SetField(ref _report, value); }
    }

    /// <summary>The 6DOF sample correlated with <see cref="LatestReport"/>, if the caller supplied one.</summary>
    public EntitySample? LatestSample
    {
        get { return _sample; }
        private set
        {
            if (SetField(ref _sample, value) && _isExpanded)
            {
                OnPropertyChanged(nameof(ExpandedSample));
            }
        }
    }

    /// <summary>
    /// <see cref="LatestSample"/> while <see cref="IsExpanded"/> is <c>true</c>; <c>null</c> while the
    /// row is collapsed. Bind a row's detail-only fields to this rather than to <see cref="LatestSample"/>.
    /// </summary>
    /// <remarks>
    /// A binding to a hidden element still re-evaluates on every change notification of its source,
    /// so detail fields bound to <see cref="LatestSample"/> cost their full update on every report
    /// even while nobody can see them. This property raises nothing while the row is collapsed, so
    /// those bindings go quiet; expanding the row raises it once, which fills them in.
    /// </remarks>
    public EntitySample? ExpandedSample
    {
        get { return _isExpanded ? _sample : null; }
    }

    /// <summary>How many times each flag has been raised for this key, across every report observed.</summary>
    public IReadOnlyDictionary<HealthFlags, long> FlagCounts
    {
        get { return _flagCounts; }
    }

    /// <summary>
    /// <see cref="FlagCounts"/>, as ready-to-render chips: one per flag that has fired at least
    /// once, each already carrying its own resolved colour. Never contains a zero-count entry —
    /// a flag only appears here once a report has actually raised it, the same
    /// invariant <see cref="FlagCounts"/> itself holds.
    /// </summary>
    public IReadOnlyList<AlarmChipViewModel> AlarmChips
    {
        get { return _alarmChips; }
        private set { SetField(ref _alarmChips, value); }
    }

    /// <summary>The colour currently resolved for this row, including any flash cadence applied.</summary>
    public Color Color
    {
        get { return _color; }
        internal set { SetField(ref _color, value); }
    }

    /// <summary>
    /// A brief, low-emphasis pulse driven by <see cref="EntityHealthCollection"/>'s configured
    /// <see cref="Argus.Graphics.ReceiptPulse"/>, fading from its peak toward zero after each
    /// observed report. Distinct from <see cref="Color"/>: this says "new data arrived", not
    /// "something is wrong" — bind a separate, subtler visual element to it than the one bound to
    /// <see cref="Color"/>.
    /// </summary>
    public double ReceiptOpacity
    {
        get { return _receiptOpacity; }
        internal set { SetField(ref _receiptOpacity, value); }
    }

    /// <summary>
    /// The host collection's render count at the point this row was last <see cref="Publish"/>-ed.
    /// Presentation bookkeeping for <see cref="ReceiptOpacity"/>'s fade, not meant for a binding.
    /// </summary>
    internal long LastUpdatedRenderCount { get; set; }

    /// <summary>Whether the row's "more" section — the remaining 6DOF fields — is expanded.</summary>
    public bool IsExpanded
    {
        get { return _isExpanded; }
        set
        {
            if (SetField(ref _isExpanded, value))
            {
                OnPropertyChanged(nameof(ExpandedSample));
            }
        }
    }

    /// <summary>Toggles <see cref="IsExpanded"/>. Bind a UI's expander control to this rather than setting <see cref="IsExpanded"/> directly, so the binding needs no code-behind.</summary>
    public ICommand ToggleExpandedCommand { get; }

    /// <summary>Whether a report has been recorded for this row since it was last published.</summary>
    internal bool IsDirty { get; private set; }

    /// <summary>Whether this row has been published at least once, and so is in the bound list.</summary>
    internal bool IsPublished { get; private set; }

    /// <summary>
    /// Counts a report's flags and remembers it as the row's pending latest report, without raising
    /// any change notification. Cheap enough to run for every report, which is what keeps
    /// <see cref="FlagCounts"/> exact however rarely the row is published.
    /// </summary>
    /// <param name="report">The report.</param>
    /// <param name="sample">The sample it was produced from, if the caller has it.</param>
    /// <returns><c>true</c> if the row was clean before this call, i.e. it has just become dirty.</returns>
    internal bool Accumulate(EntityHealthReport report, EntitySample? sample)
    {
        bool raisedAny = false;
        foreach (HealthFlags flag in HealthFlagInfo.Split(report.Flags))
        {
            long count;
            _flagCounts.TryGetValue(flag, out count);
            _flagCounts[flag] = count + 1L;
            raisedAny = true;
        }

        _pendingReport = report;
        _pendingSample = sample;
        _countsChanged |= raisedAny;

        bool becameDirty = !IsDirty;
        IsDirty = true;
        return becameDirty;
    }

    /// <summary>
    /// Pushes the pending state to the bindable properties, raising each change notification at most
    /// once no matter how many reports were accumulated since the last publish.
    /// </summary>
    /// <param name="colors">The policy to resolve the row and chip colours from.</param>
    /// <param name="renderCount">The host collection's current render count.</param>
    /// <param name="receipt">The receipt pulse to restart.</param>
    internal void Publish(ColorPolicy colors, long renderCount, ReceiptPulse receipt)
    {
        IsDirty = false;
        IsPublished = true;

        EntityHealthReport report = _pendingReport!;
        LatestReport = report;
        LatestSample = _pendingSample;

        // Rebuilding AlarmChips hands a bound list control a brand-new list, which recreates every
        // chip's visual. Only do it when a count actually moved, which for a healthy stream is
        // almost never, rather than on every report as before.
        if (_countsChanged)
        {
            _countsChanged = false;
            OnPropertyChanged(nameof(FlagCounts));
            RefreshAlarmChips(colors);
        }

        Color = colors.Resolve(report, renderCount);
        LastUpdatedRenderCount = renderCount;
        ReceiptOpacity = receipt.Resolve(0);
    }

    /// <summary>Rebuilds <see cref="AlarmChips"/> from the current <see cref="FlagCounts"/>.</summary>
    /// <param name="colors">The policy to resolve each chip's colour from.</param>
    private void RefreshAlarmChips(ColorPolicy colors)
    {
        var chips = new List<AlarmChipViewModel>(_flagCounts.Count);
        foreach (KeyValuePair<HealthFlags, long> pair in _flagCounts)
        {
            Color background = colors.GetColorForFlag(pair.Key);
            chips.Add(new AlarmChipViewModel(pair.Key, pair.Value, background, ContrastColor.ForBackground(background)));
        }

        AlarmChips = chips.AsReadOnly();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// A minimal <see cref="ICommand"/> that always executes. <see cref="System.Windows.Input.ICommand"/>
    /// is a plain BCL interface — using it here, rather than a UI framework's richer command type,
    /// is what lets this view-model stay free of any Microsoft.Maui.Controls dependency.
    /// </summary>
    private sealed class ActionCommand : ICommand
    {
        private readonly Action _action;

        public ActionCommand(Action action)
        {
            _action = action;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter)
        {
            return true;
        }

        public void Execute(object? parameter)
        {
            _action();
        }
    }
}
