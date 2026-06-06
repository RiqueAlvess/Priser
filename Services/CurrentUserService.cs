using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Priser.Data;
using Priser.Models.Entities;

namespace Priser.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor, AppDbContext db)
{
    private User? _cached;

    public Guid? UserId
    {
        get
        {
            var val = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return val is null ? null : Guid.Parse(val);
        }
    }

    public Guid? TenantId
    {
        get
        {
            var val = httpContextAccessor.HttpContext?.User.FindFirstValue("tenant_id");
            return val is null ? null : Guid.Parse(val);
        }
    }

    public string? Role => httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role);

    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public bool IsAdmin => Role is "TenantAdmin" or "SystemAdmin";
    public bool IsManager => Role is "Manager" or "TenantAdmin" or "SystemAdmin";

    public async Task<User?> GetUserAsync()
    {
        if (_cached != null) return _cached;
        if (UserId is null) return null;
        _cached = await db.Users
            .Include(u => u.Department)
            .Include(u => u.Wallets)
            .FirstOrDefaultAsync(u => u.Id == UserId.Value);
        return _cached;
    }
}
