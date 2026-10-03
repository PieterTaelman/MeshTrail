using Microsoft.EntityFrameworkCore;

namespace Meshtrail.Core.Infrastructure.Persistence;

/// <summary>
/// EF Core context. The schema itself lives in the .sqlproj (no migrations), so this only maps to existing tables.
/// </summary>
public sealed class MeshtrailDbContext(DbContextOptions<MeshtrailDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration in this assembly (one per Db* entity).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MeshtrailDbContext).Assembly);
    }
}
