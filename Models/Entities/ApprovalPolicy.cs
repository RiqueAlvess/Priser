namespace Priser.Models.Entities;

public class ApprovalPolicy : BaseEntity
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string Name { get; set; } = "";
    public int ThresholdPoints { get; set; } = 100;
    public bool RequiresApproval { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public ICollection<ApprovalRequest> ApprovalRequests { get; set; } = [];
}
