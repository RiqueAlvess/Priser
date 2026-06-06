namespace Priser.Models.Entities;

public class Survey : BaseEntity
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string SurveyType { get; set; } = "pulse";
    public int? RewardPoints { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public bool IsAnonymous { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public ICollection<SurveyQuestion> Questions { get; set; } = [];
    public ICollection<SurveyResponse> Responses { get; set; } = [];
}
