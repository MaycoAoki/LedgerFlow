using Microsoft.EntityFrameworkCore;
using LedgerFlow.Domain.Entities;
using LedgerFlow.Domain.Interfaces;
using LedgerFlow.Infrastructure.Persistence;

namespace LedgerFlow.Infrastructure.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly AppDbContext _context;

    public CategoryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Category>> GetByUserIdAsync(Guid userId, CancellationToken ct)
    {
        return await _context.Categories
            .Where(c => c.UserId == userId)
            .ToListAsync(ct);
    }

    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _context.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task AddAsync(Category category, CancellationToken ct)
    {
        await _context.Categories.AddAsync(category, ct);
    }

    public async Task UpdateAsync(Category category, CancellationToken ct)
    {
        _context.Categories.Update(category);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category != null)
        {
            _context.Categories.Remove(category);
            await Task.CompletedTask;
        }
    }
}
