namespace Priser.Models.Entities;

public class UserRole
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public Guid TenantId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
