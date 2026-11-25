using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var envConfigFile = $"ocelot.{builder.Environment.EnvironmentName}.json";
if (File.Exists(Path.Combine(builder.Environment.ContentRootPath, envConfigFile)))
{
    builder.Configuration.AddJsonFile(envConfigFile, optional: false, reloadOnChange: true);
}
else
{
    builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
}

const string authenticationProviderKey = "FundooGatewayKey";

var signingKey = builder.Configuration["Jwt:SigningKey"] 
    ?? "FundooSuperSecretKeyForJwtAuthenticationAndOcelotValidation2026!";
var issuer = builder.Configuration["Jwt:Issuer"] ?? "fundoo-user-api";
var audience = builder.Configuration["Jwt:Audience"] ?? "fundoo-clients";

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(authenticationProviderKey, options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("gateway", new OpenApiInfo
    {
        Title = "Fundoo API Gateway",
        Version = "v1",
        Description = "Unified API Documentation & Testing Console for Fundoo Microservices"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token to authorize API Gateway requests."
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

builder.Services.AddOcelot(builder.Configuration);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseCors();

app.MapHealthChecks(
    "/health",
    new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new
                {
                    status = report.Status.ToString(),
                    service = "Fundoo.ApiGateway",
                    timestampUtc = DateTime.UtcNow
                }));
        }
    });

app.MapGet("/", () => Results.Redirect("/swagger/index.html"));
app.MapGet("/swagger", () => Results.Redirect("/swagger/index.html"));

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/api-docs/user-v1/swagger.json", "User API");
    options.SwaggerEndpoint("/api-docs/notes/swagger.json", "Notes API");
    options.SwaggerEndpoint("/api-docs/notification/swagger.json", "Notification API");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "Fundoo Microservices Swagger Gateway";
});

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = "Healthy",
            service = "Fundoo.ApiGateway",
            timestampUtc = DateTime.UtcNow
        }));
        return;
    }

    if (!context.Request.Headers.ContainsKey("X-Client-Id"))
    {
        var clientId = context.Connection.RemoteIpAddress?.ToString();
        if (string.IsNullOrWhiteSpace(clientId)) clientId = "fundoo-browser-client";
        context.Request.Headers["X-Client-Id"] = clientId;
    }
    await next();
});

await app.UseOcelot();

app.Run();
