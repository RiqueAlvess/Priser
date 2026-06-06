namespace Priser.Models.Entities;

public class SurveyAnswer : BaseEntity
{
    public Guid SurveyResponseId { get; set; }
    public SurveyResponse SurveyResponse { get; set; } = null!;
    public Guid QuestionId { get; set; }
    public SurveyQuestion Question { get; set; } = null!;
    public string? Value { get; set; }
}
