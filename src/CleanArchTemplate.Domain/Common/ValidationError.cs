using System.Collections.ObjectModel;

namespace CleanArchTemplate.Domain.Common;

/// <summary>
/// Aggregates per-field failures produced by request validation.
/// </summary>
public sealed record ValidationError : Error
{
    public ValidationError(IReadOnlyDictionary<string, string[]> failures)
        : base("validation.failed", "One or more validation errors occurred.", ErrorType.Validation) =>
        Failures = new ReadOnlyDictionary<string, string[]>(
            failures.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));

    public IReadOnlyDictionary<string, string[]> Failures { get; }
}
