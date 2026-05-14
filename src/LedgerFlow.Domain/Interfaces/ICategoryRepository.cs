using LedgerFlow.Domain.Entities;

namespace LedgerFlow.Domain.Interfaces;

public interface ICategoryRepository
{
    Task<List<Category>> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task<Category?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Category category, CancellationToken ct);
    Task UpdateAsync(Category category, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
