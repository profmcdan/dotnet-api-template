namespace CleanArchTemplate.Application.Abstractions.Dispatch;

/// <summary>
/// The single entry point from the presentation layer into the application layer.
/// Controllers depend on this and nothing else - never on a DbContext or a repository.
/// </summary>
public interface ISender
{
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
