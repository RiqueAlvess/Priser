using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Priser.Data;
using Priser.Models.Entities;
using Priser.Models.ViewModels;

namespace Priser.Controllers;

public class AuthController(AppDbContext db) : Controller
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
    public IActionResult Register() => RedirectToAction("Login");

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
