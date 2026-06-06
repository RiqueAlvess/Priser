namespace Priser.Models.Entities;

public class SurveyQuestion : BaseEntity
{
    public Guid SurveyId { get; set; }
    public Survey Survey { get; set; } = null!;
    public string Text { get; set; } = "";
    public string QuestionType { get; set; } = "rating";
    public int DisplayOrder { get; set; }
    public bool IsRequired { get; set; } = true;
    public string? Options { get; set; }

    public ICollection<SurveyAnswer> Answers { get; set; } = [];
}
