using System.Collections.Generic;
using Argus.Contracts;
using Microsoft.Maui.Graphics;

namespace Argus.Graphics;

/// <summary>
/// Turns a report into a single colour, by severity precedence.
/// </summary>
/// <remarks>
/// <para>
/// This is the only place in Argus that knows what a colour is. <c>Argus.Core</c> emits
/// flags and findings and has no opinion about how they look (architecture rule 1) — which
/// is what lets the same findings drive a map, a log, a CSV and a message to the team
/// producing the stream without any of them being privileged.
/// </para>
/// <para>
/// The reduction from a set of flags to one colour happens <i>here</i>, at the last possible
/// moment, and it never happens during detection. That distinction is architecture rule 4:
/// an entity can be a group outlier and carry a non-normalised quaternion at once, both
/// findings are reported, and only the pixel has to pick.
/// </para>
/// <para>
/// The default precedence is by category, most severe first:
/// </para>
/// <list type="number">
/// <item><description><b>Encoding</b> — the fault renders as a plausible value, so nothing
/// downstream will notice it and no human looking at a map will either. Highest priority
/// precisely because it is the least visible.</description></item>
/// <item><description><b>Temporal</b> — ordering and cadence faults corrupt every derived
/// quantity computed from the stream, so they explain other findings.</description></item>
/// <item><description><b>Kinematic</b> — usually visible to a human eventually, and often a
/// symptom of one of the two above rather than a cause.</description></item>
/// <item><description><b>Group</b> — a relationship rather than a property of the entity;
/// frequently one entity's fault showing up on its neighbours.</description></item>
/// <item><description><b>Attitude</b> — real, but rarely the thing that makes a stream
/// unusable.</description></item>
/// </list>
/// </remarks>
public sealed class ColorPolicy
{
    private readonly Dictionary<HealthFlagCategory, Color> _categoryColors;
    private readonly Dictionary<HealthFlags, Color> _flagOverrides = new Dictionary<HealthFlags, Color>();

    /// <summary>Creates a policy with the default palette and precedence.</summary>
    public ColorPolicy()
    {
        _categoryColors = new Dictionary<HealthFlagCategory, Color>
        {
            [HealthFlagCategory.Encoding] = new Color(0.85f, 0.15f, 0.15f),
            [HealthFlagCategory.Temporal] = new Color(0.60f, 0.25f, 0.75f),
            [HealthFlagCategory.Kinematic] = new Color(0.95f, 0.45f, 0.10f),
            [HealthFlagCategory.Group] = new Color(0.95f, 0.72f, 0.15f),
            [HealthFlagCategory.Attitude] = new Color(0.90f, 0.88f, 0.20f),
        };
    }

    /// <summary>The category order used to pick a colour, most severe first.</summary>
    public static IReadOnlyList<HealthFlagCategory> DefaultPrecedence { get; } = new[]
    {
        HealthFlagCategory.Encoding,
        HealthFlagCategory.Temporal,
        HealthFlagCategory.Kinematic,
        HealthFlagCategory.Group,
        HealthFlagCategory.Attitude,
    };

    /// <summary>The colour for an entity with no flags raised and nothing left unchecked.</summary>
    public Color HealthyColor { get; set; } = new Color(0.20f, 0.70f, 0.35f);

    /// <summary>
    /// The colour for an entity with no flags raised but at least one detector unable to run.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="HealthyColor"/> on purpose. "Nothing wrong was found" and
    /// "some things were not checked" are different claims, and painting them the same colour
    /// is how a stream ends up being described as verified when half the catalogue never ran.
    /// </remarks>
    public Color NotEvaluatedColor { get; set; } = new Color(0.55f, 0.58f, 0.62f);

    /// <summary>The category order used to pick a colour, most severe first.</summary>
    public IReadOnlyList<HealthFlagCategory> Precedence { get; set; } = DefaultPrecedence;

    /// <summary>An optional flash cadence applied to flagged colours.</summary>
    /// <remarks>
    /// This lives here, and not in a detector, because a render count is presentation state.
    /// The prototype kept its flash cadence inside the detector, which coupled how often the
    /// screen redrew to what the diagnostics said — so the same stream produced different
    /// findings on a slower machine.
    /// </remarks>
    public FlashCadence? Flash { get; set; }

    /// <summary>Overrides the colour used for one specific flag.</summary>
    /// <param name="flag">The flag.</param>
    /// <param name="color">The colour to use when it is the most severe flag raised.</param>
    /// <returns>This policy, so overrides can be chained.</returns>
    public ColorPolicy Override(HealthFlags flag, Color color)
    {
        _flagOverrides[flag] = color;
        return this;
    }

    /// <summary>Sets the colour used for a whole category.</summary>
    /// <param name="category">The category.</param>
    /// <param name="color">The colour.</param>
    /// <returns>This policy, so changes can be chained.</returns>
    public ColorPolicy SetCategoryColor(HealthFlagCategory category, Color color)
    {
        _categoryColors[category] = color;
        return this;
    }

    /// <summary>Picks the colour for a report.</summary>
    /// <param name="report">The report.</param>
    /// <returns>The colour.</returns>
    public Color Resolve(EntityHealthReport report)
    {
        if (report == null)
        {
            return NotEvaluatedColor;
        }

        if (report.Flags == HealthFlags.None)
        {
            return report.IsFullyEvaluated ? HealthyColor : NotEvaluatedColor;
        }

        return GetColorForFlag(MostSevere(report.Flags));
    }

    /// <summary>
    /// Picks the colour for a single flag, independent of any report: an override if one is set
    /// for it, otherwise its category's colour.
    /// </summary>
    /// <param name="flag">A single-bit flag value.</param>
    /// <returns>The colour a row's per-flag alarm chip or a legend entry for this flag should use.</returns>
    /// <remarks>
    /// The single-flag counterpart to <see cref="Resolve(EntityHealthReport)"/>'s multi-flag,
    /// precedence-driven answer — used wherever a caller already knows which one flag it means
    /// (a per-flag chip, a legend entry) rather than needing this policy to pick the most severe
    /// one out of several.
    /// </remarks>
    public Color GetColorForFlag(HealthFlags flag)
    {
        Color color;
        if (_flagOverrides.TryGetValue(flag, out color))
        {
            return color;
        }

        HealthFlagCategory category = HealthFlagInfo.GetCategory(flag);
        if (_categoryColors.TryGetValue(category, out color))
        {
            return color;
        }

        return NotEvaluatedColor;
    }

    /// <summary>Picks the colour for a report and applies the flash cadence, if one is set.</summary>
    /// <param name="report">The report.</param>
    /// <param name="renderCount">The host's render counter.</param>
    /// <returns>The colour.</returns>
    public Color Resolve(EntityHealthReport report, long renderCount)
    {
        Color color = Resolve(report);

        if (Flash == null || report == null || report.Flags == HealthFlags.None)
        {
            return color;
        }

        return Flash.Apply(color, renderCount);
    }

    /// <summary>
    /// Returns the single most severe flag in a combination, by the configured precedence.
    /// </summary>
    /// <param name="flags">The flags raised.</param>
    /// <returns>The most severe single flag, or <see cref="HealthFlags.None"/> if none were raised.</returns>
    public HealthFlags MostSevere(HealthFlags flags)
    {
        if (flags == HealthFlags.None)
        {
            return HealthFlags.None;
        }

        IReadOnlyList<HealthFlagCategory> precedence = Precedence;
        for (int i = 0; i < precedence.Count; i++)
        {
            foreach (HealthFlags flag in HealthFlagInfo.Split(flags))
            {
                if (HealthFlagInfo.GetCategory(flag) == precedence[i])
                {
                    return flag;
                }
            }
        }

        foreach (HealthFlags flag in HealthFlagInfo.Split(flags))
        {
            return flag;
        }

        return HealthFlags.None;
    }
}
