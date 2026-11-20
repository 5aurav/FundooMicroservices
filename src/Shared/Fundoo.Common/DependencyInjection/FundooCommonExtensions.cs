using System.Reflection;
using FluentValidation;
using Fundoo.Common.Caching;
using Fundoo.Common.Logging;
using Fundoo.Common.Messaging;
using Fundoo.Common.Middleware;
using Fundoo.Common.Security;
using Fundoo.Common.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fundoo.Common.DependencyInjection;

public static class FundooCommonExtensions
{
    /// <summary>
    /// Registers Redis distributed cache and cache service abstraction.
    /// </summary>
    public static IServiceCollection AddFundooRedisCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisSettings = configuration.GetSection(RedisCacheSettings.SectionName).Get<RedisCacheSettings>()
            ?? new RedisCacheSettings();

        services.Configure<RedisCacheSettings>(configuration.GetSection(RedisCacheSettings.SectionName));

        if (redisSettings.Enabled)
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisSettings.Configuration;
                options.InstanceName = redisSettings.InstanceName;
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.AddSingleton<IRedisDistributedCacheService, RedisDistributedCacheService>();
        return services;
    }

    /// <summary>
    /// Registers the Kafka producer and configuration.
    /// </summary>
    public static IServiceCollection AddFundooKafkaProducer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<KafkaSettings>(configuration.GetSection(KafkaSettings.SectionName));
        services.AddSingleton<IKafkaProducer, KafkaProducer>();
        return services;
    }

    /// <summary>
    /// Adds internal service authentication middleware to enforce gateway-only access.
    /// </summary>
    public static IApplicationBuilder UseFundooInternalServiceAuth(this IApplicationBuilder app)
        => app.UseMiddleware<InternalServiceAuthMiddleware>();

    /// <summary>
    /// Registers the centralized <see cref="GlobalExceptionHandler"/> and
    /// ProblemDetails support so unhandled exceptions become RFC 7807 responses.
    /// </summary>
    public static IServiceCollection AddFundooExceptionHandling(
        this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        return services;
    }

    /// <summary>
    /// Registers FluentValidation validators from the supplied assemblies and
    /// wires the <see cref="FluentValidationActionFilter"/> into the MVC pipeline.
    /// </summary>
    public static IServiceCollection AddFundooFluentValidation(
        this IServiceCollection services,
        params Assembly[] validatorAssemblies)
    {
        services.Configure<MvcOptions>(
            options => options.Filters.Add<FluentValidationActionFilter>());

        if (validatorAssemblies is { Length: > 0 })
        {
            services.AddValidatorsFromAssemblies(validatorAssemblies);
        }

        return services;
    }

    /// <summary>
    /// Adds the <see cref="CorrelationIdMiddleware"/>. Register it as early as
    /// possible so every downstream log and error response carries the id.
    /// </summary>
    public static IApplicationBuilder UseFundooCorrelation(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
