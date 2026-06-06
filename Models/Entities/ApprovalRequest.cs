namespace Priser.Models.Entities;

public class ApprovalRequest : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid RecognitionId { get; set; }
    public Recognition Recognition { get; set; } = null!;
    public Guid ApproverId { get; set; }
    public User Approver { get; set; } = null!;
    public Guid? PolicyId { get; set; }
    public ApprovalPolicy? Policy { get; set; }
    public string Status { get; set; } = "pending";
    public string? Notes { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
