using System.Linq.Expressions;
using Core;
using Core.Ai.CurrencyStrength;
using Core.Calendar;
using Core.Dashboard;
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
    public DbSet<CopyExecution> CopyExecutions => Set<CopyExecution>();
    public DbSet<CopyNotification> CopyNotifications => Set<CopyNotification>();
    public DbSet<CopyFeeAccrual> CopyFeeAccruals => Set<CopyFeeAccrual>();
    public DbSet<CopyProviderListing> CopyProviderListings => Set<CopyProviderListing>();
    public DbSet<ViewerGrant> ViewerGrants => Set<ViewerGrant>();
    public DbSet<McpApiKey> McpApiKeys => Set<McpApiKey>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<AiProviderCredential> AiProviderCredentials => Set<AiProviderCredential>();
    public DbSet<Core.Domain.AiFeatureBinding> AiFeatureBindings => Set<Core.Domain.AiFeatureBinding>();
    public DbSet<UserDashboard> UserDashboards => Set<UserDashboard>();
    public DbSet<AgentMandate> AgentMandates => Set<AgentMandate>();
    public DbSet<AgentProposal> AgentProposals => Set<AgentProposal>();
    public DbSet<Core.Agent.TradingAgent> TradingAgents => Set<Core.Agent.TradingAgent>();
    public DbSet<Core.Agent.AgentDecisionRecord> AgentDecisionRecords => Set<Core.Agent.AgentDecisionRecord>();
    public DbSet<Core.Agent.AgentMemoryRecord> AgentMemories => Set<Core.Agent.AgentMemoryRecord>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<AlertEvent> AlertEvents => Set<AlertEvent>();
    public DbSet<Core.Journal.JournalNote> JournalNotes => Set<Core.Journal.JournalNote>();
    public DbSet<PropRule> PropRules => Set<PropRule>();
    public DbSet<PropFirmChallenge> PropFirmChallenges => Set<PropFirmChallenge>();
    public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<OpenApiApplication> OpenApiApplications => Set<OpenApiApplication>();
    public DbSet<OpenApiAuthorization> OpenApiAuthorizations => Set<OpenApiAuthorization>();
    public DbSet<CopyProfile> CopyProfiles => Set<CopyProfile>();
    public DbSet<CopyDestination> CopyDestinations => Set<CopyDestination>();
    public DbSet<EconomicSeries> CalendarSeries => Set<EconomicSeries>();
    public DbSet<EconomicEvent> EconomicEvents => Set<EconomicEvent>();
    public DbSet<CalendarApiClient> CalendarApiClients => Set<CalendarApiClient>();
    public DbSet<CalendarWebhook> CalendarWebhooks => Set<CalendarWebhook>();
    public DbSet<CurrencyStrengthSnapshot> CurrencyStrengthSnapshots => Set<CurrencyStrengthSnapshot>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    // The calendar module lives in its own Postgres schema so its append-only churn stays logically
    // isolated from the trading core and can be dropped wholesale when white-label-disabled.
    public const string CalendarSchema = "calendar";

    public override int SaveChanges() { ApplySoftDelete(); return base.SaveChanges(); }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        ApplySoftDelete();
        return base.SaveChangesAsync(ct);
    }

    private void ApplySoftDelete()
    {
        foreach (var entry in ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
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
        configurationBuilder.Properties<InstanceLineageId>().HaveConversion<StrongIdConverter<InstanceLineageId>>();
        configurationBuilder.Properties<McpApiKeyId>().HaveConversion<StrongIdConverter<McpApiKeyId>>();
        configurationBuilder.Properties<AgentMandateId>().HaveConversion<StrongIdConverter<AgentMandateId>>();
        configurationBuilder.Properties<AgentProposalId>().HaveConversion<StrongIdConverter<AgentProposalId>>();
        configurationBuilder.Properties<TradingAgentId>().HaveConversion<StrongIdConverter<TradingAgentId>>();
        configurationBuilder.Properties<AgentDecisionRecordId>().HaveConversion<StrongIdConverter<AgentDecisionRecordId>>();
        configurationBuilder.Properties<AgentMemoryRecordId>().HaveConversion<StrongIdConverter<AgentMemoryRecordId>>();
        configurationBuilder.Properties<AlertRuleId>().HaveConversion<StrongIdConverter<AlertRuleId>>();
        configurationBuilder.Properties<JournalNoteId>().HaveConversion<StrongIdConverter<JournalNoteId>>();
        configurationBuilder.Properties<AlertEventId>().HaveConversion<StrongIdConverter<AlertEventId>>();
        configurationBuilder.Properties<PropRuleId>().HaveConversion<StrongIdConverter<PropRuleId>>();
        configurationBuilder.Properties<OpenApiApplicationId>().HaveConversion<StrongIdConverter<OpenApiApplicationId>>();
        configurationBuilder.Properties<OpenApiAuthorizationId>().HaveConversion<StrongIdConverter<OpenApiAuthorizationId>>();
        configurationBuilder.Properties<CopyProfileId>().HaveConversion<StrongIdConverter<CopyProfileId>>();
        configurationBuilder.Properties<CopyDestinationId>().HaveConversion<StrongIdConverter<CopyDestinationId>>();
        configurationBuilder.Properties<CopyProviderListingId>().HaveConversion<StrongIdConverter<CopyProviderListingId>>();
        configurationBuilder.Properties<CopyRunId>().HaveConversion<StrongIdConverter<CopyRunId>>();
        configurationBuilder.Properties<PropFirmChallengeId>().HaveConversion<StrongIdConverter<PropFirmChallengeId>>();
        configurationBuilder.Properties<EconomicSeriesId>().HaveConversion<StrongIdConverter<EconomicSeriesId>>();
        configurationBuilder.Properties<CalendarEventId>().HaveConversion<StrongIdConverter<CalendarEventId>>();
        configurationBuilder.Properties<CalendarApiClientId>().HaveConversion<StrongIdConverter<CalendarApiClientId>>();
        configurationBuilder.Properties<CalendarWebhookId>().HaveConversion<StrongIdConverter<CalendarWebhookId>>();
        configurationBuilder.Properties<LegalDocumentId>().HaveConversion<StrongIdConverter<LegalDocumentId>>();
        configurationBuilder.Properties<ConsentRecordId>().HaveConversion<StrongIdConverter<ConsentRecordId>>();
        configurationBuilder.Properties<UserDashboardId>().HaveConversion<StrongIdConverter<UserDashboardId>>();
        configurationBuilder.Properties<AiProviderCredentialId>().HaveConversion<StrongIdConverter<AiProviderCredentialId>>();
        configurationBuilder.Properties<AiFeatureBindingId>().HaveConversion<StrongIdConverter<AiFeatureBindingId>>();
        configurationBuilder.Properties<CurrencyStrengthSnapshotId>().HaveConversion<StrongIdConverter<CurrencyStrengthSnapshotId>>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasIndex(x => x.NormalizedEmail).IsUnique();
            e.HasIndex(x => x.ActivationState);
            e.HasDiscriminator<string>("Role")
                .HasValue<OwnerUser>("Owner")
                .HasValue<AdminUser>("Admin")
                .HasValue<RegularUser>("User")
                .HasValue<ViewerUser>("Viewer");
            e.Property(x => x.ActivationState).HasConversion<string>().HasMaxLength(32);
            e.HasMany(x => x.BackupCodes).WithOne().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.EmailVerificationTokens).WithOne().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.OwnsOne(x => x.Profile, p =>
            {
                p.Property(v => v.FullName).HasMaxLength(128);
                p.Property(v => v.DisplayName).HasMaxLength(128);
                p.Property(v => v.CountryCode).HasMaxLength(2);
                p.Property(v => v.PhoneNumber).HasMaxLength(20);
                p.Property(v => v.Company).HasMaxLength(128);
                p.Property(v => v.Locale).HasMaxLength(32);
                p.Property(v => v.TimeZone).HasMaxLength(64);
            });
        });

        modelBuilder.Entity<AppUser>().Navigation(x => x.BackupCodes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        modelBuilder.Entity<AppUser>().Navigation(x => x.EmailVerificationTokens)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        modelBuilder.Entity<AppUser>().Navigation(x => x.Profile)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        modelBuilder.Entity<MfaBackupCode>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.CodeHash });
        });

        modelBuilder.Entity<EmailVerificationToken>(e =>
        {
            e.HasIndex(x => x.TokenHash);
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
            e.Property(x => x.LinkMethod).HasConversion<string>().HasMaxLength(16);
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

        modelBuilder.Entity<AiProviderCredential>(e =>
        {
            // At most one active credential PER SCOPE — two partial unique indexes enforce it at the DB
            // (defence in depth behind the store's activate-exclusivity): one active deployment default
            // (OwnerUserId IS NULL) and one active credential per user (OwnerUserId IS NOT NULL).
            e.HasIndex(x => x.IsActive).IsUnique()
                .HasFilter("\"OwnerUserId\" IS NULL AND \"IsActive\" = true AND \"IsDeleted\" = false");
            e.HasIndex(x => new { x.OwnerUserId, x.IsActive }).IsUnique()
                .HasFilter("\"OwnerUserId\" IS NOT NULL AND \"IsActive\" = true AND \"IsDeleted\" = false");
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.BaseUrl).HasMaxLength(512);
            e.Property(x => x.Model).HasMaxLength(256);
        });

        modelBuilder.Entity<Core.Domain.AiFeatureBinding>(e =>
        {
            // At most one binding per (scope, feature): one deployment default (OwnerUserId IS NULL) and one
            // per user (OwnerUserId IS NOT NULL) for each feature. Two partial unique indexes enforce it.
            e.HasIndex(x => x.Feature).IsUnique()
                .HasFilter("\"OwnerUserId\" IS NULL AND \"IsDeleted\" = false");
            e.HasIndex(x => new { x.OwnerUserId, x.Feature }).IsUnique()
                .HasFilter("\"OwnerUserId\" IS NOT NULL AND \"IsDeleted\" = false");
            e.Property(x => x.Feature).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<UserDashboard>(e =>
        {
            e.HasIndex(x => x.UserId).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.OwnsMany(x => x.Widgets, b => b.ToJson());
        });
        modelBuilder.Entity<UserDashboard>().Navigation(x => x.Widgets)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

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

        modelBuilder.Entity<AlertRule>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Name }).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Trigger).HasConversion<string>().HasMaxLength(24);
            e.Ignore(x => x.MinImpact);
        });

        modelBuilder.Entity<AlertEvent>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.HasOne(x => x.Rule).WithMany(x => x.Events).HasForeignKey(x => x.RuleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Core.Journal.JournalNote>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PropRule>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.TradingAccountId }).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.TradingAccount).WithMany().HasForeignKey(x => x.TradingAccountId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PropFirmChallenge>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Phase).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.DrawdownMode).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.Breach).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.DailyLossBasis).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<Core.Agent.TradingAgent>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Archetype).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.Autonomy).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            // Computed accessors derived from stored columns — not persisted.
            e.Ignore(x => x.Goals);
            e.Ignore(x => x.Envelope);
            e.Ignore(x => x.Temperament);
            e.Ignore(x => x.ManagedAccounts);
        });

        modelBuilder.Entity<Core.Agent.AgentDecisionRecord>(e =>
        {
            e.HasIndex(x => new { x.AgentId, x.Sequence });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Approval).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<Core.Agent.AgentMemoryRecord>(e =>
        {
            e.HasIndex(x => new { x.AgentId, x.CreatedAt });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Tier).HasConversion<string>().HasMaxLength(24);
        });

        modelBuilder.Entity<LegalDocument>(e =>
        {
            e.HasIndex(x => new { x.Type, x.Version }).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<ConsentRecord>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.DocumentType, x.Version });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.DocumentType).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<OpenApiApplication>(e =>
        {
            e.HasIndex(x => x.UserId).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasIndex(x => x.IsShared).IsUnique().HasFilter("\"IsShared\" = true AND \"IsDeleted\" = false");
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OpenApiAuthorization>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.CtidUserId }).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<OpenApiApplication>().WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Scope).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<CopyProfile>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Name }).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.HasMany(x => x.Destinations).WithOne().HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CopyDestination>(e =>
        {
            e.HasIndex(x => new { x.ProfileId, x.DestinationAccountId }).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.Property(x => x.RiskMode).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.Direction).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.SymbolFilterMode).HasConversion<string>().HasMaxLength(16);
            e.OwnsMany(x => x.SymbolMaps, b => b.ToJson());
            e.OwnsMany(x => x.SymbolFilters, b => b.ToJson());
        });

        modelBuilder.Entity<CopyExecution>(e =>
        {
            e.HasIndex(x => new { x.ProfileId, x.OccurredAt });
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<CopyNotification>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.OccurredAt });
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Severity).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<CopyFeeAccrual>(e => e.HasIndex(x => new { x.UserId, x.SettledAt }));

        modelBuilder.Entity<CopyProviderListing>(e =>
        {
            e.HasIndex(x => x.ProfileId).IsUnique().HasFilter("\"IsDeleted\" = false"); // one listing per profile
            e.HasIndex(x => x.Published);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CopyProfile>().Navigation(x => x.Destinations)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        modelBuilder.Entity<CopyDestination>().Navigation(x => x.SymbolMaps)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        modelBuilder.Entity<CopyDestination>().Navigation(x => x.SymbolFilters)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        modelBuilder.Entity<CTraderIdAccount>().Navigation(x => x.TradingAccounts)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        modelBuilder.Entity<CBot>().Navigation(x => x.ParamSets)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        modelBuilder.Entity<AgentMandate>().Navigation(x => x.Proposals)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        modelBuilder.Entity<AlertRule>().Navigation(x => x.Events)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        modelBuilder.Entity<EconomicSeries>(e =>
        {
            e.ToTable("series", CalendarSchema);
            e.Ignore(x => x.Code);
            e.Ignore(x => x.Country);
            e.HasIndex(x => x.SeriesCodeValue).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.Property(x => x.Category).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.Cadence).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.DefaultImpact).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<EconomicEvent>(e =>
        {
            e.ToTable("economic_event", CalendarSchema);
            e.Ignore(x => x.SeriesCode);
            e.Ignore(x => x.Country);
            e.Ignore(x => x.LatestRevision);
            // The two hot queries: "next events by currency+impact" (EffectiveAt) and idempotent upsert
            // by (series, instant).
            e.HasIndex(x => new { x.SeriesId, x.EffectiveAt }).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasIndex(x => x.EffectiveAt);
            e.Property(x => x.Precision).HasConversion<string>().HasMaxLength(16);
            e.OwnsMany(x => x.Revisions, b =>
            {
                b.ToTable("event_revision", CalendarSchema);
                b.WithOwner().HasForeignKey("CalendarEventId");
                b.HasKey("CalendarEventId", nameof(EventRevision.Sequence));
                b.Property(r => r.Sequence).ValueGeneratedNever();
                b.HasIndex("CalendarEventId", nameof(EventRevision.KnownAt));
                b.Property(r => r.Kind).HasConversion<string>().HasMaxLength(16);
                b.Property(r => r.ImpactLevel).HasConversion<string>().HasMaxLength(16);
                b.Property(r => r.Actual).HasPrecision(18, 6);
                b.Property(r => r.Forecast).HasPrecision(18, 6);
                b.Property(r => r.Previous).HasPrecision(18, 6);
            });
            e.Navigation(x => x.Revisions).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<CalendarApiClient>(e =>
        {
            e.ToTable("api_client", CalendarSchema);
            e.HasIndex(x => x.KeyPrefix).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CalendarWebhook>(e =>
        {
            e.ToTable("webhook", CalendarSchema);
            e.HasIndex(x => x.OwnerId);
            e.Ignore(x => x.MinImpact);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CurrencyStrengthSnapshot>(e =>
        {
            e.ToTable("currency_strength_snapshot");
            e.HasIndex(x => x.AsOf);
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.RankingJson).HasColumnType("jsonb");
            e.Property(x => x.HorizonsJson).HasColumnType("jsonb");
            e.Property(x => x.IndicatorsJson).HasColumnType("jsonb");
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
