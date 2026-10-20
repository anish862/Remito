using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Remito.Application.Abstractions;
using Remito.Infrastructure.Caching;
using Remito.Infrastructure.Persistence;
using StackExchange.Redis;

namespace Remito.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var databaseConnection = configuration.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(databaseConnection))
        {
            services.AddDbContext<RemitoDbContext>(options => options.UseSqlServer(databaseConnection));
        }

        var redisConnection = configuration["Redis:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            var connection = ConnectionMultiplexer.Connect(redisConnection);
            services.AddSingleton<IConnectionMultiplexer>(connection);
            services.AddSingleton<IRateCache, RedisRateCache>();
        }

        return services;
    }
}
