using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Priser.Data;
using Priser.Models.Entities;
using Priser.Models.ViewModels;
using Priser.Services;

namespace Priser.Controllers;

[Authorize]
public class RecognizeController(AppDbContext db, WalletService wallets, NotificationService notifications, CurrentUserService currentUser) : Controller
{
    public async Task<IActionResult> Index()
    {
        var tenantId = currentUser.TenantId!.Value;
        var userId = currentUser.UserId!.Value;

        ViewBag.Users = await db.Users
            .Where(u => u.TenantId == tenantId && u.Id != userId && u.Status == "active")
            .OrderBy(u => u.FirstName)
            .ToListAsync();
        ViewBag.CompanyValues = await db.CompanyValues
            .Where(v => v.TenantId == tenantId && v.IsActive)
            .OrderBy(v => v.DisplayOrder)
            .ToListAsync();
        ViewBag.Budget = await db.Budgets
            .FirstOrDefaultAsync(b => b.ManagerId == userId && b.IsActive);
        ViewBag.Wallet = await db.Wallets
            .FirstOrDefaultAsync(w => w.UserId == userId && w.WalletType == "allowance");

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(SendRecognitionViewModel model)
    {
        var tenantId = currentUser.TenantId!.Value;
        var userId = currentUser.UserId!.Value;

        if (!ModelState.IsValid)
        {
            await PopulateViewBag(tenantId, userId);
            return View("Index", model);
        }

        var policy = await db.ApprovalPolicies
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.IsActive);

        var needsApproval = policy != null && policy.RequiresApproval && model.Points >= policy.ThresholdPoints;

        var recognition = new Recognition
        {
            TenantId = tenantId,
            SenderId = userId,
            ReceiverId = model.ReceiverId,
            CompanyValueId = model.CompanyValueId,
            Message = model.Message,
            PointsAmount = model.Points,
            Visibility = model.Visibility,
            Status = needsApproval ? "pending_approval" : "approved"
        };
        db.Recognitions.Add(recognition);
        await db.SaveChangesAsync();

        if (needsApproval)
        {
            var manager = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            var approverId = manager?.ManagerId ?? userId;

            db.ApprovalRequests.Add(new ApprovalRequest
            {
                TenantId = tenantId,
                RecognitionId = recognition.Id,
                ApproverId = approverId,
                PolicyId = policy!.Id,
                Status = "pending"
            });
            await db.SaveChangesAsync();
            await notifications.SendAsync(approverId, tenantId, "approval_needed",
                "Aprovação necessária", $"Um reconhecimento de {model.Points} pts aguarda sua aprovação.",
                "/approvals");

            TempData["Success"] = "Reconhecimento enviado para aprovação!";
        }
        else
        {
            await wallets.TransferPointsAsync(userId, model.ReceiverId, tenantId,
                model.Points, "recognition", recognition.Id, "Recognition");

            await notifications.SendAsync(model.ReceiverId, tenantId, "recognition_received",
                "Você foi reconhecido! 🎉", $"Você recebeu {model.Points} pontos.",
                "/feed");

            TempData["Success"] = "Reconhecimento enviado com sucesso! 🎉";
        }

        return RedirectToAction("Index", "Feed");
    }

    private async Task PopulateViewBag(Guid tenantId, Guid userId)
    {
        ViewBag.Users = await db.Users.Where(u => u.TenantId == tenantId && u.Id != userId && u.Status == "active").OrderBy(u => u.FirstName).ToListAsync();
        ViewBag.CompanyValues = await db.CompanyValues.Where(v => v.TenantId == tenantId && v.IsActive).OrderBy(v => v.DisplayOrder).ToListAsync();
        ViewBag.Budget = await db.Budgets.FirstOrDefaultAsync(b => b.ManagerId == userId && b.IsActive);
        ViewBag.Wallet = await db.Wallets.FirstOrDefaultAsync(w => w.UserId == userId && w.WalletType == "allowance");
    }
}
