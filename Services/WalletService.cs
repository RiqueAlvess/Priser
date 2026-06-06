using Microsoft.EntityFrameworkCore;
using Priser.Data;
using Priser.Models.Entities;

namespace Priser.Services;

public class WalletService(AppDbContext db)
{
    public async Task<Wallet> GetOrCreateWalletAsync(Guid userId, Guid tenantId, string type = "earned")
    {
        var wallet = await db.Wallets
            .FirstOrDefaultAsync(w => w.UserId == userId && w.WalletType == type);

        if (wallet != null) return wallet;

        wallet = new Wallet
        {
            UserId = userId,
            TenantId = tenantId,
            WalletType = type,
            Balance = 0
        };
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();
        return wallet;
    }

    public async Task<bool> TransferPointsAsync(
        Guid fromUserId, Guid toUserId, Guid tenantId,
        decimal amount, string txType, Guid? referenceId = null, string refType = "")
    {
        var fromWallet = await GetOrCreateWalletAsync(fromUserId, tenantId, "allowance");
        var toWallet = await GetOrCreateWalletAsync(toUserId, tenantId, "earned");

        if (fromWallet.Balance < amount) return false;

        fromWallet.Balance -= amount;
        toWallet.Balance += amount;

        db.Transactions.Add(new Transaction
        {
            TenantId = tenantId,
            FromWalletId = fromWallet.Id,
            ToWalletId = toWallet.Id,
            Amount = amount,
            TransactionType = txType,
            ReferenceId = referenceId,
            ReferenceType = refType,
            Status = "completed"
        });

        await db.SaveChangesAsync();
        return true;
    }

    public async Task CreditPointsAsync(Guid userId, Guid tenantId, decimal amount, string txType, string description = "")
    {
        var wallet = await GetOrCreateWalletAsync(userId, tenantId, "earned");
        wallet.Balance += amount;

        db.Transactions.Add(new Transaction
        {
            TenantId = tenantId,
            ToWalletId = wallet.Id,
            Amount = amount,
            TransactionType = txType,
            Description = description,
            Status = "completed"
        });

        await db.SaveChangesAsync();
    }

    public async Task<bool> DebitPointsAsync(Guid userId, Guid tenantId, decimal amount, string txType, Guid? referenceId = null)
    {
        var wallet = await GetOrCreateWalletAsync(userId, tenantId, "earned");
        if (wallet.Balance < amount) return false;

        wallet.Balance -= amount;
        db.Transactions.Add(new Transaction
        {
            TenantId = tenantId,
            FromWalletId = wallet.Id,
            Amount = amount,
            TransactionType = txType,
            ReferenceId = referenceId,
            Status = "completed"
        });

        await db.SaveChangesAsync();
        return true;
    }
}
