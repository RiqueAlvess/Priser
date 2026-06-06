namespace Priser.Models.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? LogoUrl { get; set; }
    public string? CustomDomain { get; set; }
    public string BillingPlan { get; set; } = "starter";
    public string Status { get; set; } = "active";
    public string? PrimaryColor { get; set; }
    public string? AccentColor { get; set; }

    public ICollection<User> Users { get; set; } = [];
    public ICollection<Department> Departments { get; set; } = [];
    public ICollection<CompanyValue> CompanyValues { get; set; } = [];
    public ICollection<Recognition> Recognitions { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
    public ICollection<Survey> Surveys { get; set; } = [];
    public ICollection<Budget> Budgets { get; set; } = [];
    public ICollection<ApprovalPolicy> ApprovalPolicies { get; set; } = [];
}
