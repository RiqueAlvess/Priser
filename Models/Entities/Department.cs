namespace Priser.Models.Entities;

public class Department : BaseEntity
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string Name { get; set; } = "";
    public Guid? ParentDepartmentId { get; set; }
    public Department? ParentDepartment { get; set; }

    public ICollection<Department> Children { get; set; } = [];
    public ICollection<User> Users { get; set; } = [];
}
