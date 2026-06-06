using Microsoft.EntityFrameworkCore;
using Priser.Models.Entities;

namespace Priser.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<CompanyValue> CompanyValues => Set<CompanyValue>();
    public DbSet<Recognition> Recognitions => Set<Recognition>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Reaction> Reactions => Set<Reaction>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<ApprovalPolicy> ApprovalPolicies => Set<ApprovalPolicy>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Survey> Surveys => Set<Survey>();
    public DbSet<SurveyQuestion> SurveyQuestions => Set<SurveyQuestion>();
    public DbSet<SurveyResponse> SurveyResponses => Set<SurveyResponse>();
    public DbSet<SurveyAnswer> SurveyAnswers => Set<SurveyAnswer>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ComplianceSetting> ComplianceSettings => Set<ComplianceSetting>();
    public DbSet<UserCustomField> UserCustomFields => Set<UserCustomField>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<PointPool> PointPools => Set<PointPool>();
    public DbSet<ApprovalHistory> ApprovalHistories => Set<ApprovalHistory>();
    public DbSet<AutomationRule> AutomationRules => Set<AutomationRule>();
    public DbSet<MilestoneEvent> MilestoneEvents => Set<MilestoneEvent>();
    public DbSet<MemoryBook> MemoryBooks => Set<MemoryBook>();
    public DbSet<MemoryBookEntry> MemoryBookEntries => Set<MemoryBookEntry>();
    public DbSet<ShippingAddress> ShippingAddresses => Set<ShippingAddress>();
    public DbSet<RedemptionHistory> RedemptionHistories => Set<RedemptionHistory>();
    public DbSet<RecognitionAttachment> RecognitionAttachments => Set<RecognitionAttachment>();
    public DbSet<Integration> Integrations => Set<Integration>();
    public DbSet<SyncLog> SyncLogs => Set<SyncLog>();
    public DbSet<SyncMapping> SyncMappings => Set<SyncMapping>();
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<UserBadge> UserBadges => Set<UserBadge>();
    public DbSet<Leaderboard> Leaderboards => Set<Leaderboard>();
    public DbSet<AchievementRule> AchievementRules => Set<AchievementRule>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<AnalyticsCache> AnalyticsCaches => Set<AnalyticsCache>();
    public DbSet<TenantBilling> TenantBillings => Set<TenantBilling>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);

        // Global soft delete query filters
        foreach (var entityType in model.Model.GetEntityTypes()
                     .Where(entityType => typeof(BaseEntity).IsAssignableFrom(entityType.ClrType)))
        {
            var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
            var deletedAt = System.Linq.Expressions.Expression.Property(parameter, nameof(BaseEntity.DeletedAt));
            var nullConstant = System.Linq.Expressions.Expression.Constant(null, typeof(DateTime?));
            var body = System.Linq.Expressions.Expression.Equal(deletedAt, nullConstant);
            var lambda = System.Linq.Expressions.Expression.Lambda(body, parameter);
            entityType.SetQueryFilter(lambda);
        }


        // UserRole composite PK
        model.Entity<UserRole>().HasKey(ur => new { ur.UserId, ur.RoleId, ur.TenantId });

        // Tenant
        model.Entity<Tenant>(e => {
            e.HasIndex(t => t.Slug).IsUnique();
        });

        // User
        model.Entity<User>(e => {
            e.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
            e.HasOne(u => u.Manager).WithMany(u => u.DirectReports)
                .HasForeignKey(u => u.ManagerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(u => u.Department).WithMany(d => d.Users)
                .HasForeignKey(u => u.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        });

        // Recognition — restrict deletes to avoid cascade issues
        model.Entity<Recognition>(e => {
            e.HasOne(r => r.Sender).WithMany(u => u.SentRecognitions)
                .HasForeignKey(r => r.SenderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Receiver).WithMany(u => u.ReceivedRecognitions)
                .HasForeignKey(r => r.ReceiverId).OnDelete(DeleteBehavior.Restrict);
        });

        // Wallet
        model.Entity<Wallet>(e => {
            e.HasIndex(w => new { w.UserId, w.WalletType }).IsUnique();
        });

        // Transaction
        model.Entity<Transaction>(e => {
            e.HasOne(t => t.FromWallet).WithMany(w => w.SentTransactions)
                .HasForeignKey(t => t.FromWalletId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.ToWallet).WithMany(w => w.ReceivedTransactions)
                .HasForeignKey(t => t.ToWalletId).OnDelete(DeleteBehavior.Restrict);
        });

        // Department self-reference
        model.Entity<Department>(e => {
            e.HasOne(d => d.ParentDepartment).WithMany(d => d.Children)
                .HasForeignKey(d => d.ParentDepartmentId).OnDelete(DeleteBehavior.Restrict);
        });

        // Indexes for tenant_id on high-volume tables
        model.Entity<ComplianceSetting>().HasIndex(c => c.TenantId).IsUnique();
        model.Entity<UserCustomField>().HasIndex(f => new { f.TenantId, f.UserId, f.FieldKey }).IsUnique();
        model.Entity<UserPermission>().HasIndex(p => new { p.TenantId, p.UserId, p.PermissionKey }).IsUnique();
        model.Entity<PointPool>().HasIndex(p => new { p.TenantId, p.Name }).IsUnique();
        model.Entity<ApprovalHistory>().HasIndex(h => new { h.TenantId, h.ApprovalRequestId });
        model.Entity<AutomationRule>().HasIndex(r => new { r.TenantId, r.TriggerType, r.IsActive });
        model.Entity<MilestoneEvent>().HasIndex(e => new { e.TenantId, e.EventType, e.Status });
        model.Entity<MemoryBook>().HasIndex(b => new { b.TenantId, b.RecipientId, b.Status });
        model.Entity<ShippingAddress>().HasIndex(a => new { a.TenantId, a.UserId });
        model.Entity<RedemptionHistory>().HasIndex(r => new { r.TenantId, r.UserId });
        model.Entity<RecognitionAttachment>().HasIndex(a => new { a.TenantId, a.RecognitionId });
        model.Entity<Integration>().HasIndex(i => new { i.TenantId, i.Provider, i.IntegrationType }).IsUnique();
        model.Entity<SyncLog>().HasIndex(l => new { l.TenantId, l.IntegrationId, l.StartedAt });
        model.Entity<SyncMapping>().HasIndex(m => new { m.TenantId, m.IntegrationId, m.TargetField }).IsUnique();
        model.Entity<Badge>().HasIndex(b => new { b.TenantId, b.Name }).IsUnique();
        model.Entity<UserBadge>().HasIndex(b => new { b.TenantId, b.UserId, b.BadgeId }).IsUnique();
        model.Entity<Leaderboard>().HasIndex(l => new { l.TenantId, l.Period, l.Metric });
        model.Entity<AchievementRule>().HasIndex(r => new { r.TenantId, r.TriggerMetric, r.Threshold });
        model.Entity<Report>().HasIndex(r => new { r.TenantId, r.CreatedByUserId });
        model.Entity<AnalyticsCache>().HasIndex(c => new { c.TenantId, c.CacheKey }).IsUnique();
        model.Entity<TenantBilling>().HasIndex(b => b.TenantId).IsUnique();
        model.Entity<FeatureFlag>().HasIndex(f => new { f.TenantId, f.Key }).IsUnique();
        model.Entity<Recognition>().HasIndex(r => r.TenantId);
        model.Entity<Transaction>().HasIndex(t => t.TenantId);
        model.Entity<AuditLog>().HasIndex(a => a.TenantId);
        model.Entity<Notification>().HasIndex(n => new { n.UserId, n.ReadAt });

        // Seed default roles
        model.Entity<Role>().HasData(
            new Role { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Name = "SystemAdmin", NormalizedName = "SYSTEMADMIN", Description = "Platform-level superuser", CreatedAt = DateTime.UtcNow },
            new Role { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), Name = "TenantAdmin", NormalizedName = "TENANTADMIN", Description = "HR Admin for a tenant", CreatedAt = DateTime.UtcNow },
            new Role { Id = Guid.Parse("00000000-0000-0000-0000-000000000003"), Name = "Manager", NormalizedName = "MANAGER", Description = "Team manager", CreatedAt = DateTime.UtcNow },
            new Role { Id = Guid.Parse("00000000-0000-0000-0000-000000000004"), Name = "Employee", NormalizedName = "EMPLOYEE", Description = "Regular employee", CreatedAt = DateTime.UtcNow },
            new Role { Id = Guid.Parse("00000000-0000-0000-0000-000000000005"), Name = "Viewer", NormalizedName = "VIEWER", Description = "Read-only auditor", CreatedAt = DateTime.UtcNow }
        );
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
