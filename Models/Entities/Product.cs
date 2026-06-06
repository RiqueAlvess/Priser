namespace Priser.Models.Entities;

public class Product : BaseEntity
{
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public decimal PricePoints { get; set; }
    public string Category { get; set; } = "general";
    public string StockType { get; set; } = "global";
    public int? StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<OrderItem> OrderItems { get; set; } = [];
}
