namespace Priser.Models.Entities;

public class Budget : BaseEntity
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid ManagerId { get; set; }
    public User Manager { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public decimal SpentAmount { get; set; }
    public decimal RemainingAmount => TotalAmount - SpentAmount;
    public string RenewalPeriod { get; set; } = "monthly";
    public DateTime NextRenewalDate { get; set; }
    public bool ResetUnused { get; set; } = false;
    public bool IsActive { get; set; } = true;
}
