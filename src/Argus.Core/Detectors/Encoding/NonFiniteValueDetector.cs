using System.Collections.Generic;
using Argus.Contracts;
using Argus.Geodesy;

namespace Argus.Detectors.Encoding;

/// <summary>
/// Reports a field carrying NaN, an infinity, or a subnormal value.
/// </summary>
/// <remarks>
/// <para>
/// The cheapest and least ambiguous encoding check there is, and the reason it leads the
/// category: nothing physical is NaN. A NaN in a position field is proof the bytes were
/// never a position — the frame was truncated, misaligned, or read past its end — and it
/// says so with no threshold and no tuning.
/// </para>
/// <para>
/// Subnormals are included, controlled by
/// <c>DetectorThresholds.TreatSubnormalAsNonFinite</c>. A subnormal is a valid double but
/// an impossible measurement: it is what you get when a field's bytes are the tail of an
/// adjacent field or a fragment of filler, and it will render on a map as a position
/// indistinguishable from the origin rather than as an obvious error.
/// </para>
/// </remarks>
public sealed class NonFiniteValueDetector : IDetector
{
    /// <summary>The stable identifier this detector stamps on its findings.</summary>
    public const string DetectorId = "argus.encoding.non-finite-value";

    /// <inheritdoc />
    public string Id
    {
        get { return DetectorId; }
    }

    /// <inheritdoc />
    public HealthFlags Flag
    {
        get { return HealthFlags.NonFiniteValue; }
    }

    /// <inheritdoc />
    public DetectorStatus Status
    {
        get { return DetectorStatus.Implemented; }
    }

    /// <inheritdoc />
    public DetectorResult Evaluate(DetectorContext context)
    {
        bool treatSubnormalAsNonFinite = context.Thresholds.TreatSubnormalAsNonFinite;

        var offenders = new List<string>();
        int inspected = 0;

        EntitySample sample = context.Sample;
        Inspect("Latitude", sample.Latitude, offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("Longitude", sample.Longitude, offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("Altitude", sample.Altitude, offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("RollDegrees", sample.RollDegrees, offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("PitchDegrees", sample.PitchDegrees, offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("YawDegrees", sample.YawDegrees, offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("HeadingDegrees", sample.HeadingDegrees, offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("QuaternionX", sample.QuaternionX, offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("QuaternionY", sample.QuaternionY, offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("QuaternionZ", sample.QuaternionZ, offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("QuaternionW", sample.QuaternionW, offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("VelocityNorthMetersPerSecond", sample.VelocityNorthMetersPerSecond, offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("VelocityEastMetersPerSecond", sample.VelocityEastMetersPerSecond, offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("VelocityDownMetersPerSecond", sample.VelocityDownMetersPerSecond, offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("AngularVelocityXDegreesPerSecond", sample.AngularVelocityXDegreesPerSecond, offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("AngularVelocityYDegreesPerSecond", sample.AngularVelocityYDegreesPerSecond, offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("AngularVelocityZDegreesPerSecond", sample.AngularVelocityZDegreesPerSecond, offenders, ref inspected, treatSubnormalAsNonFinite);

        IReadOnlyList<RawField>? rawFields = sample.RawFields;
        if (rawFields != null)
        {
            for (int i = 0; i < rawFields.Count; i++)
            {
                Inspect(rawFields[i].Name, rawFields[i].Value, offenders, ref inspected, treatSubnormalAsNonFinite);
            }
        }

        string expected = treatSubnormalAsNonFinite
            ? "every supplied field finite and normal"
            : "every supplied field finite";

        if (inspected == 0)
        {
            return DetectorResult.NotEvaluable(
                Flag,
                DetectorId,
                "the sample supplied no numeric fields to inspect");
        }

        if (offenders.Count > 0)
        {
            return DetectorResult.Flagged(
                Flag,
                DetectorId,
                string.Join(", ", offenders.ToArray()),
                expected,
                offenders.Count,
                "fields");
        }

        return DetectorResult.Healthy(Flag, DetectorId, inspected + " fields finite", expected, 0.0, "fields");
    }

    private static void Inspect(string name, double? value, List<string> offenders, ref int inspected, bool treatSubnormalAsNonFinite)
    {
        if (!value.HasValue)
        {
            return;
        }

        inspected++;
        double actual = value.Value;

        if (double.IsNaN(actual))
        {
            offenders.Add(name + "=NaN");
        }
        else if (double.IsPositiveInfinity(actual))
        {
            offenders.Add(name + "=+Infinity");
        }
        else if (double.IsNegativeInfinity(actual))
        {
            offenders.Add(name + "=-Infinity");
        }
        else if (treatSubnormalAsNonFinite && Geo.IsSubnormal(actual))
        {
            offenders.Add(name + "=subnormal");
        }
    }
}
