using LedgerFlow.Domain.Entities;

namespace LedgerFlow.Domain.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct);
    Task AddAsync(RefreshToken refreshToken, CancellationToken ct);
    Task RevokeAsync(string token, CancellationToken ct);
}
