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

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);

        // Global soft delete query filters
        model.Entity<Tenant>().HasQueryFilter(e => e.DeletedAt == null);
        model.Entity<User>().HasQueryFilter(e => e.DeletedAt == null);
        model.Entity<Department>().HasQueryFilter(e => e.DeletedAt == null);
        model.Entity<CompanyValue>().HasQueryFilter(e => e.DeletedAt == null);
        model.Entity<Recognition>().HasQueryFilter(e => e.DeletedAt == null);
        model.Entity<Comment>().HasQueryFilter(e => e.DeletedAt == null);
        model.Entity<Product>().HasQueryFilter(e => e.DeletedAt == null);
        model.Entity<Survey>().HasQueryFilter(e => e.DeletedAt == null);

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
        model.Entity<Recognition>().HasIndex(r => r.TenantId);
        model.Entity<Transaction>().HasIndex(t => t.TenantId);
        model.Entity<AuditLog>().HasIndex(a => a.TenantId);
        model.Entity<Notification>().HasIndex(n => new { n.UserId, n.ReadAt });

        // Seed default roles
        model.Entity<Role>().HasData(
            new Role { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Name = "SystemAdmin", NormalizedName = "SYSTEMADMIN", Description = "Platform-level superuser", CreatedAt = DateTime.UtcNow },
            new Role { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), Name = "TenantAdmin", NormalizedName = "TENANTADMIN", Description = "HR Admin for a tenant", CreatedAt = DateTime.UtcNow },
            new Role { Id = Guid.Parse("00000000-0000-0000-0000-000000000003"), Name = "Manager", NormalizedName = "MANAGER", Description = "Team manager", CreatedAt = DateTime.UtcNow },
            new Role { Id = Guid.Parse("00000000-0000-0000-0000-000000000004"), Name = "Employee", NormalizedName = "EMPLOYEE", Description = "Regular employee", CreatedAt = DateTime.UtcNow }
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
