using System.Collections.Generic;
using Argus.Controls;
using Argus.Graphics;
using Microsoft.Maui.Controls;

namespace Argus.Controls.Maui;

/// <summary>A legend explaining what a nearby <see cref="EntityHealthCollectionView"/>'s colours and icons mean.</summary>
/// <remarks>
/// <para>
/// Set <see cref="Colors"/> to the same <see cref="ColorPolicy"/> the collection view next to it
/// is using (typically <c>Board.Colors</c>), so the legend's swatches and icons always match what
/// is actually on screen rather than a hardcoded guess at the palette. Leaving it unset falls
/// back to a default-constructed <see cref="ColorPolicy"/>.
/// </para>
/// <para>
/// A two-column grid of icon-plus-title cells, not a single scrolling column of icon, title and
/// full definition. The first version was the latter, and its own scrollbar was the only signal
/// that there was more to see — easy to miss entirely, which defeats a legend. Tap an entry for
/// its definition instead (<see cref="OnLegendEntryTapped"/>); <see cref="UpArrow"/>/
/// <see cref="DownArrow"/> in the XAML show or hide themselves from <see cref="OnItemsViewScrolled"/>
/// so it is visible, not just discoverable by accident, that the grid still scrolls.
/// </para>
/// </remarks>
public partial class LegendView : ContentView
{
    /// <summary>Backing store for <see cref="Colors"/>.</summary>
    public static readonly BindableProperty ColorsProperty = BindableProperty.Create(
        nameof(Colors),
        typeof(ColorPolicy),
        typeof(LegendView),
        propertyChanged: OnColorsChanged);

    private IReadOnlyList<LegendEntry> _entries = new List<LegendEntry>(0).AsReadOnly();

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
        _entries = LegendCatalogue.BuildEntries(policy);
        ItemsView.ItemsSource = _entries;
    }

    /// <summary>
    /// Shows/hides the up/down scroll arrows from where the grid actually is: an up arrow means
    /// there is something above the first visible row, a down arrow means something below the
    /// last one. Both hidden together means the whole legend fits without scrolling at all.
    /// </summary>
    private void OnItemsViewScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        UpArrow.IsVisible = e.FirstVisibleItemIndex > 0;
        DownArrow.IsVisible = e.LastVisibleItemIndex < _entries.Count - 1;
    }

    /// <summary>Shows a legend entry's full definition, since the two-column grid has no room to show it inline.</summary>
    private async void OnLegendEntryTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not VisualElement element || element.BindingContext is not LegendEntry entry)
        {
            return;
        }

        Page? page = Application.Current?.Windows.Count > 0 ? Application.Current.Windows[0].Page : null;
        if (page != null)
        {
            await page.DisplayAlert(entry.Title, entry.Definition, "OK");
        }
    }
}
