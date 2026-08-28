using System.Globalization;
using Argus.Contracts;
using Argus.Geodesy;
using Argus.State;

namespace Argus.Detectors.Group;

/// <summary>
/// Reports an entity lying further from its group's centroid than the configured radius.
/// </summary>
/// <remarks>
/// <para>
/// The centroid this compares against is not the one the prototype computed, in three ways,
/// each of which was a defect on its own.
/// </para>
/// <para>
/// It excludes the entity under test. Including it drags the centroid toward the entity by
/// a factor of one over <i>n</i>, which systematically understates exactly the quantity
/// being measured — and the understatement is worst for small groups, which is where an
/// outlier matters most.
/// </para>
/// <para>
/// It excludes invalid entities. The prototype included them, so a single entity reporting
/// an out-of-range position dragged the centroid off the group and made every other entity
/// in it look like an outlier: one fault, <i>n</i> findings, none of them about the entity
/// that was actually broken.
/// </para>
/// <para>
/// It is a vector mean rather than an arithmetic mean of degrees, so it does not break
/// across the antimeridian or near a pole. And it requires
/// <c>DetectorThresholds.MinimumGroupContributors</c> before it will answer at all: with one
/// other entity a "centroid" is just that entity's position.
/// </para>
/// </remarks>
public sealed class GroupOutlierDetector : IDetector
{
    /// <summary>The stable identifier this detector stamps on its findings.</summary>
    public const string DetectorId = "argus.group.outlier";

    /// <inheritdoc />
    public string Id
    {
        get { return DetectorId; }
    }

    /// <inheritdoc />
    public HealthFlags Flag
    {
        get { return HealthFlags.GroupOutlier; }
    }

    /// <inheritdoc />
    public DetectorStatus Status
    {
        get { return DetectorStatus.Implemented; }
    }

    /// <inheritdoc />
    public DetectorResult Evaluate(DetectorContext context)
    {
        double? radius = context.Thresholds.GroupOutlierRadiusMeters;
        if (!radius.HasValue)
        {
            return DetectorResult.NotEvaluable(
                Flag,
                DetectorId,
                "DetectorThresholds.GroupOutlierRadiusMeters is not configured, so there is no radius to compare against");
        }

        GroupTickContext? group = context.Group;
        if (group == null)
        {
            return DetectorResult.NotEvaluable(
                Flag,
                DetectorId,
                "no group tick context was supplied; call IEntityStreamMonitor.CreateTickContext once per tick and pass it to Observe");
        }

        if (!context.PositionIsUsable)
        {
            return DetectorResult.NotEvaluable(
                Flag,
                DetectorId,
                "this sample's position is not usable, so its distance from the group cannot be measured");
        }

        if (!group.IdentitiesResolved)
        {
            return DetectorResult.NotEvaluable(
                Flag,
                DetectorId,
                "at least one group member's identity could not be resolved, so the entity under test cannot be excluded from its own centroid");
        }

        int minimum = context.Thresholds.MinimumGroupContributors;
        double centroidLatitude;
        double centroidLongitude;
        int contributors;

        if (!group.TryGetCentroidExcluding(context.Sample.EntityId, minimum, out centroidLatitude, out centroidLongitude, out contributors))
        {
            return DetectorResult.NotEvaluable(
                Flag,
                DetectorId,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} other valid contributors after excluding this entity, and {1} are required (DetectorThresholds.MinimumGroupContributors); or the remaining positions summed to a degenerate centroid",
                    contributors,
                    minimum));
        }

        double distance = Geo.DistanceMeters(
            centroidLatitude,
            centroidLongitude,
            context.Sample.Latitude.Value,
            context.Sample.Longitude.Value);

        if (!Geo.IsFinite(distance))
        {
            return DetectorResult.NotEvaluable(
                Flag,
                DetectorId,
                "the distance to the group centroid could not be computed");
        }

        string measured = string.Format(
            CultureInfo.InvariantCulture,
            "{0} from the centroid of {1} other valid entities",
            HealthFinding.Quantity(distance, "m"),
            contributors);
        string expected = HealthFinding.AtMost(radius.Value, "m");

        if (distance > radius.Value)
        {
            return DetectorResult.Flagged(Flag, DetectorId, measured, expected, distance, "m");
        }

        return DetectorResult.Healthy(Flag, DetectorId, measured, expected, distance, "m");
    }
}
