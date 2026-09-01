using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Domain.Common;
using FluentValidation;

namespace CleanArchTemplate.Application.Dispatch.Behaviors;

/// <summary>
/// Runs every validator registered for the request and converts failures into a
/// <see cref="ValidationError"/> result rather than an exception, so the shape of a 400 is
/// decided by the same Result mapping as every other outcome.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var applicable = validators.ToArray();
        if (applicable.Length == 0)
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(applicable.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .GroupBy(failure => ToCamelCase(failure.PropertyName), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        if (failures.Count == 0)
        {
            return await next();
        }

        return CreateValidationResult(new ValidationError(failures));
    }

    private static string ToCamelCase(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return propertyName;
        }

        var segments = propertyName.Split('.');
        for (var i = 0; i < segments.Length; i++)
        {
            if (segments[i].Length > 0 && char.IsUpper(segments[i][0]))
            {
                segments[i] = char.ToLowerInvariant(segments[i][0]) + segments[i][1..];
            }
        }

        return string.Join('.', segments);
    }

    /// <summary>
    /// <typeparamref name="TResponse"/> is either <see cref="Result"/> or <c>Result&lt;T&gt;</c>;
    /// build whichever one this pipeline was closed over.
    /// </summary>
    private static TResponse CreateValidationResult(ValidationError error)
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)Result.Failure(error);
        }

        var valueType = typeof(TResponse).GetGenericArguments()[0];
        var failureMethod = typeof(Result)
            .GetMethods()
            .First(m => m.Name == nameof(Result.Failure) && m.IsGenericMethod)
            .MakeGenericMethod(valueType);

        return (TResponse)failureMethod.Invoke(null, [error])!;
    }
}
