namespace CleanArchTemplate.Application.Abstractions.Dispatch;

public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>
/// Wraps every request. Behaviours run in registration order on the way in and unwind in
/// reverse on the way out, so the outermost concern (logging) is registered first.
/// </summary>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
