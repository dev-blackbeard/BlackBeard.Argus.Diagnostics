using System.Collections.Generic;

namespace Argus.Testing.Injectors;

/// <summary>
/// One instance of every injector in the harness, with its neutral defaults.
/// </summary>
/// <remarks>
/// Used by <c>Argus.Golden.Tests</c> to assert that every declared fault has a detector that
/// catches it — and, while the catalogue is still being implemented, that every fault whose
/// detector is a stub is explicitly listed as pending rather than quietly unchecked.
/// </remarks>
public static class InjectorCatalogue
{
    /// <summary>Creates one of each per-sample injector.</summary>
    /// <returns>The injectors.</returns>
    public static IReadOnlyList<ISampleInjector> CreateSampleInjectors()
    {
        var injectors = new List<ISampleInjector>
        {
            new EndianSwapInjector(),
            new ByteShiftInjector(),
            new ScaleErrorInjector(),
            new RadiansAsDegreesInjector(),
            new AxisSwapInjector(),
            new SentinelInjector(),
            new QuantisationCollapseInjector(),
            new FreezeInjector(),
            new JitterInjector(),
            new PacketLossInjector(),
        };

        return injectors.AsReadOnly();
    }

    /// <summary>Creates one of each whole-stream injector.</summary>
    /// <returns>The injectors.</returns>
    public static IReadOnlyList<IStreamInjector> CreateStreamInjectors()
    {
        var injectors = new List<IStreamInjector>
        {
            new ReorderInjector(),
        };

        return injectors.AsReadOnly();
    }
}
