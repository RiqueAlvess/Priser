using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Priser.Data;
using Priser.Services;

namespace Priser.Controllers;

[Authorize]
public class NotificationsController(AppDbContext db, CurrentUserService currentUser) : Controller
{
    public async Task<IActionResult> Index()
    {
        var userId = currentUser.UserId!.Value;
        var notifications = await db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync();

        // Mark all as read
        foreach (var n in notifications.Where(n => n.ReadAt == null))
            n.ReadAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        ViewBag.Notifications = notifications;
        return View();
    }
}
