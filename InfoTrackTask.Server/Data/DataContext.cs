using Microsoft.EntityFrameworkCore;
using InfoTrackTask.Server.Models.db;
namespace InfoTrackTask.Server.Data;
class DataContext : DbContext
{
    public DataContext(DbContextOptions<DataContext> options) : base(options) { }

    public DbSet<SolicitorRecordEntity> Solicitors => Set<SolicitorRecordEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Add an index on SearchLocation so looking up cached items is lightning fast
        modelBuilder.Entity<SolicitorRecordEntity>()
            .HasIndex(s => s.SearchLocation);
    }
}