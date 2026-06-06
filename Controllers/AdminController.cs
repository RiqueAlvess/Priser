using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Priser.Data;
using Priser.Models.Entities;
using Priser.Services;

namespace Priser.Controllers;

[Authorize(Policy = "TenantAdmin")]
[Route("admin")]
public class AdminController(AppDbContext db, WalletService wallets, CurrentUserService currentUser, EnterpriseSchemaService enterpriseSchema) : Controller
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

        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == roleId);
        if (role == null || role.NormalizedName == "SYSTEMADMIN")
        {
            TempData["Error"] = "Papel inválido para este tenant.";
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

        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, TenantId = tenantId });
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

    [HttpPost("users/import-csv")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportUsersCsv(IFormFile csvFile)
    {
        if (csvFile.Length == 0)
        {
            TempData["Error"] = "Envie um CSV com colaboradores.";
            return RedirectToAction("Users");
        }

        var tenantId = currentUser.TenantId!.Value;
        var roles = await db.Roles.ToDictionaryAsync(r => r.NormalizedName, r => r);
        var usersByEmail = await db.Users.Where(u => u.TenantId == tenantId).ToDictionaryAsync(u => u.Email);
        var imported = 0;
        var skipped = 0;
        var pendingManagers = new List<(User User, string ManagerEmail)>();

        using var reader = new StreamReader(csvFile.OpenReadStream());
        var lineNumber = 0;
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var columns = ParseCsvLine(line);
            if (lineNumber == 1 && columns.Any(c => c.Equals("email", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var email = GetColumn(columns, 0).Trim().ToLowerInvariant();
            var firstName = GetColumn(columns, 1).Trim();
            var lastName = GetColumn(columns, 2).Trim();
            var roleName = GetColumn(columns, 3).Trim().ToUpperInvariant();
            var departmentName = GetColumn(columns, 4).Trim();
            var managerEmail = GetColumn(columns, 5).Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || usersByEmail.ContainsKey(email))
            {
                skipped++;
                continue;
            }

            var normalizedRole = roleName switch
            {
                "TENANTADMIN" or "TENANT ADMIN" => "TENANTADMIN",
                "MANAGER" => "MANAGER",
                "VIEWER" or "READONLY" or "READ-ONLY" => "VIEWER",
                _ => "EMPLOYEE"
            };

            if (normalizedRole == "SYSTEMADMIN") normalizedRole = "EMPLOYEE";
            var role = roles[normalizedRole];
            Guid? departmentId = null;
            if (!string.IsNullOrWhiteSpace(departmentName))
            {
                var department = await db.Departments.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Name == departmentName);
                if (department == null)
                {
                    department = new Department { TenantId = tenantId, Name = departmentName };
                    db.Departments.Add(department);
                    await db.SaveChangesAsync();
                }
                departmentId = department.Id;
            }

            var user = new User
            {
                TenantId = tenantId,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                DepartmentId = departmentId,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword($"Priser@{Guid.NewGuid().ToString()[..8]}"),
                Status = "active"
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            db.UserRoles.Add(new UserRole { TenantId = tenantId, UserId = user.Id, RoleId = role.Id });
            await wallets.GetOrCreateWalletAsync(user.Id, tenantId, "earned");
            await wallets.GetOrCreateWalletAsync(user.Id, tenantId, "allowance");

            usersByEmail[email] = user;
            if (!string.IsNullOrWhiteSpace(managerEmail)) pendingManagers.Add((user, managerEmail));
            imported++;
        }

        foreach (var (user, managerEmail) in pendingManagers)
        {
            if (usersByEmail.TryGetValue(managerEmail, out var manager))
            {
                user.ManagerId = manager.Id;
            }
        }

        await db.SaveChangesAsync();
        TempData["Success"] = $"Importação concluída: {imported} colaboradores criados, {skipped} linhas ignoradas.";
        return RedirectToAction("Users");
    }

    private static string GetColumn(IReadOnlyList<string> columns, int index) => index < columns.Count ? columns[index] : "";

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        var insideQuotes = false;

        foreach (var character in line)
        {
            if (character == '"')
            {
                insideQuotes = !insideQuotes;
                continue;
            }

            if (character == ',' && !insideQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        values.Add(current.ToString());
        return values;
    }

    [HttpGet("enterprise")]
    public async Task<IActionResult> Enterprise()
    {
        await enterpriseSchema.EnsureAsync();
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

        ViewBag.Tenant = await db.Tenants.FirstAsync(t => t.Id == tenantId);
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


    [HttpPost("enterprise/branding")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBranding(string name, string? logoUrl, string? primaryColor, string? accentColor)
    {
        var tenantId = currentUser.TenantId!.Value;
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenant == null) return NotFound();

        tenant.Name = name.Trim();
        tenant.LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim();
        tenant.PrimaryColor = string.IsNullOrWhiteSpace(primaryColor) ? tenant.PrimaryColor : primaryColor.Trim();
        tenant.AccentColor = string.IsNullOrWhiteSpace(accentColor) ? tenant.AccentColor : accentColor.Trim();

        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = currentUser.UserId,
            Action = "enterprise.branding.updated",
            EntityType = nameof(Tenant),
            EntityId = tenant.Id,
            NewValue = $"name={tenant.Name}; logo={tenant.LogoUrl}"
        });

        await db.SaveChangesAsync();
        TempData["Success"] = "Logo e identidade da empresa atualizados. Faça login novamente para ver o logo no menu.";
        return RedirectToAction("Enterprise");
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
