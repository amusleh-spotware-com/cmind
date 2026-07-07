using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DataContext>
{
    public DataContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("APP_DESIGN_CONNECTION")
                 ?? "Host=localhost;Port=5432;Database=appdb;Username=postgres;Password=postgres";
        var opts = new DbContextOptionsBuilder<DataContext>().UseNpgsql(cs).Options;
        return new DataContext(opts);
    }
}
