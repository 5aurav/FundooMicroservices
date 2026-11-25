using Fundoo.Common.DependencyInjection;
using Fundoo.User.Application.Abstractions;
using Fundoo.User.Application.DTO;
using Fundoo.User.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fundoo.User.Infrastructure.Security
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddUserInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("UserDb")
                ?? throw new InvalidOperationException(
                    "UserDb connection string is not configured.");

            services.AddDbContext<UserDbContext>(options =>
                options.UseNpgsql(connectionString));

            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IPasswordHasher, PasswordHasher>();
            services.AddScoped<ITokenService, JwtTokenService>();

            services.AddScoped<IAuthService, AuthService>();

            services.Configure<KeycloakSettings>(configuration.GetSection(KeycloakSettings.SectionName));
            services.AddHttpClient<IOAuthService, KeycloakOAuthService>();

            services.Configure<GoogleOAuthSettings>(configuration.GetSection(GoogleOAuthSettings.SectionName));
            services.AddHttpClient<IGoogleOAuthService, GoogleOAuthService>();
            services.AddScoped<ISessionService, SessionService>();
            services.AddScoped<ITwoFactorAuthService, TotpTwoFactorAuthService>();

            services.AddFundooKafkaProducer(configuration);
            services.AddHostedService<OutboxProcessorBackgroundService>();

            return services;
        }
    }
}
