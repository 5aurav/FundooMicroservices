using Fundoo.Common.DependencyInjection;
using Fundoo.Common.HealthChecks;
using Fundoo.Notification.Api.Consumers;
using Fundoo.Notification.Api.Services;
using Fundoo.Notification.Application.Abstractions;
using Fundoo.Notification.Application.DTO;
using Fundoo.Notification.Infrastructure.Persistence;
using Fundoo.Notification.Infrastructure.Repositories;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Web;

var logger = LogManager.Setup()
    .LoadConfigurationFromAppSettings()
    .GetCurrentClassLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    // Register controllers
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    builder.Services.AddControllers();

    // Register Swagger/OpenAPI
    builder.Services.AddEndpointsApiExplorer();

    builder.Services.AddFundooExceptionHandling();
    builder.Services.AddFundooFluentValidation(typeof(PasswordResetNotificationRequest).Assembly);

    builder.Services.AddDbContext<NotificationDbContext>(options =>
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("NotificationDb")));

    builder.Services.AddScoped<
        INotificationIdempotencyRepository,
        NotificationIdempotencyRepository>();
    builder.Services.AddScoped<
        INotificationDeliveryRepository,
        NotificationDeliveryRepository>();

    builder.Services
        .AddHealthChecks()
        .AddDbContextCheck<NotificationDbContext>(
            "Notification PostgreSQL Database",
            tags: new[] { "ready" })
        .AddCheck<KafkaHealthCheck>(
            "Notification Kafka Broker",
            tags: new[] { "ready" });

    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc(
            "v1",
            new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "Fundoo Notification API",
                Version = "v1",
                Description = "API for sending Fundoo email notifications"
            });
    });

    // Register Notification Service
    builder.Services.AddTransient<IEmailSender, SmtpEmailService>();

    builder.Services.AddScoped<INotificationService, NotificationService>();

    builder.Services.AddFundooKafkaProducer(builder.Configuration);
    builder.Services.AddHostedService<KafkaNotificationConsumerService>();

    var app = builder.Build();

    // Automatically apply pending database migrations
    try
    {
        using var migrationScope = app.Services.CreateScope();
        var notifDb = migrationScope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await notifDb.Database.MigrateAsync();
        logger.Info("Notification database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        logger.Error(ex, "Failed to apply notification database migrations on startup.");
    }

    // Configure Swagger
    app.UseCors();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Fundoo Notification API V1");
    });

    logger.Info("Fundoo Notification API is starting.");

    if (!app.Environment.IsProduction())
    {
        app.UseHttpsRedirection();
    }

    app.UseFundooCorrelation();
    app.UseExceptionHandler();
    app.UseFundooInternalServiceAuth();
    app.UseAuthorization();

    // Map controller routes
    app.MapControllers();

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });

    app.Run();
}
catch (Exception exception)
{
    logger.Error(exception, "Application stopped because of an exception.");
    throw;
}
finally
{
    LogManager.Shutdown();
}
