using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Priser.Data;
using Priser.Services;

namespace Priser.Controllers;

[Authorize]
public class WalletController(AppDbContext db, CurrentUserService currentUser) : Controller
{
    public async Task<IActionResult> Index()
    {
        var userId = currentUser.UserId!.Value;
        var tenantId = currentUser.TenantId!.Value;

        var wallets = await db.Wallets
            .Where(w => w.UserId == userId)
            .ToListAsync();

        var transactions = await db.Transactions
            .Where(t => (t.FromWallet!.UserId == userId || t.ToWallet!.UserId == userId) && t.TenantId == tenantId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(50)
            .Include(t => t.FromWallet).ThenInclude(w => w!.User)
            .Include(t => t.ToWallet).ThenInclude(w => w!.User)
            .ToListAsync();

        ViewBag.Wallets = wallets;
        ViewBag.Transactions = transactions;
        ViewBag.UserId = userId;

        return View();
    }
}
