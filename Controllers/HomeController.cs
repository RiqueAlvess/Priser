using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Priser.Data;
using Priser.Services;

namespace Priser.Controllers;

[Authorize]
public class HomeController(AppDbContext db, CurrentUserService currentUser) : Controller
{
    public async Task<IActionResult> Index()
    {
        var tenantId = currentUser.TenantId!.Value;
        var userId = currentUser.UserId!.Value;

        var user = await currentUser.GetUserAsync();
        var wallet = await db.Wallets
            .Where(w => w.UserId == userId && w.WalletType == "earned")
            .FirstOrDefaultAsync();

        var recentRecognitions = await db.Recognitions
            .Where(r => r.TenantId == tenantId && r.Status == "approved")
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .Include(r => r.Sender)
            .Include(r => r.Receiver)
            .Include(r => r.CompanyValue)
            .ToListAsync();

        var pendingApprovals = 0;
        if (currentUser.IsManager)
        {
            pendingApprovals = await db.ApprovalRequests
                .CountAsync(a => a.ApproverId == userId && a.Status == "pending");
        }

        var unreadNotifications = await db.Notifications
            .CountAsync(n => n.UserId == userId && n.ReadAt == null);

        ViewBag.User = user;
        ViewBag.WalletBalance = wallet?.Balance ?? 0;
        ViewBag.RecentRecognitions = recentRecognitions;
        ViewBag.PendingApprovals = pendingApprovals;
        ViewBag.UnreadNotifications = unreadNotifications;

        return View();
    }

    [Route("/error")]
    public IActionResult Error() => View();
}
