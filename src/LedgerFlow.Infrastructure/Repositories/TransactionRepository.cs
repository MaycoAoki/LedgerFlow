using Microsoft.EntityFrameworkCore;
using LedgerFlow.Domain.Entities;
using LedgerFlow.Domain.Interfaces;
using LedgerFlow.Infrastructure.Persistence;

namespace LedgerFlow.Infrastructure.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly AppDbContext _context;

    public TransactionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Transaction>> GetByUserIdAsync(Guid userId, CancellationToken ct)
    {
        return await _context.Transactions
            .Include(t => t.Account)
            .Where(t => t.Account!.UserId == userId)
            .OrderByDescending(t => t.Date)
            .ToListAsync(ct);
    }

    public async Task<List<Transaction>> GetByAccountIdAsync(Guid accountId, CancellationToken ct)
    {
        return await _context.Transactions
            .Where(t => t.AccountId == accountId)
            .OrderByDescending(t => t.Date)
            .ToListAsync(ct);
    }

    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _context.Transactions.FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task AddAsync(Transaction transaction, CancellationToken ct)
    {
        await _context.Transactions.AddAsync(transaction, ct);
    }

    public async Task UpdateAsync(Transaction transaction, CancellationToken ct)
    {
        _context.Transactions.Update(transaction);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var transaction = await _context.Transactions.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (transaction != null)
        {
            _context.Transactions.Remove(transaction);
            await Task.CompletedTask;
        }
    }
}
