using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Application.Dispatch.Behaviors;
using CleanArchTemplate.Domain.Common;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CleanArchTemplate.Application.UnitTests.Dispatch;

public sealed class SenderTests
{
    public sealed record EchoQuery(string Value) : IQuery<string>;

    internal sealed class EchoQueryHandler : IQueryHandler<EchoQuery, string>
    {
        public Task<Result<string>> HandleAsync(EchoQuery request, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(request.Value));
    }

    internal sealed class EchoQueryValidator : AbstractValidator<EchoQuery>
    {
        public EchoQueryValidator() => RuleFor(x => x.Value).NotEmpty().MinimumLength(3);
    }

    /// <summary>Records the order behaviours wrap the handler in.</summary>
    internal sealed class TracingBehavior<TRequest, TResponse>(List<string> trace)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : Result
    {
        public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            trace.Add("before");
            var response = await next();
            trace.Add("after");
            return response;
        }
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ISender, Application.Dispatch.Sender>();
        services.AddScoped<IRequestHandler<EchoQuery, Result<string>>, EchoQueryHandler>();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Routes_a_request_to_its_handler()
    {
        var sender = BuildProvider().GetRequiredService<ISender>();

        var result = await sender.SendAsync(new EchoQuery("hello"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("hello");
    }

    [Fact]
    public async Task Throws_a_helpful_error_when_no_handler_is_registered()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ISender, Application.Dispatch.Sender>();
        var sender = services.BuildServiceProvider().GetRequiredService<ISender>();

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => sender.SendAsync(new EchoQuery("hello"), TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("No handler registered");
    }

    [Fact]
    public async Task Runs_registered_behaviours_around_the_handler()
    {
        var trace = new List<string>();
        var provider = BuildProvider(services =>
        {
            services.AddSingleton(trace);
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TracingBehavior<,>));
        });

        await provider.GetRequiredService<ISender>().SendAsync(new EchoQuery("hello"), TestContext.Current.CancellationToken);

        trace.ShouldBe(["before", "after"]);
    }

    [Fact]
    public async Task Validation_failures_come_back_as_a_failed_result_not_an_exception()
    {
        var provider = BuildProvider(services =>
        {
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            services.AddScoped<IValidator<EchoQuery>, EchoQueryValidator>();
        });

        var result = await provider.GetRequiredService<ISender>()
            .SendAsync(new EchoQuery("no"), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        var validation = result.Error.ShouldBeOfType<ValidationError>();
        validation.Failures.ShouldContainKey("value");
    }

    [Fact]
    public async Task Valid_requests_pass_straight_through_the_validation_behaviour()
    {
        var provider = BuildProvider(services =>
        {
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            services.AddScoped<IValidator<EchoQuery>, EchoQueryValidator>();
        });

        var result = await provider.GetRequiredService<ISender>()
            .SendAsync(new EchoQuery("valid"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }
}
