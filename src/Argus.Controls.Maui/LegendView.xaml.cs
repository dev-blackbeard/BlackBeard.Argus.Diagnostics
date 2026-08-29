using Argus.Controls;
using Argus.Graphics;
using Microsoft.Maui.Controls;

namespace Argus.Controls.Maui;

/// <summary>A legend explaining what a nearby <see cref="EntityHealthCollectionView"/>'s colours and icons mean.</summary>
/// <remarks>
/// Set <see cref="Colors"/> to the same <see cref="ColorPolicy"/> the collection view next to it
/// is using (typically <c>Board.Colors</c>), so the legend's swatches and icons always match what
/// is actually on screen rather than a hardcoded guess at the palette. Leaving it unset falls
/// back to a default-constructed <see cref="ColorPolicy"/>.
/// </remarks>
public partial class LegendView : ContentView
{
    /// <summary>Backing store for <see cref="Colors"/>.</summary>
    public static readonly BindableProperty ColorsProperty = BindableProperty.Create(
        nameof(Colors),
        typeof(ColorPolicy),
        typeof(LegendView),
        propertyChanged: OnColorsChanged);

    /// <summary>Creates the control.</summary>
    public LegendView()
    {
        InitializeComponent();
        RefreshEntries();
    }

    /// <summary>The colour policy this legend explains. Defaults to library colours if unset.</summary>
    public ColorPolicy? Colors
    {
        get { return (ColorPolicy?)GetValue(ColorsProperty); }
        set { SetValue(ColorsProperty, value); }
    }

    private static void OnColorsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((LegendView)bindable).RefreshEntries();
    }

    private void RefreshEntries()
    {
        ColorPolicy policy = Colors ?? new ColorPolicy();
        ItemsView.ItemsSource = LegendCatalogue.BuildEntries(policy);
    }
}
