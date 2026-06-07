using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Priser.Data;
using Priser.Models.Entities;
using Priser.Services;
using System.Text;

namespace Priser.Controllers;

[Authorize(Roles = "SystemAdmin")]
[Route("system")]
public class SystemController(AppDbContext db, WalletService wallets) : Controller
{
    // ─── Dashboard ───────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var tenants = await db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Slug != "platform")
            .ToListAsync();

        var allUsers = await db.Users
            .IgnoreQueryFilters()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.Tenant.Slug != "platform")
            .ToListAsync();

        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        var recognitions = await db.Recognitions
            .IgnoreQueryFilters()
            .Where(r => r.CreatedAt >= sevenDaysAgo)
            .Select(r => new { r.CreatedAt })
            .ToListAsync();

        var recentAudit = await db.AuditLogs
            .IgnoreQueryFilters()
            .OrderByDescending(a => a.CreatedAt)
            .Take(20)
            .ToListAsync();

        // KPI: MRR baseado nos planos
        var planPrices = new Dictionary<string, decimal>
        {
            { "trial", 0m }, { "starter", 99m }, { "growth", 299m },
            { "business", 799m }, { "enterprise", 1999m }
        };
        var activeTenants = tenants.Where(t => t.Status == "active").ToList();
        var mrr = activeTenants.Sum(t =>
            planPrices.TryGetValue(t.BillingPlan.ToLower(), out var p) ? p : 0m);

        var totalTransactionPoints = await db.Transactions
            .IgnoreQueryFilters()
            .Where(tx => tx.TransactionType == "earned" || tx.TransactionType == "recognition")
            .SumAsync(tx => (decimal?)tx.Amount) ?? 0;

        // Reconhecimentos por dia (últimos 7 dias)
        var recByDay = Enumerable.Range(0, 7)
            .Select(i => DateTime.UtcNow.Date.AddDays(-6 + i))
            .Select(day => new
            {
                Date = day,
                Count = recognitions.Count(r => r.CreatedAt.Date == day)
            })
            .ToList();

        // Tenants em risco
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var tenantRecCounts = await db.Recognitions
            .IgnoreQueryFilters()
            .Where(r => r.CreatedAt >= thirtyDaysAgo)
            .GroupBy(r => r.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync();

        var tenantsAtRisk = activeTenants
            .Select(t =>
            {
                var recCount = tenantRecCounts.FirstOrDefault(r => r.TenantId == t.Id)?.Count ?? 0;
                var userCount = allUsers.Count(u => u.TenantId == t.Id);
                string? problem = null;
                string? risk = null;
                if (recCount == 0) { problem = "Sem reconhecimentos no mês"; risk = "alto"; }
                else if (recCount < 5) { problem = "Poucos reconhecimentos"; risk = "médio"; }
                return new { Tenant = t, UserCount = userCount, RecCount = recCount, Problem = problem, Risk = risk };
            })
            .Where(x => x.Problem != null)
            .Take(8)
            .ToList();

        ViewBag.TotalTenants = activeTenants.Count;
        ViewBag.SuspendedTenants = tenants.Count(t => t.Status != "active");
        ViewBag.TotalUsers = allUsers.Count(u => u.Status == "active");
        ViewBag.MRR = mrr;
        ViewBag.ARR = mrr * 12;
        ViewBag.TotalPoints = totalTransactionPoints;
        ViewBag.RecByDay = recByDay;
        ViewBag.TenantsAtRisk = tenantsAtRisk;
        ViewBag.RecentAudit = recentAudit;

        return View();
    }

    // ─── Tenants ─────────────────────────────────────────────────────────────

    [HttpGet("tenants")]
    public async Task<IActionResult> Tenants(string? plan, string? status, string? q)
    {
        var query = db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Slug != "platform")
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(plan))
            query = query.Where(t => t.BillingPlan == plan);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(t => t.Status == status);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(t => t.Name.Contains(q) || t.Slug.Contains(q));

        var tenants = await query.OrderBy(t => t.Name).ToListAsync();

        var tenantIds = tenants.Select(t => t.Id).ToList();

        var userCounts = await db.Users
            .IgnoreQueryFilters()
            .Where(u => tenantIds.Contains(u.TenantId) && u.Status == "active")
            .GroupBy(u => u.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync();

        var tenantAdmins = await db.Users
            .IgnoreQueryFilters()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => tenantIds.Contains(u.TenantId) && u.UserRoles.Any(ur => ur.Role.NormalizedName == "TENANTADMIN"))
            .ToListAsync();

        var walletPoints = await db.Wallets
            .IgnoreQueryFilters()
            .Where(w => tenantIds.Contains(w.TenantId))
            .GroupBy(w => w.TenantId)
            .Select(g => new { TenantId = g.Key, Total = g.Sum(w => w.Balance) })
            .ToListAsync();

        ViewBag.Tenants = tenants;
        ViewBag.UserCounts = userCounts;
        ViewBag.TenantAdmins = tenantAdmins;
        ViewBag.WalletPoints = walletPoints;
        ViewBag.FilterPlan = plan;
        ViewBag.FilterStatus = status;
        ViewBag.FilterQ = q;

        return View();
    }

    [HttpGet("tenants/{id:guid}")]
    public async Task<IActionResult> TenantDetail(Guid id)
    {
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id);
        if (tenant == null) return NotFound();

        var users = await db.Users
            .IgnoreQueryFilters()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.TenantId == id)
            .OrderBy(u => u.FirstName)
            .ToListAsync();

        var recentRecs = await db.Recognitions
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == id)
            .OrderByDescending(r => r.CreatedAt)
            .Take(10)
            .ToListAsync();

        var auditLogs = await db.AuditLogs
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == id)
            .OrderByDescending(a => a.CreatedAt)
            .Take(20)
            .ToListAsync();

        ViewBag.Tenant = tenant;
        ViewBag.Users = users;
        ViewBag.RecentRecs = recentRecs;
        ViewBag.AuditLogs = auditLogs;

        return View();
    }

    // ─── Planos ──────────────────────────────────────────────────────────────

    [HttpGet("plans")]
    public async Task<IActionResult> Plans()
    {
        var planCounts = await db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Slug != "platform")
            .GroupBy(t => t.BillingPlan)
            .Select(g => new { Plan = g.Key, Count = g.Count() })
            .ToListAsync();

        ViewBag.PlanCounts = planCounts;
        return View();
    }

    // ─── Cobrança ────────────────────────────────────────────────────────────

    [HttpGet("billing")]
    public async Task<IActionResult> Billing()
    {
        var tenants = await db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Slug != "platform" && t.Status == "active")
            .ToListAsync();

        var planPrices = new Dictionary<string, decimal>
        {
            { "trial", 0m }, { "starter", 99m }, { "growth", 299m },
            { "business", 799m }, { "enterprise", 1999m }
        };

        var mrr = tenants.Sum(t => planPrices.TryGetValue(t.BillingPlan.ToLower(), out var p) ? p : 0m);

        var recentTransactions = await db.Transactions
            .IgnoreQueryFilters()
            .Include(tx => tx.ToWallet)
            .OrderByDescending(tx => tx.CreatedAt)
            .Take(50)
            .ToListAsync();

        var tenantList = await db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Slug != "platform")
            .OrderBy(t => t.Name)
            .ToListAsync();

        ViewBag.MRR = mrr;
        ViewBag.ARR = mrr * 12;
        ViewBag.Tenants = tenantList;
        ViewBag.Transactions = recentTransactions;
        ViewBag.TenantsByPlan = tenants.GroupBy(t => t.BillingPlan)
            .Select(g => new { Plan = g.Key, Count = g.Count(), Revenue = g.Sum(t => planPrices.TryGetValue(t.BillingPlan.ToLower(), out var p) ? p : 0m) })
            .OrderByDescending(x => x.Revenue)
            .ToList();

        return View();
    }

    [HttpPost("billing/emit-credits")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmitCredits(Guid tenantId, Guid userId, decimal amount, string reason)
    {
        var wallet = await wallets.GetOrCreateWalletAsync(userId, tenantId, "allowance");
        wallet.Balance += amount;

        db.Transactions.Add(new Transaction
        {
            TenantId = tenantId,
            ToWalletId = wallet.Id,
            Amount = amount,
            TransactionType = "manual_credit",
            Description = $"[SysAdmin] {reason}",
            Status = "completed"
        });

        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = GetCurrentUserId(),
            Action = "manual_credit_emit",
            EntityType = "Wallet",
            EntityId = wallet.Id,
            NewValue = $"amount={amount}, reason={reason}",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        await db.SaveChangesAsync();
        TempData["Success"] = $"{amount:N0} créditos emitidos com sucesso. Motivo: {reason}";
        return RedirectToAction("Billing");
    }

    // ─── Catálogo Global ─────────────────────────────────────────────────────

    [HttpGet("catalog")]
    public IActionResult Catalog()
    {
        return View();
    }

    // ─── Integrações ─────────────────────────────────────────────────────────

    [HttpGet("integrations")]
    public IActionResult Integrations()
    {
        return View();
    }

    // ─── Comunicações ────────────────────────────────────────────────────────

    [HttpGet("communications")]
    public IActionResult Communications()
    {
        return View();
    }

    // ─── Suporte ─────────────────────────────────────────────────────────────

    [HttpGet("support")]
    public async Task<IActionResult> Support()
    {
        var tenantAdmins = await db.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.Tenant.Slug != "platform" && u.UserRoles.Any(ur => ur.Role.NormalizedName == "TENANTADMIN") && u.Status == "active")
            .OrderBy(u => u.Tenant.Name)
            .ToListAsync();

        var recentImpersonations = await db.AuditLogs
            .IgnoreQueryFilters()
            .Where(a => a.Action == "impersonate")
            .OrderByDescending(a => a.CreatedAt)
            .Take(20)
            .ToListAsync();

        ViewBag.TenantAdmins = tenantAdmins;
        ViewBag.RecentImpersonations = recentImpersonations;
        return View();
    }

    [HttpPost("support/impersonate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Impersonate(Guid userId, string reason)
    {
        var user = await db.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return NotFound();

        db.AuditLogs.Add(new AuditLog
        {
            TenantId = user.TenantId,
            UserId = GetCurrentUserId(),
            Action = "impersonate",
            EntityType = "User",
            EntityId = userId,
            NewValue = $"target={user.Email}, reason={reason}",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await db.SaveChangesAsync();

        TempData["Info"] = $"Impersonação de {user.FullName} ({user.Tenant?.Name}) registrada. Motivo: {reason}. (Funcionalidade de sessão em desenvolvimento)";
        return RedirectToAction("Support");
    }

    // ─── Auditoria ───────────────────────────────────────────────────────────

    [HttpGet("audit")]
    public async Task<IActionResult> Audit(Guid? tenantId, string? action, DateTime? from, DateTime? to, int page = 1)
    {
        const int pageSize = 50;
        var query = db.AuditLogs.IgnoreQueryFilters().AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(a => a.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action.Contains(action));
        if (from.HasValue)
            query = query.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(a => a.CreatedAt <= to.Value.AddDays(1));

        var total = await query.CountAsync();
        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var tenants = await db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Slug != "platform")
            .OrderBy(t => t.Name)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync();

        ViewBag.Logs = logs;
        ViewBag.Total = total;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
        ViewBag.Tenants = tenants;
        ViewBag.FilterTenantId = tenantId;
        ViewBag.FilterAction = action;
        ViewBag.FilterFrom = from?.ToString("yyyy-MM-dd");
        ViewBag.FilterTo = to?.ToString("yyyy-MM-dd");

        return View();
    }

    [HttpGet("audit/export")]
    public async Task<IActionResult> ExportAudit(Guid? tenantId, string? action, DateTime? from, DateTime? to)
    {
        var query = db.AuditLogs.IgnoreQueryFilters().AsQueryable();

        if (tenantId.HasValue) query = query.Where(a => a.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => a.Action.Contains(action));
        if (from.HasValue) query = query.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(a => a.CreatedAt <= to.Value.AddDays(1));

        var logs = await query.OrderByDescending(a => a.CreatedAt).Take(5000).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Data,Tenant,Usuario,Acao,Entidade,EntityId,IP");
        foreach (var log in logs)
        {
            sb.AppendLine($"{log.CreatedAt:yyyy-MM-dd HH:mm:ss},{log.TenantId},{log.UserId},{log.Action},{log.EntityType},{log.EntityId},{log.IpAddress}");
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"audit_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    // ─── Saúde do Sistema ────────────────────────────────────────────────────

    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        var dbOk = false;
        try { await db.Database.CanConnectAsync(); dbOk = true; } catch { }

        var recentErrors = await db.AuditLogs
            .IgnoreQueryFilters()
            .Where(a => a.Action.Contains("error") || a.Action.Contains("fail"))
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .ToListAsync();

        var totalUsers = await db.Users.IgnoreQueryFilters().CountAsync();
        var totalTenants = await db.Tenants.IgnoreQueryFilters().CountAsync();
        var totalRecs = await db.Recognitions.IgnoreQueryFilters().CountAsync();
        var totalTransactions = await db.Transactions.IgnoreQueryFilters().CountAsync();

        ViewBag.DbOk = dbOk;
        ViewBag.RecentErrors = recentErrors;
        ViewBag.TotalUsers = totalUsers;
        ViewBag.TotalTenants = totalTenants;
        ViewBag.TotalRecs = totalRecs;
        ViewBag.TotalTransactions = totalTransactions;
        ViewBag.ServerTime = DateTime.UtcNow;

        return View();
    }

    // ─── Configurações ───────────────────────────────────────────────────────

    [HttpGet("settings")]
    public async Task<IActionResult> Settings()
    {
        var sysAdmins = await db.Users
            .IgnoreQueryFilters()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.UserRoles.Any(ur => ur.Role.NormalizedName == "SYSTEMADMIN"))
            .ToListAsync();

        ViewBag.SysAdmins = sysAdmins;
        return View();
    }

    [HttpPost("settings/sysadmin/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSysAdmin(string firstName, string lastName, string email, string? password)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == normalizedEmail))
        {
            TempData["Error"] = "Já existe um usuário com este email.";
            return RedirectToAction("Settings");
        }

        var platformTenant = await db.Tenants.IgnoreQueryFilters().FirstAsync(t => t.Slug == "platform");
        var sysAdminRole = await db.Roles.FirstAsync(r => r.NormalizedName == "SYSTEMADMIN");
        var pass = string.IsNullOrWhiteSpace(password) ? $"SysAdmin@{Guid.NewGuid().ToString()[..8]}" : password;

        var user = new User
        {
            TenantId = platformTenant.Id,
            Email = normalizedEmail,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(pass),
            Status = "active"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserRoles.Add(new UserRole { TenantId = platformTenant.Id, UserId = user.Id, RoleId = sysAdminRole.Id });

        db.AuditLogs.Add(new AuditLog
        {
            UserId = GetCurrentUserId(),
            Action = "create_sysadmin",
            EntityType = "User",
            EntityId = user.Id,
            NewValue = $"email={normalizedEmail}",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        await db.SaveChangesAsync();
        TempData["Success"] = $"SysAdmin criado: {normalizedEmail}. Senha: {pass}";
        return RedirectToAction("Settings");
    }

    // ─── Existing Tenant POST actions ────────────────────────────────────────

    [HttpPost("tenants/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTenant(
        string name, string? slug, string? logoUrl, string? primaryColor,
        string? accentColor, string adminEmail, string adminFirstName,
        string adminLastName, string? adminPassword, string? billingPlan)
    {
        slug = NormalizeSlug(string.IsNullOrWhiteSpace(slug) ? name : slug);
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Slug == slug))
        {
            TempData["Error"] = "Já existe uma empresa com este slug.";
            return RedirectToAction("Tenants");
        }

        adminEmail = adminEmail.Trim().ToLowerInvariant();
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == adminEmail))
        {
            TempData["Error"] = "Já existe um usuário com este email.";
            return RedirectToAction("Tenants");
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
            BillingPlan = string.IsNullOrWhiteSpace(billingPlan) ? "trial" : billingPlan,
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

        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenant.Id,
            UserId = GetCurrentUserId(),
            Action = "create_tenant",
            EntityType = "Tenant",
            EntityId = tenant.Id,
            NewValue = $"name={name}, plan={tenant.BillingPlan}",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        await db.SaveChangesAsync();
        await wallets.GetOrCreateWalletAsync(admin.Id, tenant.Id, "earned");
        await wallets.GetOrCreateWalletAsync(admin.Id, tenant.Id, "allowance");
        await tx.CommitAsync();

        TempData["Success"] = $"Empresa criada. Tenant Admin: {adminEmail}. Senha: {password}";
        return RedirectToAction("Tenants");
    }

    [HttpPost("tenants/{id:guid}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleTenantStatus(Guid id)
    {
        var tenant = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
        if (tenant == null) return NotFound();

        tenant.Status = tenant.Status == "active" ? "suspended" : "active";
        tenant.DeletedAt = null;

        db.AuditLogs.Add(new AuditLog
        {
            TenantId = id,
            UserId = GetCurrentUserId(),
            Action = tenant.Status == "active" ? "tenant_activated" : "tenant_suspended",
            EntityType = "Tenant",
            EntityId = id,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        await db.SaveChangesAsync();
        TempData["Success"] = $"Empresa {(tenant.Status == "active" ? "ativada" : "suspensa")} com sucesso.";
        return RedirectToAction("Tenants");
    }

    [HttpPost("tenants/{id:guid}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTenant(
        Guid id, string name, string? logoUrl, string? primaryColor,
        string? accentColor, string? billingPlan, string? timezone, string? language)
    {
        var tenant = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
        if (tenant == null) return NotFound();

        tenant.Name = name.Trim();
        tenant.LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim();
        tenant.PrimaryColor = string.IsNullOrWhiteSpace(primaryColor) ? tenant.PrimaryColor : primaryColor.Trim();
        tenant.AccentColor = string.IsNullOrWhiteSpace(accentColor) ? tenant.AccentColor : accentColor.Trim();
        if (!string.IsNullOrWhiteSpace(billingPlan)) tenant.BillingPlan = billingPlan.Trim();

        db.AuditLogs.Add(new AuditLog
        {
            TenantId = id,
            UserId = GetCurrentUserId(),
            Action = "edit_tenant",
            EntityType = "Tenant",
            EntityId = id,
            NewValue = $"name={name}, plan={tenant.BillingPlan}",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        await db.SaveChangesAsync();
        TempData["Success"] = $"Empresa \"{tenant.Name}\" atualizada com sucesso.";
        return RedirectToAction("Tenants");
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
            return RedirectToAction("Tenants");
        }

        user.Status = user.Status == "active" ? "inactive" : "active";
        await db.SaveChangesAsync();
        TempData["Success"] = $"Acesso de {user.FullName} {(user.Status == "active" ? "ativado" : "desativado")}.";
        return RedirectToAction("Tenants");
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
            return RedirectToAction("Tenants");
        }

        var newPassword = $"Priser@{Guid.NewGuid().ToString()[..8]}";
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await db.SaveChangesAsync();
        TempData["Success"] = $"Nova senha de {user.FullName}: {newPassword}";
        return RedirectToAction("Tenants");
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
            return RedirectToAction("Tenants");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (normalizedEmail != user.Email && await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == normalizedEmail && u.Id != id))
        {
            TempData["Error"] = "Já existe um usuário com este email.";
            return RedirectToAction("Tenants");
        }

        user.FirstName = firstName.Trim();
        user.LastName = lastName.Trim();
        user.Email = normalizedEmail;
        await db.SaveChangesAsync();
        TempData["Success"] = $"Usuário {user.FullName} atualizado com sucesso.";
        return RedirectToAction("Tenants");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst("user_id")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
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
