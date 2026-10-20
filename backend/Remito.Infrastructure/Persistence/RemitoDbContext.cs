using Microsoft.EntityFrameworkCore;

namespace Remito.Infrastructure.Persistence;

public sealed class RemitoDbContext(DbContextOptions<RemitoDbContext> options) : DbContext(options);
