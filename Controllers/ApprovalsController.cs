using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Priser.Data;
using Priser.Services;

namespace Priser.Controllers;

[Authorize(Policy = "Manager")]
public class ApprovalsController(AppDbContext db, WalletService wallets, NotificationService notifications, CurrentUserService currentUser) : Controller
{
    public async Task<IActionResult> Index()
    {
        var userId = currentUser.UserId!.Value;

        var pending = await db.ApprovalRequests
            .Where(a => a.ApproverId == userId && a.Status == "pending")
            .Include(a => a.Recognition).ThenInclude(r => r.Sender)
            .Include(a => a.Recognition).ThenInclude(r => r.Receiver)
            .Include(a => a.Recognition).ThenInclude(r => r.CompanyValue)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        var history = await db.ApprovalRequests
            .Where(a => a.ApproverId == userId && a.Status != "pending")
            .Include(a => a.Recognition).ThenInclude(r => r.Sender)
            .Include(a => a.Recognition).ThenInclude(r => r.Receiver)
            .OrderByDescending(a => a.ReviewedAt)
            .Take(20)
            .ToListAsync();

        ViewBag.Pending = pending;
        ViewBag.History = history;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid requestId, string? notes = null)
    {
        var userId = currentUser.UserId!.Value;
        var tenantId = currentUser.TenantId!.Value;

        var request = await db.ApprovalRequests
            .Include(a => a.Recognition)
            .FirstOrDefaultAsync(a => a.Id == requestId && a.ApproverId == userId);

        if (request == null) return NotFound();

        request.Status = "approved";
        request.Notes = notes;
        request.ReviewedAt = DateTime.UtcNow;
        request.Recognition.Status = "approved";

        await db.SaveChangesAsync();

        await wallets.TransferPointsAsync(
            request.Recognition.SenderId, request.Recognition.ReceiverId, tenantId,
            request.Recognition.PointsAmount, "recognition", request.Recognition.Id, "Recognition");

        await notifications.SendAsync(request.Recognition.ReceiverId, tenantId, "recognition_received",
            "Você foi reconhecido! 🎉",
            $"Um reconhecimento de {request.Recognition.PointsAmount} pts foi aprovado.", "/feed");

        await notifications.SendAsync(request.Recognition.SenderId, tenantId, "recognition_approved",
            "Reconhecimento aprovado!", "Seu reconhecimento foi aprovado pelo gestor.", "/feed");

        TempData["Success"] = "Reconhecimento aprovado!";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid requestId, string? notes = null)
    {
        var userId = currentUser.UserId!.Value;
        var tenantId = currentUser.TenantId!.Value;

        var request = await db.ApprovalRequests
            .Include(a => a.Recognition)
            .FirstOrDefaultAsync(a => a.Id == requestId && a.ApproverId == userId);

        if (request == null) return NotFound();

        request.Status = "rejected";
        request.Notes = notes;
        request.ReviewedAt = DateTime.UtcNow;
        request.Recognition.Status = "rejected";

        await db.SaveChangesAsync();

        await notifications.SendAsync(request.Recognition.SenderId, tenantId, "recognition_rejected",
            "Reconhecimento recusado", "Seu reconhecimento foi recusado pelo gestor.", "/recognize");

        TempData["Error"] = "Reconhecimento recusado.";
        return RedirectToAction("Index");
    }
}
