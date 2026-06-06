namespace Priser.Models.Entities;

public class Comment : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid RecognitionId { get; set; }
    public Recognition Recognition { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Content { get; set; } = "";
}
