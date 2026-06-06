using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Priser.Data;
using Priser.Models.Entities;
using Priser.Services;

namespace Priser.Controllers;

[Authorize(Roles = "SystemAdmin")]
[Route("system")]
public class SystemController(AppDbContext db, WalletService wallets) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewBag.Tenants = await db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Slug != "platform")
            .OrderBy(t => t.Name)
            .ToListAsync();

        ViewBag.TenantAdmins = await db.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.UserRoles.Any(ur => ur.Role.NormalizedName == "TENANTADMIN"))
            .OrderBy(u => u.Tenant.Name)
            .ThenBy(u => u.FirstName)
            .ToListAsync();

        return View();
    }

    [HttpPost("tenants/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTenant(
        string name,
        string? slug,
        string? logoUrl,
        string? primaryColor,
        string? accentColor,
        string adminEmail,
        string adminFirstName,
        string adminLastName,
        string? adminPassword)
    {
        slug = NormalizeSlug(string.IsNullOrWhiteSpace(slug) ? name : slug);
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Slug == slug))
        {
            TempData["Error"] = "Já existe uma empresa com este slug.";
            return RedirectToAction("Index");
        }

        adminEmail = adminEmail.Trim().ToLowerInvariant();
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == adminEmail))
        {
            TempData["Error"] = "Já existe um usuário com este email.";
            return RedirectToAction("Index");
        }

        var password = string.IsNullOrWhiteSpace(adminPassword)
            ? $"Priser@{Guid.NewGuid().ToString()[..8]}"
            : adminPassword;

        using var tx = await db.Database.BeginTransactionAsync();

        var tenant = new Tenant
        {
            Name = name.Trim(),
            Slug = slug,
            LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim(),
            BillingPlan = "trial",
            Status = "active",
            PrimaryColor = string.IsNullOrWhiteSpace(primaryColor) ? "#0b1b3d" : primaryColor.Trim(),
            AccentColor = string.IsNullOrWhiteSpace(accentColor) ? "#00A3E0" : accentColor.Trim()
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var tenantAdminRole = await db.Roles.FirstAsync(r => r.NormalizedName == "TENANTADMIN");
        var admin = new User
        {
            TenantId = tenant.Id,
            Email = adminEmail,
            FirstName = adminFirstName.Trim(),
            LastName = adminLastName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Status = "active"
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        db.UserRoles.Add(new UserRole { TenantId = tenant.Id, UserId = admin.Id, RoleId = tenantAdminRole.Id });
        SeedTenantDefaults(tenant.Id);
        await db.SaveChangesAsync();

        await wallets.GetOrCreateWalletAsync(admin.Id, tenant.Id, "earned");
        await wallets.GetOrCreateWalletAsync(admin.Id, tenant.Id, "allowance");
        await tx.CommitAsync();

        TempData["Success"] = $"Empresa criada. Tenant Admin: {adminEmail}. Senha: {password}";
        return RedirectToAction("Index");
    }

    [HttpPost("tenants/{id:guid}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleTenantStatus(Guid id)
    {
        var tenant = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
        if (tenant == null) return NotFound();

        tenant.Status = tenant.Status == "active" ? "suspended" : "active";
        tenant.DeletedAt = null;
        await db.SaveChangesAsync();
        return RedirectToAction("Index");
    }

    private void SeedTenantDefaults(Guid tenantId)
    {
        db.CompanyValues.AddRange(
            new CompanyValue { TenantId = tenantId, Name = "Inovação", Icon = "lightbulb", Color = "#00658d", DisplayOrder = 1 },
            new CompanyValue { TenantId = tenantId, Name = "Colaboração", Icon = "group", Color = "#567bff", DisplayOrder = 2 },
            new CompanyValue { TenantId = tenantId, Name = "Excelência", Icon = "star", Color = "#0039b5", DisplayOrder = 3 },
            new CompanyValue { TenantId = tenantId, Name = "Respeito", Icon = "handshake", Color = "#004b69", DisplayOrder = 4 }
        );

        db.ApprovalPolicies.Add(new ApprovalPolicy
        {
            TenantId = tenantId,
            Name = "Política Padrão",
            ThresholdPoints = 100,
            RequiresApproval = true,
            IsActive = true
        });
    }

    private static string NormalizeSlug(string value)
    {
        var slug = value.Trim().ToLowerInvariant().Replace(" ", "-").Replace("_", "-");
        return string.Join('-', slug.Split('-', StringSplitOptions.RemoveEmptyEntries));
    }
}
