using Argus.Controls;
using Microsoft.Maui.Controls;

namespace Argus.Controls.Maui;

/// <summary>
/// A <see cref="CollectionView"/>-based control that visualises an <see cref="EntityHealthCollection"/>:
/// one row per entity, 6DOF fields, per-flag anomaly counts, and colour/flash by severity.
/// </summary>
/// <remarks>
/// Set <see cref="Board"/> to the collection a host application is pushing observed reports into
/// (see <see cref="EntityHealthCollection.Observe(Argus.Contracts.EntityHealthReport, Argus.Contracts.EntitySample?, string?)"/>);
/// this control only renders it and never mutates it.
/// </remarks>
public partial class EntityHealthCollectionView : ContentView
{
    /// <summary>Backing store for <see cref="Board"/>.</summary>
    public static readonly BindableProperty BoardProperty = BindableProperty.Create(
        nameof(Board),
        typeof(EntityHealthCollection),
        typeof(EntityHealthCollectionView),
        propertyChanged: OnBoardChanged);

    /// <summary>Creates the control.</summary>
    public EntityHealthCollectionView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The collection this control renders. Its <see cref="EntityHealthCollection.Items"/> drives
    /// the list; nothing here mutates it.
    /// </summary>
    public EntityHealthCollection? Board
    {
        get { return (EntityHealthCollection?)GetValue(BoardProperty); }
        set { SetValue(BoardProperty, value); }
    }

    private static void OnBoardChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (EntityHealthCollectionView)bindable;
        var board = newValue as EntityHealthCollection;
        view.ItemsView.ItemsSource = board?.Items;
    }
}
