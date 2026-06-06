using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Priser.Data;
using Priser.Models.Entities;
using Priser.Services;

namespace Priser.Controllers;

[Authorize]
public class MarketplaceController(AppDbContext db, WalletService wallets, CurrentUserService currentUser) : Controller
{
    public async Task<IActionResult> Index(string? category = null, string? search = null)
    {
        var tenantId = currentUser.TenantId!.Value;
        var userId = currentUser.UserId!.Value;

        var query = db.Products
            .Where(p => (p.TenantId == null || p.TenantId == tenantId) && p.IsActive)
            .AsQueryable();

        if (!string.IsNullOrEmpty(category))
            query = query.Where(p => p.Category == category);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.Name.Contains(search) || (p.Description != null && p.Description.Contains(search)));

        var products = await query.OrderBy(p => p.PricePoints).ToListAsync();
        var wallet = await db.Wallets.FirstOrDefaultAsync(w => w.UserId == userId && w.WalletType == "earned");
        var categories = await db.Products
            .Where(p => p.IsActive && (p.TenantId == null || p.TenantId == tenantId))
            .Select(p => p.Category).Distinct().ToListAsync();

        ViewBag.Products = products;
        ViewBag.WalletBalance = wallet?.Balance ?? 0;
        ViewBag.Categories = categories;
        ViewBag.CategoryFilter = category;
        ViewBag.Search = search;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Redeem(Guid productId)
    {
        var userId = currentUser.UserId!.Value;
        var tenantId = currentUser.TenantId!.Value;

        var product = await db.Products.FindAsync(productId);
        if (product == null || !product.IsActive)
        {
            TempData["Error"] = "Produto não encontrado.";
            return RedirectToAction("Index");
        }

        if (product.StockQuantity.HasValue && product.StockQuantity <= 0)
        {
            TempData["Error"] = "Produto sem estoque.";
            return RedirectToAction("Index");
        }

        var ok = await wallets.DebitPointsAsync(userId, tenantId, product.PricePoints, "redemption", productId);
        if (!ok)
        {
            TempData["Error"] = "Pontos insuficientes.";
            return RedirectToAction("Index");
        }

        if (product.StockQuantity.HasValue)
            product.StockQuantity--;

        var order = new Order
        {
            TenantId = tenantId,
            UserId = userId,
            TotalPoints = product.PricePoints,
            Status = "processing"
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        db.OrderItems.Add(new OrderItem
        {
            OrderId = order.Id,
            ProductId = productId,
            Quantity = 1,
            UnitPoints = product.PricePoints,
            TotalPoints = product.PricePoints
        });
        await db.SaveChangesAsync();

        TempData["Success"] = $"Resgate realizado com sucesso! Pedido #{order.Id.ToString()[..8].ToUpper()}";
        return RedirectToAction("Orders");
    }

    public async Task<IActionResult> Orders()
    {
        var userId = currentUser.UserId!.Value;
        var orders = await db.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .ToListAsync();

        ViewBag.Orders = orders;
        return View();
    }
}
