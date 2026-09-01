using System.Collections.Concurrent;
using CleanArchTemplate.Application.Abstractions.Dispatch;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchTemplate.Application.Dispatch;

/// <summary>
/// Resolves the handler for a request and wraps it in the registered pipeline behaviours.
/// A closed generic executor is built once per request type and cached, so the reflection
/// cost is paid on first use only.
/// </summary>
internal sealed class Sender(IServiceProvider services) : ISender
{
    private static readonly ConcurrentDictionary<Type, object> Executors = new();

    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var executor = (Executor<TResponse>)Executors.GetOrAdd(
            request.GetType(),
            static (requestType, responseType) => Activator.CreateInstance(
                typeof(Executor<,>).MakeGenericType(requestType, responseType))!,
            typeof(TResponse));

        return executor.ExecuteAsync(request, services, cancellationToken);
    }

    private abstract class Executor<TResponse>
    {
        public abstract Task<TResponse> ExecuteAsync(object request, IServiceProvider services, CancellationToken cancellationToken);
    }

    private sealed class Executor<TRequest, TResponse> : Executor<TResponse>
        where TRequest : IRequest<TResponse>
    {
        public override Task<TResponse> ExecuteAsync(object request, IServiceProvider services, CancellationToken cancellationToken)
        {
            var typedRequest = (TRequest)request;
            var handler = services.GetService<IRequestHandler<TRequest, TResponse>>()
                ?? throw new InvalidOperationException(
                    $"No handler registered for '{typeof(TRequest).FullName}'. " +
                    "Handlers are discovered by assembly scanning - check the type is public and non-abstract.");

            RequestHandlerDelegate<TResponse> pipeline = () => handler.HandleAsync(typedRequest, cancellationToken);

            // Reverse so the first-registered behaviour ends up outermost.
            var behaviors = services.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse();
            foreach (var behavior in behaviors)
            {
                var next = pipeline;
                pipeline = () => behavior.HandleAsync(typedRequest, next, cancellationToken);
            }

            return pipeline();
        }
    }
}
