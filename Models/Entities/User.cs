namespace Priser.Models.Entities;

public class User : BaseEntity
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public string? JobTitle { get; set; }
    public DateTime? HireDate { get; set; }
    public DateTime? BirthDate { get; set; }
    public string Status { get; set; } = "active";
    public Guid? ManagerId { get; set; }
    public User? Manager { get; set; }
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public string FullName => $"{FirstName} {LastName}";

    public ICollection<User> DirectReports { get; set; } = [];
    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<Recognition> SentRecognitions { get; set; } = [];
    public ICollection<Recognition> ReceivedRecognitions { get; set; } = [];
    public ICollection<Wallet> Wallets { get; set; } = [];
    public ICollection<Comment> Comments { get; set; } = [];
    public ICollection<Reaction> Reactions { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
    public ICollection<SurveyResponse> SurveyResponses { get; set; } = [];
    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<Budget> ManagedBudgets { get; set; } = [];
}
