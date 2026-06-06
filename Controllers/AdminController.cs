using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Priser.Data;
using Priser.Models.Entities;
using Priser.Services;

namespace Priser.Controllers;

[Authorize(Policy = "TenantAdmin")]
[Route("admin")]
public class AdminController(AppDbContext db, WalletService wallets, CurrentUserService currentUser) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var tenantId = currentUser.TenantId!.Value;

        ViewBag.TotalUsers = await db.Users.CountAsync(u => u.TenantId == tenantId);
        ViewBag.TotalRecognitions = await db.Recognitions.CountAsync(r => r.TenantId == tenantId);
        ViewBag.PendingApprovals = await db.ApprovalRequests.CountAsync(a => a.TenantId == tenantId && a.Status == "pending");
        ViewBag.TotalPointsIssued = await db.Transactions
            .Where(t => t.TenantId == tenantId && t.TransactionType == "recognition")
            .SumAsync(t => (decimal?)t.Amount) ?? 0;

        return View();
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users()
    {
        var tenantId = currentUser.TenantId!.Value;
        var users = await db.Users
            .Where(u => u.TenantId == tenantId)
            .Include(u => u.Department)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.Wallets)
            .OrderBy(u => u.FirstName)
            .ToListAsync();

        ViewBag.Users = users;
        ViewBag.Departments = await db.Departments.Where(d => d.TenantId == tenantId).ToListAsync();
        ViewBag.Roles = await db.Roles.ToListAsync();
        return View();
    }

    [HttpPost("users/invite")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InviteUser(string email, string firstName, string lastName, Guid? departmentId, Guid roleId)
    {
        var tenantId = currentUser.TenantId!.Value;

        if (await db.Users.AnyAsync(u => u.Email == email.ToLower() && u.TenantId == tenantId))
        {
            TempData["Error"] = "Email já cadastrado.";
            return RedirectToAction("Users");
        }

        var tempPassword = $"Priser@{Guid.NewGuid().ToString()[..8]}";
        var user = new User
        {
            TenantId = tenantId,
            Email = email.ToLower(),
            FirstName = firstName,
            LastName = lastName,
            DepartmentId = departmentId,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword),
            Status = "active"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId, TenantId = tenantId });
        await db.SaveChangesAsync();

        await wallets.GetOrCreateWalletAsync(user.Id, tenantId, "earned");
        await wallets.GetOrCreateWalletAsync(user.Id, tenantId, "allowance");

        TempData["Success"] = $"Usuário criado! Senha temporária: {tempPassword}";
        return RedirectToAction("Users");
    }

    [HttpGet("company-values")]
    public async Task<IActionResult> CompanyValues()
    {
        var tenantId = currentUser.TenantId!.Value;
        ViewBag.Values = await db.CompanyValues.Where(v => v.TenantId == tenantId).OrderBy(v => v.DisplayOrder).ToListAsync();
        return View();
    }

    [HttpPost("company-values/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCompanyValue(string name, string? description, string? icon, string color)
    {
        var tenantId = currentUser.TenantId!.Value;
        var maxOrder = await db.CompanyValues.Where(v => v.TenantId == tenantId).MaxAsync(v => (int?)v.DisplayOrder) ?? 0;

        db.CompanyValues.Add(new CompanyValue
        {
            TenantId = tenantId, Name = name, Description = description,
            Icon = icon, Color = color, DisplayOrder = maxOrder + 1
        });
        await db.SaveChangesAsync();
        TempData["Success"] = "Valor criado!";
        return RedirectToAction("CompanyValues");
    }

    [HttpGet("surveys")]
    public async Task<IActionResult> Surveys()
    {
        var tenantId = currentUser.TenantId!.Value;
        ViewBag.Surveys = await db.Surveys
            .Where(s => s.TenantId == tenantId)
            .Include(s => s.Questions)
            .Include(s => s.Responses)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
        return View();
    }

    [HttpPost("surveys/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSurvey(string title, string? description, string surveyType, int? rewardPoints, bool isAnonymous, DateTime? endsAt)
    {
        var tenantId = currentUser.TenantId!.Value;
        db.Surveys.Add(new Survey
        {
            TenantId = tenantId,
            Title = title,
            Description = description,
            SurveyType = surveyType,
            RewardPoints = rewardPoints,
            IsAnonymous = isAnonymous,
            EndsAt = endsAt?.ToUniversalTime(),
            StartsAt = DateTime.UtcNow,
            IsActive = true
        });
        await db.SaveChangesAsync();
        TempData["Success"] = "Pesquisa criada!";
        return RedirectToAction("Surveys");
    }

    [HttpGet("budgets")]
    public async Task<IActionResult> Budgets()
    {
        var tenantId = currentUser.TenantId!.Value;
        ViewBag.Budgets = await db.Budgets
            .Where(b => b.TenantId == tenantId)
            .Include(b => b.Manager)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
        ViewBag.Managers = await db.Users
            .Where(u => u.TenantId == tenantId && u.UserRoles.Any(ur => ur.Role.NormalizedName == "MANAGER"))
            .ToListAsync();
        return View();
    }

    [HttpPost("budgets/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBudget(Guid managerId, decimal totalAmount, string renewalPeriod, bool resetUnused)
    {
        var tenantId = currentUser.TenantId!.Value;
        var manager = await db.Users.FindAsync(managerId);
        if (manager == null) return NotFound();

        var nextRenewal = renewalPeriod == "monthly"
            ? DateTime.UtcNow.AddMonths(1)
            : DateTime.UtcNow.AddYears(1);

        db.Budgets.Add(new Budget
        {
            TenantId = tenantId,
            ManagerId = managerId,
            TotalAmount = totalAmount,
            SpentAmount = 0,
            RenewalPeriod = renewalPeriod,
            NextRenewalDate = nextRenewal,
            ResetUnused = resetUnused,
            IsActive = true
        });
        await wallets.CreditPointsAsync(managerId, tenantId, totalAmount, "budget_grant", "Orçamento concedido pelo admin");
        await db.SaveChangesAsync();
        TempData["Success"] = "Orçamento criado e pontos creditados!";
        return RedirectToAction("Budgets");
    }

    [HttpGet("marketplace")]
    public async Task<IActionResult> Marketplace()
    {
        var tenantId = currentUser.TenantId!.Value;
        ViewBag.Products = await db.Products
            .Where(p => p.TenantId == null || p.TenantId == tenantId)
            .OrderBy(p => p.Name)
            .ToListAsync();
        return View();
    }

    [HttpPost("marketplace/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProduct(string name, string? description, string? imageUrl, decimal pricePoints, string category, int? stockQuantity)
    {
        var tenantId = currentUser.TenantId!.Value;
        db.Products.Add(new Product
        {
            TenantId = tenantId,
            Name = name,
            Description = description,
            ImageUrl = imageUrl,
            PricePoints = pricePoints,
            Category = category,
            StockQuantity = stockQuantity,
            StockType = "internal",
            IsActive = true
        });
        await db.SaveChangesAsync();
        TempData["Success"] = "Produto adicionado ao marketplace!";
        return RedirectToAction("Marketplace");
    }
    [HttpGet("enterprise")]
    public async Task<IActionResult> Enterprise()
    {
        var tenantId = currentUser.TenantId!.Value;

        var compliance = await db.ComplianceSettings.FirstOrDefaultAsync(c => c.TenantId == tenantId);
        if (compliance == null)
        {
            compliance = new ComplianceSetting { TenantId = tenantId };
            db.ComplianceSettings.Add(compliance);
        }

        var defaultFlags = new[]
        {
            "sso_stub", "mfa_policy", "memory_books", "automation_rules", "gamification",
            "analytics_bi", "white_label", "hris_sync_stub", "split_payment_stub", "marketplace_stub"
        };

        var existingFlags = await db.FeatureFlags
            .Where(f => f.TenantId == tenantId)
            .Select(f => f.Key)
            .ToListAsync();

        foreach (var key in defaultFlags.Except(existingFlags))
        {
            db.FeatureFlags.Add(new FeatureFlag { TenantId = tenantId, Key = key, IsEnabled = key.EndsWith("_stub") || key is "memory_books" or "gamification" or "analytics_bi" });
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync();
        }

        ViewBag.Compliance = await db.ComplianceSettings.FirstAsync(c => c.TenantId == tenantId);
        ViewBag.FeatureFlags = await db.FeatureFlags.Where(f => f.TenantId == tenantId).OrderBy(f => f.Key).ToListAsync();
        ViewBag.PointPools = await db.PointPools.Where(p => p.TenantId == tenantId).OrderBy(p => p.Name).ToListAsync();
        ViewBag.Integrations = await db.Integrations.Where(i => i.TenantId == tenantId).OrderBy(i => i.Provider).ToListAsync();
        ViewBag.AutomationRules = await db.AutomationRules.Where(r => r.TenantId == tenantId).OrderBy(r => r.Name).ToListAsync();
        ViewBag.Badges = await db.Badges.Where(b => b.TenantId == tenantId).OrderBy(b => b.Name).ToListAsync();
        ViewBag.Reports = await db.Reports.Where(r => r.TenantId == tenantId).OrderBy(r => r.Name).ToListAsync();
        var burnRateCutoff = DateTime.UtcNow.AddDays(-30);
        ViewBag.PointsLiability = await db.Wallets.Where(w => w.TenantId == tenantId && w.WalletType == "earned").SumAsync(w => (decimal?)w.Balance) ?? 0;
        ViewBag.BurnRate = await db.Transactions.Where(t => t.TenantId == tenantId && t.CreatedAt >= burnRateCutoff).SumAsync(t => (decimal?)t.Amount) ?? 0;

        return View();
    }

    [HttpPost("enterprise/compliance")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCompliance(int dataRetentionDays, int auditRetentionDays, bool ssoEnforced, bool mfaRequired, bool immutableBackupsEnabled, string encryptionMode)
    {
        var tenantId = currentUser.TenantId!.Value;
        var compliance = await db.ComplianceSettings.FirstOrDefaultAsync(c => c.TenantId == tenantId);
        if (compliance == null)
        {
            compliance = new ComplianceSetting { TenantId = tenantId };
            db.ComplianceSettings.Add(compliance);
        }

        compliance.DataRetentionDays = dataRetentionDays;
        compliance.AuditRetentionDays = auditRetentionDays;
        compliance.SsoEnforced = ssoEnforced;
        compliance.MfaRequired = mfaRequired;
        compliance.ImmutableBackupsEnabled = immutableBackupsEnabled;
        compliance.EncryptionMode = encryptionMode;

        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = currentUser.UserId,
            Action = "enterprise.compliance.updated",
            EntityType = nameof(ComplianceSetting),
            EntityId = compliance.Id,
            NewValue = $"retention={dataRetentionDays}; audit={auditRetentionDays}; sso={ssoEnforced}; mfa={mfaRequired}"
        });

        await db.SaveChangesAsync();
        TempData["Success"] = "Configurações de compliance atualizadas.";
        return RedirectToAction("Enterprise");
    }

    [HttpPost("enterprise/feature-flags/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFeatureFlag(Guid id)
    {
        var tenantId = currentUser.TenantId!.Value;
        var flag = await db.FeatureFlags.FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tenantId);
        if (flag == null) return NotFound();

        flag.IsEnabled = !flag.IsEnabled;
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = currentUser.UserId,
            Action = "enterprise.feature_flag.toggled",
            EntityType = nameof(FeatureFlag),
            EntityId = flag.Id,
            NewValue = $"{flag.Key}={flag.IsEnabled}"
        });
        await db.SaveChangesAsync();
        return RedirectToAction("Enterprise");
    }

    [HttpPost("enterprise/point-pools/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePointPool(string name, decimal balance)
    {
        var tenantId = currentUser.TenantId!.Value;
        db.PointPools.Add(new PointPool { TenantId = tenantId, Name = name, Balance = balance });
        await db.SaveChangesAsync();
        TempData["Success"] = "Pool corporativo criado.";
        return RedirectToAction("Enterprise");
    }

    [HttpPost("enterprise/integrations/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateIntegrationStub(string provider, string integrationType)
    {
        var tenantId = currentUser.TenantId!.Value;
        db.Integrations.Add(new Integration
        {
            TenantId = tenantId,
            Provider = provider,
            IntegrationType = integrationType,
            Status = "stub",
            SettingsJson = "{\"mode\":\"no-real-integration\"}"
        });
        await db.SaveChangesAsync();
        TempData["Success"] = "Integração simulada registrada sem conexão real externa.";
        return RedirectToAction("Enterprise");
    }

    [HttpPost("enterprise/automation-rules/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAutomationRule(string name, string triggerType, string scheduleCron, int rewardPoints)
    {
        var tenantId = currentUser.TenantId!.Value;
        db.AutomationRules.Add(new AutomationRule
        {
            TenantId = tenantId,
            Name = name,
            TriggerType = triggerType,
            ScheduleCron = scheduleCron,
            ActionsJson = $"{{\"rewardPoints\":{rewardPoints}}}"
        });
        await db.SaveChangesAsync();
        TempData["Success"] = "Regra de automação criada.";
        return RedirectToAction("Enterprise");
    }

    [HttpPost("enterprise/badges/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBadge(string name, string? description, string? iconUrl)
    {
        var tenantId = currentUser.TenantId!.Value;
        db.Badges.Add(new Badge { TenantId = tenantId, Name = name, Description = description, IconUrl = iconUrl });
        await db.SaveChangesAsync();
        TempData["Success"] = "Badge criado.";
        return RedirectToAction("Enterprise");
    }

    [HttpPost("enterprise/reports/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateReport(string name, string reportType, string visibility)
    {
        var tenantId = currentUser.TenantId!.Value;
        db.Reports.Add(new Report
        {
            TenantId = tenantId,
            CreatedByUserId = currentUser.UserId!.Value,
            Name = name,
            ReportType = reportType,
            Visibility = visibility
        });
        await db.SaveChangesAsync();
        TempData["Success"] = "Relatório salvo.";
        return RedirectToAction("Enterprise");
    }

}
