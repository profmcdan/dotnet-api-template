namespace CleanArchTemplate.Application.Abstractions.Dispatch;

/// <summary>
/// Opt-in marker: requests carrying it are wrapped in an explicit database transaction by
/// <c>TransactionBehavior</c>. Commands that touch more than one aggregate or call
/// <c>SaveChanges</c> more than once need it; a single <c>SaveChanges</c> is already atomic.
/// </summary>
public interface ITransactionalRequest;
