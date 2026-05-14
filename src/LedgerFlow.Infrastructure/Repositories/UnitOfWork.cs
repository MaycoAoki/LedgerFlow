using LedgerFlow.Domain.Interfaces;
using LedgerFlow.Infrastructure.Persistence;

namespace LedgerFlow.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int> CommitAsync(CancellationToken ct)
    {
        return await _context.SaveChangesAsync(ct);
    }
}
