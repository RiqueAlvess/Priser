namespace Priser.Models.Entities;

public class SurveyResponse : BaseEntity
{
    public Guid SurveyId { get; set; }
    public Survey Survey { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public bool Rewarded { get; set; } = false;
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SurveyAnswer> Answers { get; set; } = [];
}
