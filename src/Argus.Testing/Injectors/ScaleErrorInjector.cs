using System.Collections.Generic;
using Argus.Contracts;

namespace Argus.Testing.Injectors;

/// <summary>Reads a fixed-point angular field at the wrong scale.</summary>
/// <remarks>
/// Both directions occur and they have opposite fixes, so the injector can produce either: a
/// raw scaled integer read as degrees (multiply), or a degree value read as though it were
/// already scaled (divide).
/// </remarks>
public sealed class ScaleErrorInjector : ISampleInjector
{
    /// <summary>Creates the injector.</summary>
    /// <param name="scaleFactor">The fixed-point scale factor the protocol uses.</param>
    /// <param name="multiply">
    /// <c>true</c> to produce a raw scaled value read as degrees; <c>false</c> to produce a
    /// degree value read as though already scaled.
    /// </param>
    public ScaleErrorInjector(double scaleFactor = 1e7, bool multiply = true)
    {
        ScaleFactor = scaleFactor;
        Multiply = multiply;
    }

    /// <summary>The fixed-point scale factor the protocol uses.</summary>
    public double ScaleFactor { get; }

    /// <summary>Whether the value is scaled up rather than down.</summary>
    public bool Multiply { get; }

    /// <inheritdoc />
    public string Name
    {
        get { return Multiply ? "scale-error-raw-as-degrees" : "scale-error-degrees-as-raw"; }
    }

    /// <inheritdoc />
    public string Description
    {
        get
        {
            return Multiply
                ? "Presents the raw fixed-point integer as though it were already in degrees."
                : "Presents a degree value as though it still needed dividing by the fixed-point scale.";
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<HealthFlags> ExpectedFlags { get; } = new[]
    {
        HealthFlags.FixedPointScaleError,
    };

    /// <inheritdoc />
    public EntitySample? Inject(EntitySample sample, InjectionContext context)
    {
        EntitySample damaged = sample.Clone();

        if (damaged.Latitude.HasValue)
        {
            damaged.Latitude = Rescale(damaged.Latitude.Value);
        }

        if (damaged.Longitude.HasValue)
        {
            damaged.Longitude = Rescale(damaged.Longitude.Value);
        }

        return damaged;
    }

    private double Rescale(double value)
    {
        return Multiply ? value * ScaleFactor : value / ScaleFactor;
    }
}
