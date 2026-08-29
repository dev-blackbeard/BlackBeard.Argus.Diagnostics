using System;

namespace Argus.Contracts;

/// <summary>
/// Thrown at first use when Argus cannot work out how to read latitude and longitude from
/// an application entity type.
/// </summary>
/// <remarks>
/// This is deliberately loud and deliberately early. The alternative — returning zero for
/// an unresolvable position — is the single worst thing a stream diagnostic tool can do:
/// it manufactures a plausible position at the origin, which then propagates into group
/// centroids and jump detection and produces confident findings about nothing. Failing at
/// first use costs one exception; failing silently costs a fortnight of argument.
/// </remarks>
public sealed class EntityAccessorException : InvalidOperationException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">The actionable message.</param>
    public EntityAccessorException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">The actionable message.</param>
    /// <param name="innerException">The underlying failure.</param>
    public EntityAccessorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
