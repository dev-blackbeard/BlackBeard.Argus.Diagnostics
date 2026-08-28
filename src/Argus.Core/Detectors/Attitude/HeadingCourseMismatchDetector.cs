using Argus.Contracts;

namespace Argus.Detectors.Attitude;

/// <summary>
/// Reports reported heading that disagrees with the course derived from consecutive positions.
/// </summary>
/// <remarks>
/// <para><b>Not implemented.</b> Declared here because the catalogue in
/// <c>docs/detector-catalogue.md</c> is the specification, and a condition that is specified
/// but not yet checked should be visible rather than absent. The registry skips it; set
/// <c>MonitorOptions.IncludeUnimplementedDetectors</c> to have it appear in reports as
/// <c>NotEvaluable</c>.</para>
/// <para><b>Intended method.</b> Compare EntitySample.HeadingDegrees against Geo.BearingDegrees over the last valid pair, using DetectorThresholds.MaxHeadingCourseDifferenceDegrees, and only when the derived speed exceeds DetectorThresholds.HeadingCourseMinimumSpeedMetersPerSecond - below that the derived course is noise and the check would flag a stationary entity for pointing the wrong way.</para>
/// </remarks>
public sealed class HeadingCourseMismatchDetector : NotImplementedDetector
{
    /// <summary>The stable identifier this detector will stamp on its findings.</summary>
    public const string DetectorId = "argus.attitude.heading-course-mismatch";

    // TODO(argus): HeadingCourseMismatch
    /// <summary>Creates the stub.</summary>
    public HeadingCourseMismatchDetector()
        : base(DetectorId, HealthFlags.HeadingCourseMismatch)
    {
    }
}
