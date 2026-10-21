using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Remito.Application.Auth;
using Remito.Application.Abstractions;
using Remito.Infrastructure.Auth;
using Remito.Infrastructure.Caching;
using Remito.Infrastructure.Persistence;
using Remito.Infrastructure.Rates;
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

        services.Configure<MarketRateProviderOptions>(configuration.GetSection("Rates:Market"));
        services.PostConfigure<MarketRateProviderOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                options.ApiKey = configuration["RateProviders:ApiKey"];
            }
        });
        services.Configure<ExchangeFeedOptions>(configuration.GetSection("Rates:Exchanges"));
        services.Configure<RateGuardOptions>(configuration.GetSection("Rates:Guards"));
        services.Configure<ManualOverrideOptions>(configuration.GetSection("Rates:ManualOverride"));
        services.Configure<FixtureRateProviderOptions>(configuration.GetSection("Rates:Fixtures"));
        services.AddOptions<RateGuardOptions>()
            .Bind(configuration.GetSection("Rates:Guards"))
            .Validate(options => options.MaximumVariancePercent >= 0
                && options.HistoryRetentionDays is > 0 and <= 400
                && options.PairBounds.Values.All(bounds =>
                    bounds.Minimum > 0 && bounds.Maximum > bounds.Minimum),
                "Rate guard variance, retention, or pair-bound settings are invalid.")
            .ValidateOnStart();

        var marketOptions = configuration.GetSection("Rates:Market").Get<MarketRateProviderOptions>();
        var configuredSourceIds = new HashSet<Guid>();
        var configuredSourceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(marketOptions?.Endpoint))
        {
            ValidateSource(marketOptions.SourceId, marketOptions.SourceName);
            ValidateEndpoint(marketOptions.Endpoint, marketOptions.SourceName);
            if (marketOptions.StalenessMinutes <= 0)
            {
                throw new InvalidOperationException("Market rate staleness must be a positive number of minutes.");
            }

            services.AddHttpClient<MarketRateProvider>();
            services.AddTransient<IRateProvider>(provider =>
                provider.GetRequiredService<MarketRateProvider>());
        }

        services.AddHttpClient(nameof(ExchangeFeedProvider));
        var exchangeFeeds = configuration.GetSection("Rates:Exchanges").Get<ExchangeFeedOptions>()?.Feeds ?? [];
        foreach (var feed in exchangeFeeds)
        {
            ValidateSource(feed.SourceId, feed.Name);
            ValidateEndpoint(feed.Endpoint, feed.Name);
            if (feed.StalenessMinutes <= 0
                || !new[] { "json", "csv", "html" }.Contains(feed.Strategy, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Exchange feed '{feed.Name}' has an unsupported strategy or staleness window.");
            }

            services.AddTransient<IRateProvider>(provider => new ExchangeFeedProvider(
                provider.GetRequiredService<IHttpClientFactory>(),
                feed,
                provider.GetRequiredService<TimeProvider>()));
        }

        var fixtureOptions = configuration.GetSection("Rates:Fixtures").Get<FixtureRateProviderOptions>();
        if (fixtureOptions?.Enabled == true)
        {
            if (!isDevelopment)
            {
                throw new InvalidOperationException("Fixture rate providers can only be enabled in Development.");
            }

            ValidateSource(fixtureOptions.SourceId, fixtureOptions.SourceName);
            services.AddTransient<IRateProvider, FixtureRateProvider>();
        }

        var manualOptions = configuration.GetSection("Rates:ManualOverride").Get<ManualOverrideOptions>()
            ?? new ManualOverrideOptions();
        ValidateSource(manualOptions.SourceId, manualOptions.SourceName);
        if (manualOptions.StalenessMinutes <= 0)
        {
            throw new InvalidOperationException("Manual override staleness must be a positive number of minutes.");
        }

        services.AddTransient<IRateProvider, ManualRateOverrideProvider>();
        services.AddScoped<IManualRateOverrideService, ManualRateOverrideService>();
        services.AddSingleton<RatePipelineMetrics>();

        if (!string.IsNullOrWhiteSpace(databaseConnection))
        {
            services.TryAddSingleton<IRateCache>(
                isDevelopment
                    ? provider => new DevelopmentRateCache(provider.GetRequiredService<TimeProvider>())
                    : _ => new UnconfiguredRateCache());
            services.AddScoped<RateIngestionJob>();
            services.AddScoped<IRateIngestionJob>(provider =>
                provider.GetRequiredService<RateIngestionJob>());
            services.AddScoped<RateHistoryRollupJob>();
            services.AddScoped<IRateHistoryRollupJob>(provider =>
                provider.GetRequiredService<RateHistoryRollupJob>());
        }

        return services;

        void ValidateSource(Guid sourceId, string sourceName)
        {
            if (sourceId == Guid.Empty
                || string.IsNullOrWhiteSpace(sourceName)
                || !configuredSourceIds.Add(sourceId)
                || !configuredSourceNames.Add(sourceName))
            {
                throw new InvalidOperationException(
                    "Configured rate providers must have unique, non-empty source IDs and names.");
            }
        }

        static void ValidateEndpoint(string endpoint, string sourceName)
        {
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException($"The rate feed for '{sourceName}' must use an absolute HTTPS URL.");
            }
        }
    }
}
