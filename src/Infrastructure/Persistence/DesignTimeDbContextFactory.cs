using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CtwDbContext>
{
    public CtwDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("CTW_DESIGN_CONNECTION")
                 ?? "Host=localhost;Port=5432;Database=ctwdb;Username=postgres;Password=postgres";
        var opts = new DbContextOptionsBuilder<CtwDbContext>().UseNpgsql(cs).Options;
        return new CtwDbContext(opts);
    }
}
