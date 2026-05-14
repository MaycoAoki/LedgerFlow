using Microsoft.EntityFrameworkCore;
using LedgerFlow.Domain.Entities;
using LedgerFlow.Domain.Interfaces;
using LedgerFlow.Infrastructure.Persistence;

namespace LedgerFlow.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _context;

    public RefreshTokenRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct)
    {
        return await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == token && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow, ct);
    }

    public async Task AddAsync(RefreshToken refreshToken, CancellationToken ct)
    {
        await _context.RefreshTokens.AddAsync(refreshToken, ct);
    }

    public async Task RevokeAsync(string token, CancellationToken ct)
    {
        var refreshToken = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == token, ct);
        if (refreshToken != null)
        {
            refreshToken.RevokedAt = DateTime.UtcNow;
            await Task.CompletedTask;
        }
    }
}
