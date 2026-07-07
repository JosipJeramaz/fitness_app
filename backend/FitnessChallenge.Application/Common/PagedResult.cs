namespace FitnessChallenge.Application.Common;

/// <summary>
/// Envelope for paged list responses: the current page plus the total row count (pre-paging) so
/// clients can render "X–Y of Z" controls. The assignment doesn't prescribe a response shape for
/// serving, so the envelope was chosen to carry pagination metadata (documented in the README).
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);
