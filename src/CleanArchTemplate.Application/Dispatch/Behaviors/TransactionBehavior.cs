using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Domain.Common;

namespace CleanArchTemplate.Application.Dispatch.Behaviors;

/// <summary>
/// Wraps requests marked <see cref="ITransactionalRequest"/> in a database transaction and rolls
/// back when the handler returns a failure - an expected failure must not leave half a write
/// behind just because it travelled as a Result rather than an exception.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not ITransactionalRequest)
        {
            return await next();
        }

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                async _ =>
                {
                    var response = await next();
                    return response.IsFailure ? throw new TransactionRollbackException(response) : response;
                },
                cancellationToken);
        }
        catch (TransactionRollbackException ex)
        {
            // The transaction has already been rolled back; hand the original failure back to the caller.
            return (TResponse)ex.Result;
        }
    }
}

/// <summary>
/// Carries a failed <see cref="Result"/> out through the transaction scope so the transaction
/// aborts. Never escapes <see cref="TransactionBehavior{TRequest,TResponse}"/>.
/// </summary>
internal sealed class TransactionRollbackException(Result result)
    : Exception("The handler returned a failure; the transaction was rolled back.")
{
    public Result Result { get; } = result;
}
