using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Priser.Data;
using Priser.Models.Entities;
using Priser.Models.ViewModels;
using Priser.Services;

namespace Priser.Controllers;

public class AuthController(AppDbContext db, WalletService wallets) : Controller
{
    [HttpGet("/auth/login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost("/auth/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == model.Email.ToLower() && u.DeletedAt == null);

        if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError("", "Email ou senha inválidos.");
            return View(model);
        }

        if (user.Status != "active")
        {
            ModelState.AddModelError("", "Conta inativa. Entre em contato com o RH.");
            return View(model);
        }

        await SignInUserAsync(user);

        return LocalRedirect(returnUrl ?? "/");
    }

    [HttpGet("/auth/register")]
    public IActionResult Register() => View();

    [HttpPost("/auth/register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var slug = model.CompanyName.ToLower().Replace(" ", "-").Replace("_", "-");
        if (await db.Tenants.AnyAsync(t => t.Slug == slug))
            slug = $"{slug}-{Guid.NewGuid().ToString()[..6]}";

        if (await db.Users.AnyAsync(u => u.Email == model.Email.ToLower()))
        {
            ModelState.AddModelError("Email", "Este email já está cadastrado.");
            return View(model);
        }

        using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var tenant = new Tenant
            {
                Name = model.CompanyName,
                Slug = slug,
                Status = "active",
                BillingPlan = "trial"
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();

            var employeeRole = await db.Roles.FirstAsync(r => r.NormalizedName == "TENANTADMIN");
            var user = new User
            {
                TenantId = tenant.Id,
                Email = model.Email.ToLower(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                FirstName = model.FirstName,
                LastName = model.LastName,
                Status = "active"
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = employeeRole.Id, TenantId = tenant.Id });

            // Seed default company values
            db.CompanyValues.AddRange(
                new CompanyValue { TenantId = tenant.Id, Name = "Inovação", Icon = "lightbulb", Color = "#00658d", DisplayOrder = 1 },
                new CompanyValue { TenantId = tenant.Id, Name = "Colaboração", Icon = "group", Color = "#567bff", DisplayOrder = 2 },
                new CompanyValue { TenantId = tenant.Id, Name = "Excelência", Icon = "star", Color = "#0039b5", DisplayOrder = 3 },
                new CompanyValue { TenantId = tenant.Id, Name = "Respeito", Icon = "handshake", Color = "#004b69", DisplayOrder = 4 }
            );

            // Seed default approval policy
            db.ApprovalPolicies.Add(new ApprovalPolicy
            {
                TenantId = tenant.Id,
                Name = "Política Padrão",
                ThresholdPoints = 100,
                RequiresApproval = true,
                IsActive = true
            });

            await db.SaveChangesAsync();
            await wallets.GetOrCreateWalletAsync(user.Id, tenant.Id, "earned");
            await wallets.GetOrCreateWalletAsync(user.Id, tenant.Id, "allowance");

            await tx.CommitAsync();
            await SignInUserAsync(user);
            return RedirectToAction("Index", "Home");
        }
        catch
        {
            await tx.RollbackAsync();
            ModelState.AddModelError("", "Erro ao criar conta. Tente novamente.");
            return View(model);
        }
    }

    [HttpPost("/auth/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("PriserCookies");
        return RedirectToAction("Login");
    }

    private async Task SignInUserAsync(User user)
    {
        var role = user.UserRoles.FirstOrDefault()?.Role?.Name ?? "Employee";
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, role),
            new("tenant_id", user.TenantId.ToString()),
            new("tenant_logo_url", user.Tenant.LogoUrl ?? ""),
            new("tenant_name", user.Tenant.Name),
        };
        var identity = new ClaimsIdentity(claims, "PriserCookies");
        await HttpContext.SignInAsync("PriserCookies", new ClaimsPrincipal(identity));
    }
}
