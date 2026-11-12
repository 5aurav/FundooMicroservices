using Fundoo.Notification.Api.Services;
using Fundoo.Notification.Application.Abstractions;
using Fundoo.Notification.Infrastructure.Persistence;
using Fundoo.Notification.Infrastructure.Repositories;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Register controllers
builder.Services.AddControllers();

// Register Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("NotificationDb")));

builder.Services.AddScoped<
    INotificationIdempotencyRepository,
    NotificationIdempotencyRepository>();

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<NotificationDbContext>(
        "Notification PostgreSQL Database",
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

builder.Services.AddScoped<
    INotificationIdempotencyRepository,
    NotificationIdempotencyRepository>();

builder.Services.AddScoped<INotificationService, NotificationService>();

var app = builder.Build();

// Configure Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

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