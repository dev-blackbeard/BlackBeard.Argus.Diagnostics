using System;
using Argus.Contracts;

namespace Argus.Detectors;

/// <summary>
/// Base class for a detector that is specified in the catalogue but not yet written.
/// </summary>
/// <remarks>
/// The registry never calls <see cref="Evaluate"/> on one of these, so the exception is not
/// a trap for a running system — it is there so that anything reaching past the registry to
/// call a stub directly fails loudly instead of receiving a fabricated verdict. When the
/// detector is implemented, this base class goes away with it, and
/// <c>Argus.Golden.Tests</c> then fails until the corresponding golden case is moved out of
/// the pending list.
/// </remarks>
public abstract class NotImplementedDetector : IDetector
{
    /// <summary>Creates a stub.</summary>
    /// <param name="id">The stable identifier the implemented detector will carry.</param>
    /// <param name="flag">The condition the implemented detector will check for.</param>
    protected NotImplementedDetector(string id, HealthFlags flag)
    {
        Id = id;
        Flag = flag;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public HealthFlags Flag { get; }

    /// <inheritdoc />
    public DetectorStatus Status
    {
        get { return DetectorStatus.NotImplemented; }
    }

    /// <summary>Always throws.</summary>
    /// <param name="context">Ignored.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotImplementedException">Always.</exception>
    public DetectorResult Evaluate(DetectorContext context)
    {
        throw new NotImplementedException(
            Id + " (" + Flag + ") is declared in the detector catalogue but not implemented. "
            + "See docs/detector-catalogue.md. The registry skips unimplemented detectors; "
            + "set MonitorOptions.IncludeUnimplementedDetectors to surface them as NotEvaluable findings instead.");
    }
}
