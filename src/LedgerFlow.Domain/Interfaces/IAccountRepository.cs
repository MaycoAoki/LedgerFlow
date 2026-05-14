using LedgerFlow.Domain.Entities;

namespace LedgerFlow.Domain.Interfaces;

public interface IAccountRepository
{
    Task<List<Account>> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task<Account?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Account account, CancellationToken ct);
    Task UpdateAsync(Account account, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
