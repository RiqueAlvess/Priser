using System.Text;
using Microsoft.EntityFrameworkCore;
using Priser.Data;
using Priser.Models.Entities;

namespace Priser.Services;

public class PlatformBootstrapService(AppDbContext db, EnterpriseSchemaService enterpriseSchema, WalletService wallets)
{
    private const string PlatformTenantSlug = "platform";
    private const string BootstrapAdminEmail = "admin@admin.com";
    private const string BootstrapAdminPasswordBase64 = "YWRtaW4=";

    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
        await enterpriseSchema.EnsureAsync(cancellationToken);
        await EnsureRolesAsync(cancellationToken);
        await EnsureSystemAdminAsync(cancellationToken);
    }

    private async Task EnsureRolesAsync(CancellationToken cancellationToken)
    {
        var roles = new[]
        {
            new RoleSeed("00000000-0000-0000-0000-000000000001", "SystemAdmin", "SYSTEMADMIN", "Platform-level superuser"),
            new RoleSeed("00000000-0000-0000-0000-000000000002", "TenantAdmin", "TENANTADMIN", "HR Admin for a tenant"),
            new RoleSeed("00000000-0000-0000-0000-000000000003", "Manager", "MANAGER", "Team manager"),
            new RoleSeed("00000000-0000-0000-0000-000000000004", "Employee", "EMPLOYEE", "Regular employee"),
            new RoleSeed("00000000-0000-0000-0000-000000000005", "Viewer", "VIEWER", "Read-only auditor")
        };

        foreach (var seed in roles)
        {
            var id = Guid.Parse(seed.Id);
            var role = await db.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (role == null)
            {
                db.Roles.Add(new Role
                {
                    Id = id,
                    Name = seed.Name,
                    NormalizedName = seed.NormalizedName,
                    Description = seed.Description
                });
            }
            else
            {
                role.Name = seed.Name;
                role.NormalizedName = seed.NormalizedName;
                role.Description = seed.Description;
                role.DeletedAt = null;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureSystemAdminAsync(CancellationToken cancellationToken)
    {
        var platformTenant = await db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == PlatformTenantSlug, cancellationToken);

        if (platformTenant == null)
        {
            platformTenant = new Tenant
            {
                Name = "Priser Platform",
                Slug = PlatformTenantSlug,
                BillingPlan = "internal",
                Status = "active",
                PrimaryColor = "#0b1b3d",
                AccentColor = "#00A3E0"
            };
            db.Tenants.Add(platformTenant);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            platformTenant.Status = "active";
            platformTenant.DeletedAt = null;
        }

        var systemAdminRole = await db.Roles.FirstAsync(r => r.NormalizedName == "SYSTEMADMIN", cancellationToken);
        var password = Encoding.UTF8.GetString(Convert.FromBase64String(BootstrapAdminPasswordBase64));
        var admin = await db.Users.IgnoreQueryFilters()
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Email == BootstrapAdminEmail, cancellationToken);

        if (admin == null)
        {
            admin = new User
            {
                TenantId = platformTenant.Id,
                Email = BootstrapAdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                FirstName = "System",
                LastName = "Admin",
                Status = "active"
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            admin.TenantId = platformTenant.Id;
            admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            admin.FirstName = string.IsNullOrWhiteSpace(admin.FirstName) ? "System" : admin.FirstName;
            admin.LastName = string.IsNullOrWhiteSpace(admin.LastName) ? "Admin" : admin.LastName;
            admin.Status = "active";
            admin.DeletedAt = null;
        }

        var hasRole = admin.UserRoles.Any(ur => ur.RoleId == systemAdminRole.Id && ur.TenantId == platformTenant.Id);
        if (!hasRole)
        {
            db.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = systemAdminRole.Id, TenantId = platformTenant.Id });
        }

        await db.SaveChangesAsync(cancellationToken);
        await wallets.GetOrCreateWalletAsync(admin.Id, platformTenant.Id, "earned");
        await wallets.GetOrCreateWalletAsync(admin.Id, platformTenant.Id, "allowance");
    }

    private sealed record RoleSeed(string Id, string Name, string NormalizedName, string Description);
}
