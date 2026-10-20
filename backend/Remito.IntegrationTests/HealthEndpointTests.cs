using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    public async Task Swagger_includes_passwordless_auth_and_bearer_security()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/auth/otp/request", out _));
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
            });
    }
}
