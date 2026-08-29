namespace Argus.Contracts;

/// <summary>
/// What a detector concluded about one sample.
/// </summary>
public enum DetectorOutcome
{
    /// <summary>The detector ran and found nothing wrong.</summary>
    Healthy = 0,

    /// <summary>The detector ran and found the condition it looks for.</summary>
    Flagged = 1,

    /// <summary>
    /// The detector could not run, because a field it needs was not supplied or a
    /// threshold it needs is unconfigured.
    /// </summary>
    /// <remarks>
    /// Architecture rule 6: this is never reported as <see cref="Healthy"/>. "We did not
    /// check" and "we checked and it was fine" are different claims, and a stream report
    /// that conflates them is worse than no report, because it invites the reader to
    /// conclude something was verified when it was not.
    /// </remarks>
    NotEvaluable = 2,
}
