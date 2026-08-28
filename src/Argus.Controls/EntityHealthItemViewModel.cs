using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Argus.Contracts;
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
    private bool _isExpanded;

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
        private set { SetField(ref _sample, value); }
    }

    /// <summary>How many times each flag has been raised for this key, across every report observed.</summary>
    public IReadOnlyDictionary<HealthFlags, long> FlagCounts
    {
        get { return _flagCounts; }
    }

    /// <summary>The colour currently resolved for this row, including any flash cadence applied.</summary>
    public Color Color
    {
        get { return _color; }
        internal set { SetField(ref _color, value); }
    }

    /// <summary>Whether the row's "more" section — the remaining 6DOF fields — is expanded.</summary>
    public bool IsExpanded
    {
        get { return _isExpanded; }
        set { SetField(ref _isExpanded, value); }
    }

    /// <summary>Toggles <see cref="IsExpanded"/>. Bind a UI's expander control to this rather than setting <see cref="IsExpanded"/> directly, so the binding needs no code-behind.</summary>
    public ICommand ToggleExpandedCommand { get; }

    internal void Apply(EntityHealthReport report, EntitySample? sample)
    {
        foreach (HealthFlags flag in HealthFlagInfo.Split(report.Flags))
        {
            long count;
            _flagCounts.TryGetValue(flag, out count);
            _flagCounts[flag] = count + 1L;
        }

        LatestReport = report;
        LatestSample = sample;
        OnPropertyChanged(nameof(FlagCounts));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
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
