using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace Remito.IntegrationTests;

public sealed class SqlServerContainerTests : IAsyncLifetime
{
    private readonly MsSqlContainer sqlServer = new MsSqlBuilder()
        .WithEnvironment("MSSQL_MEMORY_LIMIT_MB", "2048")
        .Build();

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

    [Fact]
    public async Task Initial_migration_applies_to_an_empty_database_and_seeds_idempotently()
    {
        var connectionString = new SqlConnectionStringBuilder(sqlServer.GetConnectionString())
        {
            InitialCatalog = "RemitoMigrationTests"
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<RemitoDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        await using var dbContext = new RemitoDbContext(options);

        Assert.Empty(await dbContext.Database.GetAppliedMigrationsAsync());
        await dbContext.Database.MigrateAsync();
        await dbContext.Database.MigrateAsync();

        Assert.Equal(10, await dbContext.Currencies.CountAsync());
        Assert.Contains(await dbContext.Corridors.ToListAsync(), corridor =>
            corridor.FromCurrency == "OMR" && corridor.ToCurrency == "INR" && corridor.IsActive);
        Assert.Equal(10, await dbContext.Exchanges.CountAsync(exchange => exchange.IsPlaceholder));
        Assert.All(
            await dbContext.Exchanges.Where(exchange => exchange.IsPlaceholder).ToListAsync(),
            exchange => Assert.Equal(ExchangeStatus.Pending, exchange.Status));
    }
}
