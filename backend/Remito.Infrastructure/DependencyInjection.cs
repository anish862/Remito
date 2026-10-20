using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Remito.Application.Auth;
using Remito.Application.Abstractions;
using Remito.Infrastructure.Auth;
using Remito.Infrastructure.Caching;
using Remito.Infrastructure.Persistence;
using StackExchange.Redis;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Remito.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment = false)
    {
        var databaseConnection = configuration.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(databaseConnection))
        {
            services.AddDbContext<RemitoDbContext>(options => options.UseSqlServer(databaseConnection));
        }

        if (configuration.GetValue<bool?>("Redis:Enabled") != false)
        {
            var redisConnection = configuration["Redis:ConnectionString"];
            if (!string.IsNullOrWhiteSpace(redisConnection))
            {
                var connection = ConnectionMultiplexer.Connect(redisConnection);
                services.AddSingleton<IConnectionMultiplexer>(connection);
                services.AddSingleton<IRateCache, RedisRateCache>();
                services.AddSingleton<IOtpRateLimiter, RedisOtpRateLimiter>();
            }
        }

        services.Configure<AuthOptions>(configuration.GetSection("Auth"));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IAuthService, AuthService>();
        services.TryAddSingleton<IOtpSender>(
            (IOtpSender)(isDevelopment ? new ConsoleOtpSender() : new UnconfiguredOtpSender()));
        services.TryAddSingleton<IOtpRateLimiter, UnconfiguredOtpRateLimiter>();

        return services;
    }
}
