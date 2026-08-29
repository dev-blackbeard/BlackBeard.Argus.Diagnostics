using System.Collections.Generic;
using Argus.Contracts;

namespace Argus.Testing.Injectors;

/// <summary>
/// Reads the position fields from the wrong byte offset — the framing misalignment signature.
/// </summary>
/// <remarks>
/// <para>
/// The most valuable injector in the harness, because it produces the most dangerous fault in
/// the catalogue. A frame read one field too far along yields values that are individually
/// plausible: latitude holds what was longitude, longitude holds what was altitude, and the
/// resulting position is somewhere real. A map draws it without complaint, and so does every
/// range check applied to one field at a time.
/// </para>
/// <para>
/// See <c>docs/corruption-taxonomy.md</c> for what this looks like on the wire and why
/// cross-field magnitude plausibility is the test that catches it.
/// </para>
/// </remarks>
public sealed class ByteShiftInjector : ISampleInjector
{
    /// <summary>Creates the injector.</summary>
    /// <param name="byteShift">How many bytes the reader is misaligned by. Four and eight are the interesting cases.</param>
    /// <param name="everyNthTick">Damage one tick in this many. One damages every tick.</param>
    public ByteShiftInjector(int byteShift = 8, int everyNthTick = 1)
    {
        ByteShift = byteShift;
        EveryNthTick = everyNthTick < 1 ? 1 : everyNthTick;
    }

    /// <summary>How many bytes the reader is misaligned by.</summary>
    public int ByteShift { get; }

    /// <summary>Damage one tick in this many.</summary>
    public int EveryNthTick { get; }

    /// <inheritdoc />
    public string Name
    {
        get { return "byte-shift"; }
    }

    /// <inheritdoc />
    public string Description
    {
        get { return "Rotates the bytes of the position triple, as a reader misaligned within the frame does."; }
    }

    /// <inheritdoc />
    public IReadOnlyList<HealthFlags> ExpectedFlags { get; } = new[]
    {
        HealthFlags.FieldShift,
    };

    /// <inheritdoc />
    public EntitySample? Inject(EntitySample sample, InjectionContext context)
    {
        if (context.TickIndex % EveryNthTick != 0)
        {
            return sample;
        }

        if (!sample.Latitude.HasValue || !sample.Longitude.HasValue || !sample.Altitude.HasValue)
        {
            return sample;
        }

        double[] shifted = ByteLevel.ShiftBytes(
            new[] { sample.Latitude.Value, sample.Longitude.Value, sample.Altitude.Value },
            ByteShift);

        EntitySample damaged = sample.Clone();
        damaged.Latitude = shifted[0];
        damaged.Longitude = shifted[1];
        damaged.Altitude = shifted[2];

        var rawFields = new List<RawField>
        {
            new RawField("latitude", 0, 8, shifted[0]),
            new RawField("longitude", 8, 8, shifted[1]),
            new RawField("altitude", 16, 8, shifted[2]),
        };

        damaged.RawFields = rawFields.AsReadOnly();
        return damaged;
    }
}
