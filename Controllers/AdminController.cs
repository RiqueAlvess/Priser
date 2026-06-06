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
}
