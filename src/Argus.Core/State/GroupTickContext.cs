using System;
using System.Collections.Generic;
using Argus.Contracts;
using Argus.Geodesy;

namespace Argus.State;

/// <summary>
/// One entity's contribution to a tick's group statistics.
/// </summary>
public sealed class GroupContribution
{
    /// <summary>Creates a contribution.</summary>
    /// <param name="entityId">The contributing entity's identity.</param>
    /// <param name="vector">The entity's position as a unit vector.</param>
    /// <param name="latitudeDegrees">Latitude in degrees.</param>
    /// <param name="longitudeDegrees">Longitude in degrees.</param>
    /// <param name="altitude">Altitude in metres, or <c>null</c> if not supplied.</param>
    public GroupContribution(string entityId, GeoVector vector, double latitudeDegrees, double longitudeDegrees, double? altitude)
    {
        EntityId = entityId;
        Vector = vector;
        LatitudeDegrees = latitudeDegrees;
        LongitudeDegrees = longitudeDegrees;
        Altitude = altitude;
    }

    /// <summary>The contributing entity's identity.</summary>
    public string EntityId { get; }

    /// <summary>The entity's position as a unit vector.</summary>
    public GeoVector Vector { get; }

    /// <summary>Latitude in degrees.</summary>
    public double LatitudeDegrees { get; }

    /// <summary>Longitude in degrees.</summary>
    public double LongitudeDegrees { get; }

    /// <summary>Altitude in metres, or <c>null</c> if not supplied.</summary>
    public double? Altitude { get; }
}

/// <summary>
/// The group statistics for one tick, computed once and shared by every entity evaluated
/// against that tick.
/// </summary>
/// <remarks>
/// <para>
/// This type exists to fix two prototype defects at once.
/// </para>
/// <para>
/// The first was cost. The prototype called <c>.Count()</c> and <c>.Average()</c> on a
/// possibly-lazy sequence once per entity per tick, so a group of <i>n</i> entities
/// re-enumerated the collection <i>n</i> times and did O(n²) work per tick — and if the
/// sequence was a LINQ query rather than a materialised list, it re-ran the query too.
/// Here the sequence is materialised exactly once, into vector sums.
/// </para>
/// <para>
/// The second was correctness. The prototype's centroid included the entity under test and
/// every invalid entity, so an entity was dragged toward itself — which systematically
/// understates its own distance from the group — and one entity reporting an out-of-range
/// position poisoned cohesion for every other entity in the group. Here invalid entities
/// never enter the sum, and excluding the entity under test is a vector subtraction that
/// costs O(1) rather than a rescan.
/// </para>
/// </remarks>
public sealed class GroupTickContext
{
    private readonly Dictionary<string, GroupContribution> _byEntity;

    internal GroupTickContext(
        DateTime tickTimeUtc,
        int sampleCount,
        IReadOnlyList<GroupContribution> contributions,
        Dictionary<string, GroupContribution> byEntity,
        GeoVector positionSum,
        double altitudeSum,
        int altitudeContributorCount,
        bool identitiesResolved)
    {
        TickTimeUtc = tickTimeUtc;
        SampleCount = sampleCount;
        Contributions = contributions;
        _byEntity = byEntity;
        PositionSum = positionSum;
        AltitudeSum = altitudeSum;
        AltitudeContributorCount = altitudeContributorCount;
        IdentitiesResolved = identitiesResolved;

        double centroidLatitude;
        double centroidLongitude;
        HasCentroid = Centroid.TryToPosition(positionSum, out centroidLatitude, out centroidLongitude);
        CentroidLatitudeDegrees = centroidLatitude;
        CentroidLongitudeDegrees = centroidLongitude;

        if (HasCentroid && contributions.Count > 0)
        {
            double sumOfSquares = 0.0;
            double maximum = 0.0;
            for (int i = 0; i < contributions.Count; i++)
            {
                double distance = Geo.DistanceMeters(
                    centroidLatitude,
                    centroidLongitude,
                    contributions[i].LatitudeDegrees,
                    contributions[i].LongitudeDegrees);

                if (!Geo.IsFinite(distance))
                {
                    continue;
                }

                sumOfSquares += distance * distance;
                if (distance > maximum)
                {
                    maximum = distance;
                }
            }

            SpreadMeters = Math.Sqrt(sumOfSquares / contributions.Count);
            MaxDistanceFromCentroidMeters = maximum;
        }
    }

    /// <summary>A context with no contributors, for callers that have no group.</summary>
    public static GroupTickContext Empty { get; } = new GroupTickContextBuilder(default(DateTime)).Build();

    /// <summary>The time the tick was assembled at.</summary>
    public DateTime TickTimeUtc { get; }

    /// <summary>How many entities were offered to the builder, valid or not.</summary>
    public int SampleCount { get; }

    /// <summary>The entities that were valid enough to contribute, in the order they were offered.</summary>
    public IReadOnlyList<GroupContribution> Contributions { get; }

    /// <summary>How many entities contributed.</summary>
    public int ContributorCount
    {
        get { return Contributions.Count; }
    }

    /// <summary>The sum of every contributor's unit vector.</summary>
    public GeoVector PositionSum { get; }

    /// <summary>The sum of every contributor's altitude, over the contributors that supplied one.</summary>
    public double AltitudeSum { get; }

    /// <summary>How many contributors supplied an altitude.</summary>
    public int AltitudeContributorCount { get; }

    /// <summary>
    /// Whether every contributor's identity could be resolved. When it could not, self
    /// exclusion is impossible and group detectors report <c>NotEvaluable</c> rather than
    /// silently comparing an entity against a centroid it is itself inside.
    /// </summary>
    public bool IdentitiesResolved { get; }

    /// <summary>Whether a centroid over all contributors exists.</summary>
    public bool HasCentroid { get; }

    /// <summary>Latitude of the centroid over all contributors, in degrees.</summary>
    public double CentroidLatitudeDegrees { get; }

    /// <summary>Longitude of the centroid over all contributors, in degrees.</summary>
    public double CentroidLongitudeDegrees { get; }

    /// <summary>Root-mean-square distance of the contributors from the all-contributor centroid, in metres.</summary>
    public double SpreadMeters { get; }

    /// <summary>The largest distance from the all-contributor centroid to any contributor, in metres.</summary>
    public double MaxDistanceFromCentroidMeters { get; }

    /// <summary>
    /// Computes the centroid of every contributor except the named one.
    /// </summary>
    /// <param name="entityId">The entity to exclude, or <c>null</c> to exclude nothing.</param>
    /// <param name="minimumContributors">
    /// The smallest number of remaining contributors that makes a centroid worth computing.
    /// Below this the answer is noise, and the caller should report <c>NotEvaluable</c>.
    /// </param>
    /// <param name="latitudeDegrees">The resulting latitude in degrees.</param>
    /// <param name="longitudeDegrees">The resulting longitude in degrees.</param>
    /// <param name="contributorCount">How many contributors were left after the exclusion.</param>
    /// <returns><c>false</c> if there were too few contributors, or the remaining sum was degenerate.</returns>
    public bool TryGetCentroidExcluding(
        string? entityId,
        int minimumContributors,
        out double latitudeDegrees,
        out double longitudeDegrees,
        out int contributorCount)
    {
        latitudeDegrees = 0.0;
        longitudeDegrees = 0.0;

        GeoVector sum = PositionSum;
        contributorCount = ContributorCount;

        GroupContribution? self = null;
        if (entityId != null && _byEntity.TryGetValue(entityId, out self) && self != null)
        {
            sum -= self.Vector;
            contributorCount--;
        }

        if (contributorCount < minimumContributors)
        {
            return false;
        }

        return Centroid.TryToPosition(sum, out latitudeDegrees, out longitudeDegrees);
    }

    /// <summary>
    /// Computes the mean altitude of every contributor except the named one.
    /// </summary>
    /// <param name="entityId">The entity to exclude, or <c>null</c> to exclude nothing.</param>
    /// <param name="minimumContributors">The smallest number of remaining contributors that makes a mean worth computing.</param>
    /// <param name="meanAltitude">The resulting mean altitude in metres.</param>
    /// <returns><c>false</c> if there were too few contributors that supplied an altitude.</returns>
    public bool TryGetMeanAltitudeExcluding(string? entityId, int minimumContributors, out double meanAltitude)
    {
        meanAltitude = 0.0;

        double sum = AltitudeSum;
        int count = AltitudeContributorCount;

        GroupContribution? self;
        if (entityId != null && _byEntity.TryGetValue(entityId, out self) && self != null && self.Altitude.HasValue)
        {
            sum -= self.Altitude.Value;
            count--;
        }

        if (count < minimumContributors || count <= 0)
        {
            return false;
        }

        meanAltitude = sum / count;
        return true;
    }

    /// <summary>Whether the named entity contributed to this tick.</summary>
    /// <param name="entityId">The entity to look for.</param>
    /// <returns><c>true</c> if the entity was a valid contributor.</returns>
    public bool Contains(string entityId)
    {
        return _byEntity.ContainsKey(entityId);
    }

    /// <summary>
    /// Builds a context from a sequence of samples.
    /// </summary>
    /// <param name="samples">The samples that make up the tick.</param>
    /// <param name="tickTimeUtc">The time to stamp the tick with.</param>
    /// <param name="treatZeroIslandAsInvalid">Whether an exact <c>(0, 0)</c> position should be excluded.</param>
    /// <returns>The context.</returns>
    /// <remarks>The sequence is enumerated exactly once.</remarks>
    public static GroupTickContext FromSamples(
        IEnumerable<EntitySample> samples,
        DateTime tickTimeUtc,
        bool treatZeroIslandAsInvalid = true)
    {
        var builder = new GroupTickContextBuilder(tickTimeUtc, treatZeroIslandAsInvalid);
        if (samples != null)
        {
            foreach (EntitySample sample in samples)
            {
                builder.AddSample(sample);
            }
        }

        return builder.Build();
    }
}

/// <summary>
/// Accumulates a <see cref="GroupTickContext"/> from entities offered one at a time.
/// </summary>
/// <remarks>
/// The builder is what makes the one-enumeration guarantee possible for callers whose
/// entities are an arbitrary application type rather than <see cref="EntitySample"/>: the
/// compatibility facade resolves each entity through an accessor and offers it here.
/// </remarks>
public sealed class GroupTickContextBuilder
{
    private readonly List<GroupContribution> _contributions = new List<GroupContribution>();
    private readonly Dictionary<string, GroupContribution> _byEntity = new Dictionary<string, GroupContribution>(StringComparer.Ordinal);
    private readonly DateTime _tickTimeUtc;
    private readonly bool _treatZeroIslandAsInvalid;

    private GeoVector _positionSum = GeoVector.Zero;
    private double _altitudeSum;
    private int _altitudeContributorCount;
    private int _sampleCount;
    private bool _identitiesResolved = true;

    /// <summary>Creates a builder.</summary>
    /// <param name="tickTimeUtc">The time to stamp the tick with.</param>
    /// <param name="treatZeroIslandAsInvalid">Whether an exact <c>(0, 0)</c> position should be excluded.</param>
    public GroupTickContextBuilder(DateTime tickTimeUtc, bool treatZeroIslandAsInvalid = true)
    {
        _tickTimeUtc = tickTimeUtc;
        _treatZeroIslandAsInvalid = treatZeroIslandAsInvalid;
    }

    /// <summary>How many entities have been offered so far.</summary>
    public int SampleCount
    {
        get { return _sampleCount; }
    }

    /// <summary>Offers an entity to the tick.</summary>
    /// <param name="entityId">The entity's identity, or <c>null</c> if it could not be resolved.</param>
    /// <param name="latitudeDegrees">Latitude in degrees, or <c>null</c>.</param>
    /// <param name="longitudeDegrees">Longitude in degrees, or <c>null</c>.</param>
    /// <param name="altitude">Altitude in metres, or <c>null</c>.</param>
    /// <returns><c>true</c> if the entity contributed; <c>false</c> if it was rejected as invalid.</returns>
    public bool Add(string? entityId, double? latitudeDegrees, double? longitudeDegrees, double? altitude)
    {
        _sampleCount++;

        if (entityId == null)
        {
            // Without identity the entity cannot be excluded from its own centroid, which is
            // the whole point of the exclusion. Record the gap rather than quietly proceeding.
            _identitiesResolved = false;
            return false;
        }

        if (!PositionValidity.IsUsable(latitudeDegrees, longitudeDegrees, _treatZeroIslandAsInvalid))
        {
            return false;
        }

        // PositionValidity.IsUsable already proved both are non-null; the compiler cannot see
        // that guarantee across the method call, so the null-forgiving operator is correct
        // here rather than a defect to tidy away.
        double latitude = latitudeDegrees!.Value;
        double longitude = longitudeDegrees!.Value;

        var contribution = new GroupContribution(
            entityId,
            Centroid.ToVector(latitude, longitude),
            latitude,
            longitude,
            altitude.HasValue && Geo.IsFinite(altitude.Value) ? altitude : (double?)null);

        _contributions.Add(contribution);

        // Last one wins if an entity appears twice in a tick, but only one contribution is
        // ever subtracted, so a duplicated entity would bias its own exclusion. Keep the
        // first and ignore the repeat instead.
        if (!_byEntity.ContainsKey(entityId))
        {
            _byEntity.Add(entityId, contribution);
        }

        _positionSum += contribution.Vector;

        if (contribution.Altitude.HasValue)
        {
            _altitudeSum += contribution.Altitude.Value;
            _altitudeContributorCount++;
        }

        return true;
    }

    /// <summary>Offers a sample to the tick.</summary>
    /// <param name="sample">The sample.</param>
    /// <returns><c>true</c> if the sample contributed.</returns>
    public bool AddSample(EntitySample sample)
    {
        if (sample == null)
        {
            return false;
        }

        return Add(sample.EntityId, sample.Latitude, sample.Longitude, sample.Altitude);
    }

    /// <summary>Offers a snapshot to the tick.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <returns><c>true</c> if the snapshot contributed.</returns>
    public bool AddSnapshot(EntitySnapshot snapshot)
    {
        return Add(snapshot.EntityId, snapshot.Latitude, snapshot.Longitude, snapshot.Altitude);
    }

    /// <summary>Produces the context.</summary>
    /// <returns>The accumulated context.</returns>
    public GroupTickContext Build()
    {
        return new GroupTickContext(
            _tickTimeUtc,
            _sampleCount,
            _contributions.AsReadOnly(),
            _byEntity,
            _positionSum,
            _altitudeSum,
            _altitudeContributorCount,
            _identitiesResolved);
    }
}
