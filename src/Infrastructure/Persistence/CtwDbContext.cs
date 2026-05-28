using Core;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Infrastructure.Persistence;

public class CtwDbContext : DbContext, IDataProtectionKeyContext
{
    public CtwDbContext(DbContextOptions<CtwDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<CTraderIdAccount> CTids => Set<CTraderIdAccount>();
    public DbSet<TradingAccount> TradingAccounts => Set<TradingAccount>();
    public DbSet<CBot> CBots => Set<CBot>();
    public DbSet<CBotSourceProject> CBotSourceProjects => Set<CBotSourceProject>();
    public DbSet<ParamSet> ParamSets => Set<ParamSet>();
    public DbSet<Node> Nodes => Set<Node>();
    public DbSet<NodeStats> NodeStats => Set<NodeStats>();
    public DbSet<Instance> Instances => Set<Instance>();
    public DbSet<InstanceLog> InstanceLogs => Set<InstanceLog>();
    public DbSet<ViewerGrant> ViewerGrants => Set<ViewerGrant>();
    public DbSet<McpApiKey> McpApiKeys => Set<McpApiKey>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey> DataProtectionKeys
        => Set<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey>();

    public override int SaveChanges() { ApplySoftDelete(); return base.SaveChanges(); }
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    { ApplySoftDelete(); return base.SaveChangesAsync(ct); }

    private void ApplySoftDelete()
    {
        foreach (var entry in ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = DateTimeOffset.UtcNow;
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var b = modelBuilder;

        foreach (var entityType in b.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                var param = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
                var prop = System.Linq.Expressions.Expression.Property(param, nameof(ISoftDeletable.IsDeleted));
                var notDeleted = System.Linq.Expressions.Expression.Equal(
                    prop, System.Linq.Expressions.Expression.Constant(false));
                var lambda = System.Linq.Expressions.Expression.Lambda(notDeleted, param);
                b.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }

        b.Entity<AppUser>(e =>
        {
            e.HasIndex(x => x.NormalizedEmail).IsUnique();
            e.Property(x => x.Role).HasConversion(v => v.Name, v => UserRole.FromName(v)).HasMaxLength(32);
        });

        b.Entity<CTraderIdAccount>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Username }).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TradingAccount>(e =>
        {
            e.HasIndex(x => new { x.CTidId, x.AccountNumber }).IsUnique();
            e.HasOne(x => x.CTid).WithMany(x => x.TradingAccounts).HasForeignKey(x => x.CTidId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CBot>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Name }).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.SourceProject).WithMany().HasForeignKey(x => x.SourceProjectId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<CBotSourceProject>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Name }).IsUnique();
            e.Property(x => x.Language).HasConversion(v => v.Name, v => CBotLanguage.FromName(v)).HasMaxLength(16);
        });

        b.Entity<ParamSet>(e =>
        {
            e.HasIndex(x => new { x.CBotId, x.Name }).IsUnique();
            e.HasOne(x => x.CBot).WithMany(x => x.ParamSets).HasForeignKey(x => x.CBotId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Node>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Mode).HasConversion(v => v.Name, v => NodeMode.FromName(v)).HasMaxLength(16);
            e.Property(x => x.Status).HasConversion(v => v.Name, v => NodeStatus.FromName(v)).HasMaxLength(16);
        });

        b.Entity<NodeStats>(e =>
        {
            e.HasKey(x => x.NodeId);
            e.HasOne(x => x.Node).WithOne(x => x.LatestStats).HasForeignKey<NodeStats>(x => x.NodeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Instance>(e =>
        {
            e.Property(x => x.Type).HasConversion(v => v.Name, v => InstanceType.FromName(v)).HasMaxLength(16);
            e.Property(x => x.Status).HasConversion(v => v.Name, v => InstanceStatus.FromName(v)).HasMaxLength(16);
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
        });

        b.Entity<InstanceLog>(e =>
        {
            e.HasIndex(x => new { x.InstanceId, x.Time });
            e.HasOne(x => x.Instance).WithMany().HasForeignKey(x => x.InstanceId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ViewerGrant>(e =>
        {
            e.HasKey(x => new { x.ViewerId, x.InstanceId });
            e.HasOne(x => x.Viewer).WithMany().HasForeignKey(x => x.ViewerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Instance).WithMany().HasForeignKey(x => x.InstanceId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<McpApiKey>(e =>
        {
            e.HasIndex(x => x.KeyPrefix).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AuditLog>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Time });
            e.HasIndex(x => new { x.EntityType, x.EntityId });
        });

        b.Entity<AppSetting>(e => e.HasKey(x => x.Key));
    }
}
