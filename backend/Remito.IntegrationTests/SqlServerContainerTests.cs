using Microsoft.EntityFrameworkCore;
using Remito.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace Remito.IntegrationTests;

public sealed class SqlServerContainerTests : IAsyncLifetime
{
    private readonly MsSqlContainer sqlServer = new MsSqlBuilder().Build();

    public Task InitializeAsync() => sqlServer.StartAsync();

    public async Task DisposeAsync() => await sqlServer.DisposeAsync();

    [Fact]
    public async Task DbContext_can_connect_to_sql_server_container()
    {
        var options = new DbContextOptionsBuilder<RemitoDbContext>()
            .UseSqlServer(sqlServer.GetConnectionString())
            .Options;
        await using var dbContext = new RemitoDbContext(options);

        Assert.True(await dbContext.Database.CanConnectAsync());
    }
}
