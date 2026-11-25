using Fundoo.Common.DependencyInjection;
using Fundoo.Notes.Api.Contracts.Requests;
using Fundoo.Notes.Api.HealthChecks;
using Fundoo.Notes.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NLog;
using NLog.Web;
using System.Text;

var logger = LogManager.Setup()
    .LoadConfigurationFromAppSettings()
    .GetCurrentClassLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    var notesConnectionString =
        builder.Configuration.GetConnectionString("NotesDb");

    Console.WriteLine(
        $"NotesDb connection string loaded: {!string.IsNullOrWhiteSpace(notesConnectionString)}");

    // Add services to the container.
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

    builder.Services.AddFundooExceptionHandling();
    builder.Services.AddFundooFluentValidation(typeof(CreateNoteRequest).Assembly);

    builder.Services.AddNotesInfrastructure(builder.Configuration);
    builder.Services
        .AddHealthChecks()
        .AddCheck<NotesDatabaseHealthCheck>(
            "notes-database",
            tags: new[] { "ready" });

    //jwt configuration
    var jwt = builder.Configuration.GetSection("Jwt");

    var signingKey = jwt["SigningKey"]
        ?? throw new InvalidOperationException("JWT signing key is missing.");

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            // Preserve claim names such as "sub" and "email".
            options.MapInboundClaims = false;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt["Issuer"],

                ValidateAudience = true,
                ValidAudience = jwt["Audience"],

                ValidateLifetime = true,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(signingKey)
                ),

                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });

    builder.Services.AddAuthorization();

    //swagger authorize configuration
    builder.Services.AddSwaggerGen(options =>
    {
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT token."
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
    });


    var app = builder.Build();

    // Configure the HTTP request pipeline.
    app.UseCors();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Fundoo Notes API V1");
    });

    logger.Info("Fundoo Notes API is starting.");

    // Ensure Notes and Labels tables exist
    try
    {
        var factory = app.Services.GetRequiredService<Fundoo.Notes.Infrastructure.Data.NotesDbConnectionFactory>();
        await using var conn = factory.CreateConnection();
        await conn.OpenAsync();
        const string ddl = @"
CREATE TABLE IF NOT EXISTS notes (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL,
    title varchar(200) NOT NULL,
    content text NOT NULL DEFAULT '',
    color varchar(50) NULL,
    is_pinned boolean NOT NULL DEFAULT FALSE,
    is_archived boolean NOT NULL DEFAULT FALSE,
    is_trashed boolean NOT NULL DEFAULT FALSE,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_notes_user_active ON notes (user_id, is_pinned DESC, created_at_utc DESC) WHERE is_archived = FALSE AND is_trashed = FALSE;
CREATE INDEX IF NOT EXISTS ix_notes_user_archived ON notes (user_id, updated_at_utc DESC) WHERE is_archived = TRUE AND is_trashed = FALSE;
CREATE INDEX IF NOT EXISTS ix_notes_user_trashed ON notes (user_id, updated_at_utc DESC) WHERE is_trashed = TRUE;
CREATE TABLE IF NOT EXISTS labels (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL,
    name varchar(100) NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_labels_user_name ON labels (user_id, name);
CREATE TABLE IF NOT EXISTS note_labels (
    note_id uuid NOT NULL REFERENCES notes(id) ON DELETE CASCADE,
    label_id uuid NOT NULL REFERENCES labels(id) ON DELETE CASCADE,
    PRIMARY KEY (note_id, label_id)
);
CREATE INDEX IF NOT EXISTS ix_note_labels_label_id ON note_labels (label_id);
CREATE TABLE IF NOT EXISTS note_collaborators (
    id uuid PRIMARY KEY,
    note_id uuid NOT NULL REFERENCES notes(id) ON DELETE CASCADE,
    collaborator_email varchar(255) NOT NULL,
    added_at_utc timestamptz NOT NULL,
    CONSTRAINT uq_note_collaborator UNIQUE (note_id, collaborator_email)
);
CREATE INDEX IF NOT EXISTS ix_note_collaborators_email ON note_collaborators (collaborator_email);
";
        await using var cmd = new Npgsql.NpgsqlCommand(ddl, conn);
        await cmd.ExecuteNonQueryAsync();
        logger.Info("Notes and Labels tables verified successfully.");
    }
    catch (Exception ex)
    {
        logger.Error(ex, "Failed to verify Notes tables on startup.");
    }

    if (!app.Environment.IsProduction())
    {
        app.UseHttpsRedirection();
    }

    app.UseFundooCorrelation();
    app.UseExceptionHandler();
    app.UseFundooInternalServiceAuth();
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
