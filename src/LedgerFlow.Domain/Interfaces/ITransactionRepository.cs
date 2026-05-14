using LedgerFlow.Domain.Entities;

namespace LedgerFlow.Domain.Interfaces;

public interface ITransactionRepository
{
    Task<List<Transaction>> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task<List<Transaction>> GetByAccountIdAsync(Guid accountId, CancellationToken ct);
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Transaction transaction, CancellationToken ct);
    Task UpdateAsync(Transaction transaction, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
