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
        TempData["Success"] = $"Empresa {(tenant.Status == "active" ? "ativada" : "suspensa")} com sucesso.";
        return RedirectToAction("Index");
    }

    [HttpPost("tenants/{id:guid}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTenant(
        Guid id,
        string name,
        string? logoUrl,
        string? primaryColor,
        string? accentColor,
        string? billingPlan)
    {
        var tenant = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
        if (tenant == null) return NotFound();

        tenant.Name = name.Trim();
        tenant.LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim();
        tenant.PrimaryColor = string.IsNullOrWhiteSpace(primaryColor) ? tenant.PrimaryColor : primaryColor.Trim();
        tenant.AccentColor = string.IsNullOrWhiteSpace(accentColor) ? tenant.AccentColor : accentColor.Trim();
        if (!string.IsNullOrWhiteSpace(billingPlan))
            tenant.BillingPlan = billingPlan.Trim();

        await db.SaveChangesAsync();
        TempData["Success"] = $"Empresa \"{tenant.Name}\" atualizada com sucesso.";
        return RedirectToAction("Index");
    }

    [HttpPost("users/{id:guid}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUserStatus(Guid id)
    {
        var user = await db.Users.IgnoreQueryFilters()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        if (user.UserRoles.Any(ur => ur.Role.NormalizedName == "SYSTEMADMIN"))
        {
            TempData["Error"] = "Não é possível alterar o status de um System Admin.";
            return RedirectToAction("Index");
        }

        user.Status = user.Status == "active" ? "inactive" : "active";
        await db.SaveChangesAsync();
        TempData["Success"] = $"Acesso de {user.FullName} {(user.Status == "active" ? "ativado" : "desativado")}.";
        return RedirectToAction("Index");
    }

    [HttpPost("users/{id:guid}/reset-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(Guid id)
    {
        var user = await db.Users.IgnoreQueryFilters()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        if (user.UserRoles.Any(ur => ur.Role.NormalizedName == "SYSTEMADMIN"))
        {
            TempData["Error"] = "Não é possível redefinir a senha de um System Admin por aqui.";
            return RedirectToAction("Index");
        }

        var newPassword = $"Priser@{Guid.NewGuid().ToString()[..8]}";
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await db.SaveChangesAsync();
        TempData["Success"] = $"Nova senha de {user.FullName}: {newPassword}";
        return RedirectToAction("Index");
    }

    [HttpPost("users/{id:guid}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(Guid id, string firstName, string lastName, string email)
    {
        var user = await db.Users.IgnoreQueryFilters()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        if (user.UserRoles.Any(ur => ur.Role.NormalizedName == "SYSTEMADMIN"))
        {
            TempData["Error"] = "Não é possível editar um System Admin por aqui.";
            return RedirectToAction("Index");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (normalizedEmail != user.Email && await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == normalizedEmail && u.Id != id))
        {
            TempData["Error"] = "Já existe um usuário com este email.";
            return RedirectToAction("Index");
        }

        user.FirstName = firstName.Trim();
        user.LastName = lastName.Trim();
        user.Email = normalizedEmail;
        await db.SaveChangesAsync();
        TempData["Success"] = $"Usuário {user.FullName} atualizado com sucesso.";
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
