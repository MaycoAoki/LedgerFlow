using Microsoft.EntityFrameworkCore;
using LedgerFlow.Domain.Entities;
using LedgerFlow.Domain.Interfaces;
using LedgerFlow.Infrastructure.Persistence;

namespace LedgerFlow.Infrastructure.Repositories;

public class AccountRepository : IAccountRepository
{
    private readonly AppDbContext _context;

    public AccountRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Account>> GetByUserIdAsync(Guid userId, CancellationToken ct)
    {
        return await _context.Accounts
            .Where(a => a.UserId == userId && !a.IsDeleted)
            .ToListAsync(ct);
    }

    public async Task<Account?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _context.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task AddAsync(Account account, CancellationToken ct)
    {
        await _context.Accounts.AddAsync(account, ct);
    }

    public async Task UpdateAsync(Account account, CancellationToken ct)
    {
        _context.Accounts.Update(account);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account != null)
        {
            account.IsDeleted = true;
            await Task.CompletedTask;
        }
    }
}
