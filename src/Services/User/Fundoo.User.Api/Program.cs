using Asp.Versioning;
using NLog;
using NLog.Web;
using Fundoo.Common.DependencyInjection;
using Fundoo.Common.HealthChecks;
using Fundoo.User.Api.Contracts;
using Fundoo.User.Api.Extensions;
using Fundoo.User.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;


var logger = LogManager.Setup()
    .LoadConfigurationFromAppSettings()
    .GetCurrentClassLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    // Add services to the container.
    builder.Services.AddNotificationClient(builder.Configuration);
    builder.Services.AddFundooExceptionHandling();
    builder.Services.AddFundooFluentValidation(typeof(RegisterRequest).Assembly);

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
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();

    builder.Services
        .AddHealthChecks()
        .AddDbContextCheck<UserDbContext>(
            "User PostgreSQL Database",
            tags: new[] { "ready" })
        .AddCheck<KafkaHealthCheck>(
            "User Kafka Broker",
            tags: new[] { "ready" });

    //Swagger implementation
    builder.Services.AddSwaggerGen(options =>
    {
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT access token."
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

        options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "Fundoo User API",
            Version = "v1"
        });

        options.SwaggerDoc("v2", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "Fundoo User API",
            Version = "v2"
        });

        options.DocInclusionPredicate((documentName, apiDescription) =>
        apiDescription.GroupName == documentName);
    });

    builder.Services.AddUserInfrastructure(builder.Configuration);

    var signingKey = builder.Configuration["Jwt:SigningKey"]
        ?? throw new InvalidOperationException(
            "JWT signing key is not configured.");

    var issuer = builder.Configuration["Jwt:Issuer"]
        ?? throw new InvalidOperationException(
            "JWT issuer is not configured.");

    var audience = builder.Configuration["Jwt:Audience"]
        ?? throw new InvalidOperationException(
            "JWT audience is not configured.");

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,

                ValidateAudience = true,
                ValidAudience = audience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(signingKey)),

                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

    builder.Services
        .AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);

            options.AssumeDefaultVersionWhenUnspecified = true;

            options.ReportApiVersions = true;

            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddMvc()
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";

            options.SubstituteApiVersionInUrl = true;
        });

    var app = builder.Build();

    logger.Info("Fundoo User API is starting.");

    // Automatically apply pending database migrations
    try
    {
        using var migrationScope = app.Services.CreateScope();
        var userDb = migrationScope.ServiceProvider.GetRequiredService<UserDbContext>();
        await userDb.Database.MigrateAsync();
        logger.Info("User database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        logger.Error(ex, "Failed to apply database migrations on startup.");
    }

    // Configure the HTTP request pipeline.
    app.UseCors();
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "Fundoo User API V1");

        options.SwaggerEndpoint(
            "/swagger/v2/swagger.json",
            "Fundoo User API V2");
    });

    if (!app.Environment.IsProduction())
    {
        app.UseHttpsRedirection();
    }

    app.UseFundooCorrelation();
    app.UseExceptionHandler();
    app.UseAuthentication();
    app.UseAuthorization();

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