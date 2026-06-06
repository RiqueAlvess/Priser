namespace Priser.Models.Entities;

public class CompanyValue : BaseEntity
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string Color { get; set; } = "#00658d";
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    public ICollection<Recognition> Recognitions { get; set; } = [];
}
