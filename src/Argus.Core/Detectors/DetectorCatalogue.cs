using System.Collections.Generic;
using Argus.Detectors.Attitude;
using Argus.Detectors.Encoding;
using Argus.Detectors.Group;
using Argus.Detectors.Kinematic;
using Argus.Detectors.Temporal;

namespace Argus.Detectors;

/// <summary>
/// The full detector catalogue, implemented and not.
/// </summary>
/// <remarks>
/// <para>
/// This list is the specification. It deliberately includes the detectors that have not been
/// written yet, because the prototype's failure mode here was subtraction by silence: its
/// detector comments ran 1, 3, 4, and nobody could tell whether detector 2 had been removed
/// on purpose, folded into another, or lost. A stub that announces itself is recoverable; a
/// gap in a numbering scheme is archaeology.
/// </para>
/// <para>
/// Order is presentation order, not evaluation order — every detector runs on every sample
/// regardless (architecture rule 4).
/// </para>
/// </remarks>
public static class DetectorCatalogue
{
    /// <summary>Creates one instance of every detector in the catalogue.</summary>
    /// <returns>The detectors, in catalogue order.</returns>
    public static IReadOnlyList<IDetector> CreateAll()
    {
        var detectors = new List<IDetector>
        {
            // Temporal
            new NonPositiveDeltaTimeDetector(),
            new DuplicateSampleDetector(),
            new OutOfOrderSequenceDetector(),
            new SequenceGapDetector(),
            new FrozenEntityDetector(),
            new UpdateRateDriftDetector(),
            new ClockSkewDetector(),

            // Encoding and framing. Highest priority in the catalogue: this stream originates
            // from a serial protocol marshalled into structs, so the faults that dominate are
            // faults of representation, and those are the ones that render as plausible values.
            new NonFiniteValueDetector(),
            new ByteOrderSwapDetector(),
            new FixedPointScaleDetector(),
            new RadiansAsDegreesDetector(),
            new AxisSwapDetector(),
            new FieldShiftDetector(),
            new QuantisationCollapseDetector(),
            new SentinelValueDetector(),

            // Kinematic
            new TeleportDetector(),
            new ImplausibleSpeedDetector(),
            new ImplausibleAccelerationDetector(),
            new ImplausibleAltitudeRateDetector(),
            new JitterDetector(),
            new VelocityMismatchDetector(),

            // Attitude
            new AttitudeRangeDetector(),
            new AttitudeWrapDetector(),
            new QuaternionNormalisationDetector(),
            new HeadingCourseMismatchDetector(),

            // Group
            new CohesionBreakDetector(),
            new GroupOutlierDetector(),
            new FormationCollapseDetector(),
        };

        return detectors.AsReadOnly();
    }

    /// <summary>Creates only the detectors that are implemented.</summary>
    /// <returns>The implemented detectors, in catalogue order.</returns>
    public static IReadOnlyList<IDetector> CreateImplemented()
    {
        var implemented = new List<IDetector>();
        foreach (IDetector detector in CreateAll())
        {
            if (detector.Status == DetectorStatus.Implemented)
            {
                implemented.Add(detector);
            }
        }

        return implemented.AsReadOnly();
    }
}
