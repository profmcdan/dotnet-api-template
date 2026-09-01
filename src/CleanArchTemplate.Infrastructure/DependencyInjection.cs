using CleanArchTemplate.Application.Abstractions.Caching;
using CleanArchTemplate.Application.Abstractions.Links;
using CleanArchTemplate.Application.Abstractions.Messaging;
using CleanArchTemplate.Application.Abstractions.Notifications;
using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Abstractions.Security;
using CleanArchTemplate.Application.Abstractions.Time;
using CleanArchTemplate.Infrastructure.Caching;
using CleanArchTemplate.Infrastructure.Links;
using CleanArchTemplate.Infrastructure.Messaging;
using CleanArchTemplate.Infrastructure.Notifications;
using CleanArchTemplate.Infrastructure.Options;
using CleanArchTemplate.Infrastructure.Persistence;
using CleanArchTemplate.Infrastructure.Persistence.Interceptors;
using CleanArchTemplate.Infrastructure.Persistence.Outbox;
using CleanArchTemplate.Infrastructure.Persistence.Repositories;
using CleanArchTemplate.Infrastructure.Security;
using CleanArchTemplate.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace CleanArchTemplate.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers every outbound adapter. Composed of focused blocks so a host can take only what
    /// it needs - the migrator, for instance, wants persistence and Kafka admin but no consumers.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddInfrastructureOptions(configuration)
            .AddPersistence()
            .AddCaching()
            .AddMessaging()
            .AddSecurity()
            .AddNotifications();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IAppLinkBuilder, AppLinkBuilder>();

        return services;
    }

    public static IServiceCollection AddInfrastructureOptions(this IServiceCollection services, IConfiguration configuration)
    {
        AddValidated<AppOptions>(services, configuration, AppOptions.SectionName);
        AddValidated<DatabaseOptions>(services, configuration, DatabaseOptions.SectionName);
        AddValidated<KafkaOptions>(services, configuration, KafkaOptions.SectionName);
        AddValidated<RedisOptions>(services, configuration, RedisOptions.SectionName);
        AddValidated<JwtOptions>(services, configuration, JwtOptions.SectionName);
        AddValidated<EmailOptions>(services, configuration, EmailOptions.SectionName);
        AddValidated<OutboxOptions>(services, configuration, OutboxOptions.SectionName);

        return services;
    }

    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<DomainEventsToOutboxInterceptor>();

        services.AddDbContext<AppDbContext>((provider, builder) =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>().Value;

            builder.UseNpgsql(options.ConnectionString, npgsql =>
            {
                npgsql.CommandTimeout(options.CommandTimeoutSeconds);
                npgsql.EnableRetryOnFailure(options.MaxRetryCount, TimeSpan.FromSeconds(5), errorCodesToAdd: null);
                npgsql.MigrationsHistoryTable("__ef_migrations_history");
            });

            builder.AddInterceptors(
                provider.GetRequiredService<AuditableEntityInterceptor>(),
                provider.GetRequiredService<DomainEventsToOutboxInterceptor>());

            builder.EnableSensitiveDataLogging(options.EnableSensitiveDataLogging);
            builder.EnableDetailedErrors(options.EnableDetailedErrors);
        });

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUserReadRepository, UserReadRepository>();

        return services;
    }

    public static IServiceCollection AddCaching(this IServiceCollection services)
    {
        // One multiplexer per process: it is thread-safe and multiplexes all commands over few sockets.
        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RedisOptions>>().Value;
            var configuration = ConfigurationOptions.Parse(options.ConnectionString);
            configuration.AbortOnConnectFail = false;
            configuration.ConnectRetry = 5;
            return ConnectionMultiplexer.Connect(configuration);
        });

        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddSingleton<IEmailDeduplicator, RedisEmailDeduplicator>();

        return services;
    }

    public static IServiceCollection AddMessaging(this IServiceCollection services)
    {
        services.AddSingleton<ITopicResolver, TopicResolver>();
        services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
        services.AddSingleton<IKafkaTopicProvisioner, KafkaTopicProvisioner>();

        return services;
    }

    public static IServiceCollection AddSecurity(this IServiceCollection services)
    {
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();

        // Hosts with a request principal (the API) replace this with an HttpContext-backed one.
        services.TryAddAnonymousCurrentUser();

        return services;
    }

    public static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        services.AddSingleton<IEmailTemplateRenderer, EmailTemplateRenderer>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        return services;
    }

    /// <summary>Hosts the outbox drain. Run it in the worker, not the API, so request latency is unaffected.</summary>
    public static IServiceCollection AddOutboxProcessor(this IServiceCollection services)
    {
        services.AddHostedService<OutboxProcessor>();
        return services;
    }

    private static void TryAddAnonymousCurrentUser(this IServiceCollection services)
    {
        if (services.All(descriptor => descriptor.ServiceType != typeof(ICurrentUser)))
        {
            services.AddScoped<ICurrentUser, AnonymousCurrentUser>();
        }
    }

    private static void AddValidated<TOptions>(IServiceCollection services, IConfiguration configuration, string section)
        where TOptions : class
    {
        // ValidateOnStart turns a bad connection string or a short signing key into a startup
        // failure rather than a 500 on the first request that happens to need it.
        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
