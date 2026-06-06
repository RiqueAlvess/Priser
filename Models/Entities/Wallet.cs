namespace Priser.Models.Entities;

public class Wallet : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string WalletType { get; set; } = "earned";
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "PTS";

    public ICollection<Transaction> SentTransactions { get; set; } = [];
    public ICollection<Transaction> ReceivedTransactions { get; set; } = [];
}
