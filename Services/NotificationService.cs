using Priser.Data;
using Priser.Models.Entities;

namespace Priser.Services;

public class NotificationService(AppDbContext db)
{
    public async Task SendAsync(Guid userId, Guid tenantId, string type, string title, string message, string? actionUrl = null)
    {
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            TenantId = tenantId,
            Type = type,
            Title = title,
            Message = message,
            ActionUrl = actionUrl
        });
        await db.SaveChangesAsync();
    }
}
