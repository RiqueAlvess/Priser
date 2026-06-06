namespace Priser.Models.Entities;

public class Recognition : BaseEntity
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid SenderId { get; set; }
    public User Sender { get; set; } = null!;
    public Guid ReceiverId { get; set; }
    public User Receiver { get; set; } = null!;
    public Guid? CompanyValueId { get; set; }
    public CompanyValue? CompanyValue { get; set; }
    public string Message { get; set; } = "";
    public int PointsAmount { get; set; }
    public string Visibility { get; set; } = "public";
    public string Status { get; set; } = "pending_approval";
    public bool IsApproved => Status == "approved";

    public ICollection<Comment> Comments { get; set; } = [];
    public ICollection<Reaction> Reactions { get; set; } = [];
    public ICollection<ApprovalRequest> ApprovalRequests { get; set; } = [];
}
