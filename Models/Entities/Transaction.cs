namespace Priser.Models.Entities;

public class Transaction : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid? FromWalletId { get; set; }
    public Wallet? FromWallet { get; set; }
    public Guid? ToWalletId { get; set; }
    public Wallet? ToWallet { get; set; }
    public decimal Amount { get; set; }
    public string TransactionType { get; set; } = "";
    public string? Description { get; set; }
    public Guid? ReferenceId { get; set; }
    public string ReferenceType { get; set; } = "";
    public string Status { get; set; } = "completed";
}
