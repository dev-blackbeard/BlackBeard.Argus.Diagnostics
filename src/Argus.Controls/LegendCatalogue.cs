using System;
using System.Collections.Generic;
using Argus.Contracts;
using Argus.Graphics;
using Microsoft.Maui.Graphics;

namespace Argus.Controls;

/// <summary>Builds the rows a legend control shows: the health-state colours, then one row per alarm.</summary>
/// <remarks>
/// <para>
/// Colours are resolved from the caller's own <see cref="ColorPolicy"/> rather than hardcoded
/// here, so a legend always matches whatever palette the collection view next to it is actually
/// using — including a host's <see cref="ColorPolicy.Override"/>/<see cref="ColorPolicy.SetCategoryColor"/>
/// customisation.
/// </para>
/// </remarks>
public static class LegendCatalogue
{
    /// <summary>
    /// The flags a legend lists individually.
    /// </summary>
    /// <remarks>
    /// Deliberately narrower than <see cref="HealthFlagInfo.All"/>: only these have an
    /// implemented detector behind them today (see <c>docs/detector-catalogue.md</c> in this
    /// repository), so only these can ever actually appear in a report. Listing the rest would
    /// show the user alarms that can never fire. Add a flag here as its detector is implemented —
    /// the same discipline the lab repository's <c>FaultScenarioCatalogue</c> and this
    /// repository's own <c>GoldenCases</c> already apply, and give it a real shape in
    /// <see cref="AlarmIconPainter"/> at the same time.
    /// </remarks>
    public static IReadOnlyList<HealthFlags> ImplementedFlags { get; } = new[]
    {
        HealthFlags.NonPositiveDeltaTime,
        HealthFlags.DuplicateSample,
        HealthFlags.OutOfOrderSequence,
        HealthFlags.NonFiniteValue,
        HealthFlags.Teleport,
        HealthFlags.ImplausibleSpeed,
        HealthFlags.NonNormalisedQuaternion,
        HealthFlags.GroupOutlier,
    };

    /// <summary>Builds the full set of legend rows for a colour policy.</summary>
    /// <param name="colors">The policy whose colours the legend should reflect.</param>
    /// <returns>The health-state rows, then one row per <see cref="ImplementedFlags"/> entry, in that order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="colors"/> is <c>null</c>.</exception>
    public static IReadOnlyList<LegendEntry> BuildEntries(ColorPolicy colors)
    {
        if (colors == null)
        {
            throw new ArgumentNullException(nameof(colors));
        }

        var entries = new List<LegendEntry>(ImplementedFlags.Count + 2)
        {
            new LegendEntry(
                "Healthy",
                HealthFlagInfo.GetDefinition(HealthFlags.None),
                colors.HealthyColor,
                ContrastColor.ForBackground(colors.HealthyColor),
                null),
            new LegendEntry(
                "Not evaluated",
                "One or more detectors could not run against this sample, so nothing was concluded about the condition they cover.",
                colors.NotEvaluatedColor,
                ContrastColor.ForBackground(colors.NotEvaluatedColor),
                null),
        };

        foreach (HealthFlags flag in ImplementedFlags)
        {
            Color background = colors.GetColorForFlag(flag);
            entries.Add(new LegendEntry(flag.ToString(), HealthFlagInfo.GetDefinition(flag), background, ContrastColor.ForBackground(background), flag));
        }

        return entries.AsReadOnly();
    }
}
