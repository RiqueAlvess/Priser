using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Priser.Data;
using Priser.Models.Entities;
using Priser.Services;

namespace Priser.Controllers;

[Authorize]
public class FeedController(AppDbContext db, CurrentUserService currentUser) : Controller
{
    public async Task<IActionResult> Index(string? valueFilter = null, string? departmentFilter = null, int page = 1)
    {
        var tenantId = currentUser.TenantId!.Value;
        var userId = currentUser.UserId!.Value;
        const int pageSize = 10;

        var query = db.Recognitions
            .Where(r => r.TenantId == tenantId && r.Status == "approved")
            .Include(r => r.Sender).ThenInclude(u => u.Department)
            .Include(r => r.Receiver).ThenInclude(u => u.Department)
            .Include(r => r.CompanyValue)
            .Include(r => r.Comments).ThenInclude(c => c.User)
            .Include(r => r.Reactions)
            .AsQueryable();

        if (!string.IsNullOrEmpty(valueFilter) && Guid.TryParse(valueFilter, out var valueId))
            query = query.Where(r => r.CompanyValueId == valueId);

        if (!string.IsNullOrEmpty(departmentFilter) && Guid.TryParse(departmentFilter, out var deptId))
            query = query.Where(r => r.Sender.DepartmentId == deptId || r.Receiver.DepartmentId == deptId);

        var total = await query.CountAsync();
        var recognitions = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Recognitions = recognitions;
        ViewBag.CurrentUserId = userId;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        ViewBag.CompanyValues = await db.CompanyValues.Where(v => v.TenantId == tenantId && v.IsActive).ToListAsync();
        ViewBag.Departments = await db.Departments.Where(d => d.TenantId == tenantId).ToListAsync();
        ViewBag.ValueFilter = valueFilter;
        ViewBag.DepartmentFilter = departmentFilter;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(Guid recognitionId, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return RedirectToAction("Index");

        var userId = currentUser.UserId!.Value;
        var tenantId = currentUser.TenantId!.Value;

        db.Comments.Add(new Comment
        {
            TenantId = tenantId,
            RecognitionId = recognitionId,
            UserId = userId,
            Content = content.Trim()
        });
        await db.SaveChangesAsync();
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> React(Guid recognitionId, string emoji = "👏")
    {
        var userId = currentUser.UserId!.Value;
        var tenantId = currentUser.TenantId!.Value;

        var existing = await db.Reactions
            .FirstOrDefaultAsync(r => r.RecognitionId == recognitionId && r.UserId == userId && r.Emoji == emoji);

        if (existing != null)
            db.Reactions.Remove(existing);
        else
            db.Reactions.Add(new Reaction { TenantId = tenantId, RecognitionId = recognitionId, UserId = userId, Emoji = emoji });

        await db.SaveChangesAsync();
        return RedirectToAction("Index");
    }
}
