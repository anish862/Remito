using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Remito.Application.Rates;
using Remito.Infrastructure.Persistence;

namespace Remito.IntegrationTests;

public sealed class HealthEndpointTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> factory = new HealthApiFactory();

    [Fact]
    public async Task Health_returns_success()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Json_responses_are_compressed_when_requested()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/rates/latest?base=OMR&symbols=INR");
        request.Headers.AcceptEncoding.ParseAdd("gzip");

        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Contains("gzip", response.Content.Headers.ContentEncoding);
        Assert.Contains("Accept-Encoding", response.Headers.Vary);
        Assert.True(response.Headers.ETag?.IsWeak ?? false);
    }

    [Fact]
    public async Task Swagger_includes_passwordless_auth_and_bearer_security()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/auth/otp/request", out _));
        Assert.True(paths.TryGetProperty("/api/v1/rates/latest", out var latest));
        Assert.True(paths.TryGetProperty("/api/v1/rates/history", out var history));
        Assert.True(paths.TryGetProperty("/api/v1/convert", out var convert));
        Assert.True(paths.TryGetProperty("/api/v1/exchanges", out var exchanges));
        Assert.True(paths.TryGetProperty("/api/v1/compare", out var compare));
        Assert.True(paths.TryGetProperty("/api/v1/redirect", out var redirect));
        Assert.True(paths.TryGetProperty("/api/v1/partners/{exchangeId}/postback", out var postback));
        Assert.True(paths.TryGetProperty("/api/v1/favourites", out var favourites));
        Assert.True(paths.TryGetProperty("/api/v1/alerts", out var alerts));
        Assert.True(paths.TryGetProperty("/api/v1/news", out var news));
        Assert.True(paths.TryGetProperty("/api/v1/notification-preferences", out var notificationPreferences));
        Assert.True(latest.TryGetProperty("get", out _));
        Assert.True(history.TryGetProperty("get", out _));
        Assert.True(convert.TryGetProperty("post", out var convertPost));
        Assert.True(convertPost.TryGetProperty("requestBody", out _));
        Assert.True(exchanges.TryGetProperty("get", out _));
        Assert.True(compare.TryGetProperty("get", out _));
        Assert.True(redirect.TryGetProperty("post", out _));
        Assert.True(postback.TryGetProperty("post", out _));
        Assert.True(favourites.TryGetProperty("get", out _));
        Assert.True(favourites.TryGetProperty("post", out _));
        Assert.True(alerts.TryGetProperty("post", out _));
        Assert.True(news.TryGetProperty("get", out _));
        Assert.True(notificationPreferences.TryGetProperty("get", out _));
        Assert.True(document.RootElement.GetProperty("components")
            .GetProperty("schemas").TryGetProperty("CurrencyConversionResponse", out _));
        Assert.True(document.RootElement.GetProperty("components")
            .GetProperty("schemas").TryGetProperty("ExchangeOffer", out _));
        Assert.True(document.RootElement.GetProperty("components")
            .GetProperty("schemas").TryGetProperty("ExchangeComparisonResponse", out _));
        Assert.True(document.RootElement.GetProperty("components")
            .GetProperty("securitySchemes").TryGetProperty("Bearer", out _));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await factory.DisposeAsync();

    private sealed class HealthApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<RemitoDbContext>();
                services.RemoveAll<DbContextOptions<RemitoDbContext>>();
                services.AddDbContext<RemitoDbContext>(options =>
                    options.UseSqlServer(
                        "Server=localhost;Database=Remito;Trusted_Connection=True;TrustServerCertificate=True"));
                services.RemoveAll<IRateQueryService>();
                services.AddSingleton<IRateQueryService, StubRateQueryService>();
            });
    }

    private sealed class StubRateQueryService : IRateQueryService
    {
        public Task<LatestRatesResponse> GetLatestAsync(
            string baseCurrency,
            IReadOnlyList<string> symbols,
            CancellationToken cancellationToken)
        {
            var rates = Enumerable.Range(0, 100)
                .Select(_ => new LatestRateItem("INR", 100m, 0m, DateTimeOffset.UnixEpoch, false))
                .ToArray();
            return Task.FromResult(new LatestRatesResponse(baseCurrency, rates));
        }

        public Task<RateHistoryResponse> GetHistoryAsync(
            string pair,
            string range,
            CancellationToken cancellationToken) =>
            Task.FromResult(new RateHistoryResponse(pair, range, [], 0m, 0m, 0m, 0m));

        public Task<CurrencyConversionResponse> ConvertAsync(
            decimal amount,
            string fromCurrency,
            string toCurrency,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CurrencyConversionResponse(
                amount,
                fromCurrency,
                toCurrency,
                amount,
                1m,
                DateTimeOffset.UnixEpoch,
                false));
    }
}
