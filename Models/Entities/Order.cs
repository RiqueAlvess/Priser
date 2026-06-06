namespace Priser.Models.Entities;

public class Order : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public decimal TotalPoints { get; set; }
    public string Status { get; set; } = "pending";
    public string? ShippingAddress { get; set; }
    public string? TrackingNumber { get; set; }

    public ICollection<OrderItem> Items { get; set; } = [];
}
