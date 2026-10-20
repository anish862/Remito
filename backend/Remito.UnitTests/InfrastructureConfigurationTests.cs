using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Remito.Application.Abstractions;
using Remito.Infrastructure;
using StackExchange.Redis;

namespace Remito.UnitTests;

public sealed class InfrastructureConfigurationTests
{
    [Fact]
    public void Disabled_redis_does_not_register_cache_or_connect_to_redis()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Enabled"] = "false",
                ["Redis:ConnectionString"] = "not-a-redis-connection-string"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        services.Should().NotContain(service => service.ServiceType == typeof(IConnectionMultiplexer));
        services.Should().NotContain(service => service.ServiceType == typeof(IRateCache));
    }
}
