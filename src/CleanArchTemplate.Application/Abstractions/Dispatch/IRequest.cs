using CleanArchTemplate.Domain.Common;

namespace CleanArchTemplate.Application.Abstractions.Dispatch;

/// <summary>Marker for anything that can be sent through <see cref="ISender"/>.</summary>
public interface IRequest<out TResponse>;

/// <summary>A state change that returns nothing but success or failure.</summary>
public interface ICommand : IRequest<Result>;

/// <summary>A state change that returns a value.</summary>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>;

/// <summary>A side-effect-free read.</summary>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>;
