using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Remito.Infrastructure.Persistence;

public sealed class RemitoDesignTimeDbContextFactory : IDesignTimeDbContextFactory<RemitoDbContext>
{
    public RemitoDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Server=localhost;Database=Remito;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<RemitoDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new RemitoDbContext(options);
    }
}
