using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Argus.Contracts;

namespace Argus.Graphics;

/// <summary>
/// Renders a report as the one-line debug subtitle the facade returns.
/// </summary>
/// <remarks>
/// <para>
/// The subtitle is the smallest self-describing form of a finding (architecture rule 7): it
/// names the flag, the measured value and the expected one, so a screenshot of it is an
/// argument rather than an assertion. It deliberately does not summarise — if three
/// conditions fired, three are listed, up to <see cref="MaxFindings"/>.
/// </para>
/// <para>
/// The prototype assigned its subtitle twice, and the first assignment was dead code that
/// had been correct once and had since drifted. There is exactly one place a subtitle is
/// produced now, and this is it.
/// </para>
/// </remarks>
public sealed class SubtitleFormatter
{
    /// <summary>The most findings to list before summarising the remainder as a count.</summary>
    public int MaxFindings { get; set; } = 3;

    /// <summary>Whether the healthy subtitle includes the running sample counts.</summary>
    public bool IncludeCounters { get; set; } = true;

    /// <summary>Renders a report.</summary>
    /// <param name="report">The report, or <c>null</c>.</param>
    /// <returns>A single line, never <c>null</c>.</returns>
    public string Format(EntityHealthReport? report)
    {
        if (report == null)
        {
            return "no report";
        }

        if (report.Flags == HealthFlags.None)
        {
            return FormatHealthy(report);
        }

        var builder = new StringBuilder();
        int listed = 0;
        int total = 0;

        foreach (HealthFinding finding in report.FlaggedFindings())
        {
            total++;
            if (listed >= MaxFindings)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(" | ");
            }

            builder.Append(finding.FlagName)
                   .Append(": ")
                   .Append(finding.Measured)
                   .Append(" (expected ")
                   .Append(finding.Expected)
                   .Append(')');

            listed++;
        }

        if (total > listed)
        {
            builder.Append(string.Format(CultureInfo.InvariantCulture, " | +{0} more", total - listed));
        }

        return builder.ToString();
    }

    /// <summary>Renders the full detail of a report, one finding per line.</summary>
    /// <param name="report">The report, or <c>null</c>.</param>
    /// <returns>Every finding, including the ones no detector could evaluate.</returns>
    /// <remarks>
    /// This is the form to paste into a conversation with the team producing the stream: it
    /// carries each flag's definition, so it can be read without access to this repository.
    /// </remarks>
    public string FormatDetail(EntityHealthReport? report)
    {
        if (report == null)
        {
            return "no report";
        }

        var lines = new List<string>();
        lines.Add(report.EntityId + " at " + report.TimestampUtc.ToString("O", CultureInfo.InvariantCulture));

        foreach (HealthFinding finding in report.Findings)
        {
            lines.Add("  " + finding.ToString());
        }

        if (lines.Count == 1)
        {
            lines.Add("  no findings recorded");
        }

        return string.Join("\n", lines.ToArray());
    }

    private string FormatHealthy(EntityHealthReport report)
    {
        var builder = new StringBuilder();
        builder.Append(report.IsFullyEvaluated ? "OK" : "OK (partial)");

        if (!report.IsFullyEvaluated)
        {
            int notEvaluable = 0;
            foreach (HealthFlags flag in HealthFlagInfo.Split(report.NotEvaluableFlags))
            {
                if (flag != HealthFlags.None)
                {
                    notEvaluable++;
                }
            }

            builder.Append(string.Format(CultureInfo.InvariantCulture, ": {0} checks not evaluable", notEvaluable));
        }

        if (IncludeCounters)
        {
            builder.Append(string.Format(
                CultureInfo.InvariantCulture,
                " | {0}/{1} samples evaluated",
                report.SamplesEvaluated,
                report.SamplesObserved));

            double? health = report.HealthPercent;
            if (health.HasValue)
            {
                builder.Append(string.Format(CultureInfo.InvariantCulture, " | {0:F1}% healthy", health.Value));
            }
        }

        return builder.ToString();
    }
}
