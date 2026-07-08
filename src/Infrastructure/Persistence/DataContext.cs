using System.Linq.Expressions;
using Core;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Persistence;

public class DataContext : DbContext, IDataProtectionKeyContext
{
    public DataContext(DbContextOptions<DataContext> options) : base(options) { }

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
    public DbSet<AgentMandate> AgentMandates => Set<AgentMandate>();
    public DbSet<AgentProposal> AgentProposals => Set<AgentProposal>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

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

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<UserId>().HaveConversion<StrongIdConverter<UserId>>();
        configurationBuilder.Properties<CtidId>().HaveConversion<StrongIdConverter<CtidId>>();
        configurationBuilder.Properties<TradingAccountId>().HaveConversion<StrongIdConverter<TradingAccountId>>();
        configurationBuilder.Properties<CBotId>().HaveConversion<StrongIdConverter<CBotId>>();
        configurationBuilder.Properties<CBotSourceProjectId>().HaveConversion<StrongIdConverter<CBotSourceProjectId>>();
        configurationBuilder.Properties<ParamSetId>().HaveConversion<StrongIdConverter<ParamSetId>>();
        configurationBuilder.Properties<NodeId>().HaveConversion<StrongIdConverter<NodeId>>();
        configurationBuilder.Properties<InstanceId>().HaveConversion<StrongIdConverter<InstanceId>>();
        configurationBuilder.Properties<McpApiKeyId>().HaveConversion<StrongIdConverter<McpApiKeyId>>();
        configurationBuilder.Properties<AgentMandateId>().HaveConversion<StrongIdConverter<AgentMandateId>>();
        configurationBuilder.Properties<AgentProposalId>().HaveConversion<StrongIdConverter<AgentProposalId>>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasIndex(x => x.NormalizedEmail).IsUnique();
            e.HasDiscriminator<string>("Role")
                .HasValue<OwnerUser>("Owner")
                .HasValue<AdminUser>("Admin")
                .HasValue<RegularUser>("User")
                .HasValue<ViewerUser>("Viewer");
        });

        modelBuilder.Entity<CTraderIdAccount>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Username }).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TradingAccount>(e =>
        {
            e.HasIndex(x => new { x.CTidId, x.AccountNumber }).IsUnique();
            e.HasOne(x => x.CTid).WithMany(x => x.TradingAccounts).HasForeignKey(x => x.CTidId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CBot>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Name }).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.SourceProject).WithMany().HasForeignKey(x => x.SourceProjectId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CBotSourceProject>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Name }).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasDiscriminator<string>("Language")
                .HasValue<CSharpProject>("CSharp")
                .HasValue<PythonProject>("Python");
        });

        modelBuilder.Entity<ParamSet>(e =>
        {
            e.HasIndex(x => new { x.CBotId, x.Name }).IsUnique();
            e.HasOne(x => x.CBot).WithMany(x => x.ParamSets).HasForeignKey(x => x.CBotId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Node>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.HasDiscriminator<string>("Kind")
                .HasValue<ActiveRunNode>("ActiveRun")
                .HasValue<ActiveBacktestNode>("ActiveBacktest")
                .HasValue<ActiveMixedNode>("ActiveMixed")
                .HasValue<DecommissioningNode>("Decommissioning")
                .HasValue<OfflineNode>("Offline")
                .HasValue<LocalNode>("Local");
        });

        modelBuilder.Entity<NodeStats>(e =>
        {
            e.HasKey(x => x.NodeId);
            e.HasOne(x => x.Node).WithOne(x => x.LatestStats).HasForeignKey<NodeStats>(x => x.NodeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(ns => !ns.Node.IsDeleted);
        });

        modelBuilder.Entity<Instance>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.HasDiscriminator<string>("Kind")
                .HasValue<PendingRunInstance>("RunPending")
                .HasValue<ScheduledRunInstance>("RunScheduled")
                .HasValue<StartingRunInstance>("RunStarting")
                .HasValue<RunningRunInstance>("RunRunning")
                .HasValue<StoppingRunInstance>("RunStopping")
                .HasValue<StoppedRunInstance>("RunStopped")
                .HasValue<FailedRunInstance>("RunFailed")
                .HasValue<PendingBacktestInstance>("BacktestPending")
                .HasValue<ScheduledBacktestInstance>("BacktestScheduled")
                .HasValue<StartingBacktestInstance>("BacktestStarting")
                .HasValue<RunningBacktestInstance>("BacktestRunning")
                .HasValue<StoppingBacktestInstance>("BacktestStopping")
                .HasValue<CompletedBacktestInstance>("BacktestCompleted")
                .HasValue<FailedBacktestInstance>("BacktestFailed");
        });

        modelBuilder.Entity<InstanceLog>(e =>
        {
            e.HasIndex(x => new { x.InstanceId, x.Time });
            e.HasOne(x => x.Instance).WithMany().HasForeignKey(x => x.InstanceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ViewerGrant>(e =>
        {
            e.HasKey(x => new { x.ViewerId, x.InstanceId });
            e.HasOne(x => x.Viewer).WithMany().HasForeignKey(x => x.ViewerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Instance).WithMany().HasForeignKey(x => x.InstanceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<McpApiKey>(e =>
        {
            e.HasIndex(x => x.KeyPrefix).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Time });
            e.HasIndex(x => new { x.EntityType, x.EntityId });
        });

        modelBuilder.Entity<AppSetting>(e => e.HasKey(x => x.Key));

        modelBuilder.Entity<AgentMandate>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Name }).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CBot).WithMany().HasForeignKey(x => x.CBotId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.TradingAccount).WithMany().HasForeignKey(x => x.TradingAccountId).OnDelete(DeleteBehavior.SetNull);
            e.Property(x => x.Autonomy).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<AgentProposal>(e =>
        {
            e.HasIndex(x => new { x.MandateId, x.CreatedAt });
            e.HasOne(x => x.Mandate).WithMany(x => x.Proposals).HasForeignKey(x => x.MandateId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType)) continue;
            if (entityType.BaseType is not null) continue; // only root types in TPH
            var param = Expression.Parameter(entityType.ClrType, "e");
            var prop = Expression.Property(param, nameof(ISoftDeletable.IsDeleted));
            var notDeleted = Expression.Equal(prop, Expression.Constant(false));
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(notDeleted, param));
        }
    }
}

public sealed class StrongIdConverter<TId> : ValueConverter<TId, Guid>
    where TId : struct, IStronglyTypedId<TId>
{
    public StrongIdConverter() : base(id => id.Value, value => From(value)) { }
    private static TId From(Guid value) => TId.From(value);
}
