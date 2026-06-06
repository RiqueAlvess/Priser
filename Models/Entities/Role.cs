namespace Priser.Models.Entities;

public class Role : BaseEntity
{
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public string? Description { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];
}
