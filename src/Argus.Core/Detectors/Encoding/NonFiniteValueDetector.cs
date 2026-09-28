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

    // Results that carry nothing specific to one sample are built once: HealthFinding is
    // immutable, and allocating an identical one per sample was pure garbage-collector load.
    private static readonly DetectorResult NoNumericFields =
        DetectorResult.NotEvaluable(HealthFlags.NonFiniteValue, DetectorId, "the sample supplied no numeric fields to inspect");

    private static readonly DetectorResult HealthyUnrecorded = DetectorResult.HealthyWithoutDetail(HealthFlags.NonFiniteValue, DetectorId);

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

        // Created only when a field fails, which on a healthy stream is never.
        List<string>? offenders = null;
        int inspected = 0;

        EntitySample sample = context.Sample;
        Inspect("Latitude", sample.Latitude, ref offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("Longitude", sample.Longitude, ref offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("Altitude", sample.Altitude, ref offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("RollDegrees", sample.RollDegrees, ref offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("PitchDegrees", sample.PitchDegrees, ref offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("YawDegrees", sample.YawDegrees, ref offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("HeadingDegrees", sample.HeadingDegrees, ref offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("QuaternionX", sample.QuaternionX, ref offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("QuaternionY", sample.QuaternionY, ref offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("QuaternionZ", sample.QuaternionZ, ref offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("QuaternionW", sample.QuaternionW, ref offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("VelocityNorthMetersPerSecond", sample.VelocityNorthMetersPerSecond, ref offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("VelocityEastMetersPerSecond", sample.VelocityEastMetersPerSecond, ref offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("VelocityDownMetersPerSecond", sample.VelocityDownMetersPerSecond, ref offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("AngularVelocityXDegreesPerSecond", sample.AngularVelocityXDegreesPerSecond, ref offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("AngularVelocityYDegreesPerSecond", sample.AngularVelocityYDegreesPerSecond, ref offenders, ref inspected, treatSubnormalAsNonFinite);
        Inspect("AngularVelocityZDegreesPerSecond", sample.AngularVelocityZDegreesPerSecond, ref offenders, ref inspected, treatSubnormalAsNonFinite);

        IReadOnlyList<RawField>? rawFields = sample.RawFields;
        if (rawFields != null)
        {
            for (int i = 0; i < rawFields.Count; i++)
            {
                Inspect(rawFields[i].Name, rawFields[i].Value, ref offenders, ref inspected, treatSubnormalAsNonFinite);
            }
        }

        string expected = treatSubnormalAsNonFinite
            ? "every supplied field finite and normal"
            : "every supplied field finite";

        if (inspected == 0)
        {
            return NoNumericFields;
        }

        if (offenders != null)
        {
            return DetectorResult.Flagged(
                Flag,
                DetectorId,
                string.Join(", ", offenders.ToArray()),
                expected,
                offenders.Count,
                "fields");
        }

        if (!context.RecordHealthyDetail)
        {
            return HealthyUnrecorded;
        }

        return DetectorResult.Healthy(Flag, DetectorId, inspected + " fields finite", expected, 0.0, "fields");
    }

    private static void Inspect(string name, double? value, ref List<string>? offenders, ref int inspected, bool treatSubnormalAsNonFinite)
    {
        if (!value.HasValue)
        {
            return;
        }

        inspected++;
        double actual = value.Value;

        if (double.IsNaN(actual))
        {
            (offenders ??= new List<string>()).Add(name + "=NaN");
        }
        else if (double.IsPositiveInfinity(actual))
        {
            (offenders ??= new List<string>()).Add(name + "=+Infinity");
        }
        else if (double.IsNegativeInfinity(actual))
        {
            (offenders ??= new List<string>()).Add(name + "=-Infinity");
        }
        else if (treatSubnormalAsNonFinite && Geo.IsSubnormal(actual))
        {
            (offenders ??= new List<string>()).Add(name + "=subnormal");
        }
    }
}
