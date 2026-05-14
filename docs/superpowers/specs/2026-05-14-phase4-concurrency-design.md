# LedgerFlow — Fase 4: Concorrência

**Date:** 2026-05-14
**Phase:** 4/4
**Prerequisite:** Fase 1 + Fase 2 + Fase 3 completas
**Arquitetura:** MVC + Services + Repositories (com RowVersion)

---

## 1. Visão Geral

Implementação de optimistic concurrency via RowVersion, sistema de auditoria completo para todas as operações, e proteção contra race conditions em cenários de alta concorrência.

---

## 2. RowVersion — Configuração EF Core

### Entities

```csharp
public class Account
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal InitialBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }
}

public class Transaction
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid? CategoryId { get; set; }
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public Guid? TransferToAccountId { get; set; }
    public string? Description { get; set; }
    public DateTime Date { get; set; }
    public DateTime CreatedAt { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }
}

public class Category
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public CategoryType Type { get; set; }
    public DateTime CreatedAt { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
```

### Configurations

```csharp
public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CurrentBalance).HasPrecision(18, 2);
        builder.Property(x => x.InitialBalance).HasPrecision(18, 2);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId);
    }
}
```

---

## 3. Concurrency Handling — Services

### UpdateAccountRequest (com RowVersion)

```csharp
public record UpdateAccountRequest(Guid Id, string Name, byte[]? RowVersion);
```

### AccountService com Concurrency Check

```csharp
public class AccountService : IAccountService
{
    private readonly IAccountRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly ILoggerService _logger;

    public async Task<Result> UpdateAsync(UpdateAccountRequest request, Guid userId, CancellationToken ct)
    {
        _logger.LogInformation("Updating account {AccountId}", request.Id);

        var account = await _repository.GetByIdAsync(request.Id, ct);
        if (account == null || account.UserId != userId)
            return Result.Fail(new Error("not_found", "Account not found"));

        // Concurrency check
        if (request.RowVersion != null && account.RowVersion != null)
        {
            if (!account.RowVersion.SequenceEqual(request.RowVersion))
            {
                _logger.LogWarning("Concurrency conflict for account {AccountId}", request.Id);
                return Result.Fail(new Error("concurrency_conflict", "The entity was modified by another user. Please refresh and try again."));
            }
        }

        account.Name = request.Name;
        account.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _repository.UpdateAsync(account, ct);
            await _unitOfWork.CommitAsync(ct);

            await _cache.RemoveAsync($"ledgerflow:dashboard:{userId}:summary", ct);

            _logger.LogInformation("Account {AccountId} updated successfully", request.Id);
            return Result.Ok();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency exception updating account {AccountId}", request.Id);
            return Result.Fail(new Error("concurrency_conflict", "The entity was modified by another user. Please refresh and try again."));
        }
    }
}
```

---

## 4. DTOs com RowVersion

```csharp
public record AccountDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public byte[]? RowVersion { get; set; }
    public DateTime CreatedAt { get; set; }

    public static AccountDto FromEntity(Account account) => new()
    {
        Id = account.Id,
        Name = account.Name,
        Balance = account.CurrentBalance,
        RowVersion = account.RowVersion,
        CreatedAt = account.CreatedAt
    };
}

public record TransactionDto
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime Date { get; set; }
    public byte[]? RowVersion { get; set; }
}
```

---

## 5. Auditoria — Entity

```csharp
public class AuditLog
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Operation { get; set; } = string.Empty; // Created, Updated, Deleted
    public Guid UserId { get; set; }
    public DateTime Timestamp { get; set; }
    public string? OldValues { get; set; } // JSON
    public string? NewValues { get; set; } // JSON
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
```

### Repository Interface

```csharp
public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log, CancellationToken ct);
    Task<List<AuditLog>> GetByEntityIdAsync(Guid entityId, CancellationToken ct);
    Task<List<AuditLog>> GetByUserIdAsync(Guid userId, int page, int pageSize, CancellationToken ct);
    Task<List<AuditLog>> GetByDateRangeAsync(DateTime from, DateTime to, int page, int pageSize, CancellationToken ct);
}
```

---

## 6. Auditoria — Service

```csharp
public interface IAuditService
{
    Task<Result<List<AuditLogDto>>> GetByEntityIdAsync(Guid entityId, CancellationToken ct);
    Task<Result<PagedResult<AuditLogDto>>> GetByUserIdAsync(Guid userId, int page, int pageSize, CancellationToken ct);
}

public class AuditService : IAuditService
{
    private readonly IAuditLogRepository _repository;

    public async Task<Result<List<AuditLogDto>>> GetByEntityIdAsync(Guid entityId, CancellationToken ct)
    {
        var logs = await _repository.GetByEntityIdAsync(entityId, ct);
        return Result<List<AuditLogDto>>.Ok(logs.Select(AuditLogDto.FromEntity).ToList());
    }
}
```

### AuditLogDto

```csharp
public record AuditLogDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object?>? Changes { get; set; }

    public static AuditLogDto FromEntity(AuditLog log) => new()
    {
        Id = log.Id,
        EntityType = log.EntityType,
        EntityId = log.EntityId,
        Operation = log.Operation,
        Timestamp = log.Timestamp,
        Changes = ParseJsonToDictionary(log.NewValues)
    };

    private static Dictionary<string, object?>? ParseJsonToDictionary(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
    }
}
```

---

## 7. Integrando Auditoria nos Services

### Base Service com Auditoria

```csharp
public abstract class BaseService
{
    protected readonly IAuditLogRepository _auditLogRepository;
    protected readonly ILoggerService _logger;

    protected async Task AuditAsync(string entityType, Guid entityId, string operation, object? oldValues, object? newValues, Guid userId, CancellationToken ct)
    {
        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            Operation = operation,
            UserId = userId,
            Timestamp = DateTime.UtcNow,
            OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
            NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null
        };

        await _auditLogRepository.AddAsync(log, ct);
    }
}

public class AccountService : BaseService, IAccountService
{
    public async Task<Result> UpdateAsync(UpdateAccountRequest request, Guid userId, CancellationToken ct)
    {
        var oldAccount = await _repository.GetByIdAsync(request.Id, ct);
        // ... update logic
        await _unitOfWork.CommitAsync(ct);

        await AuditAsync(nameof(Account), request.Id, "Updated", oldAccount, account, userId, ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var account = await _repository.GetByIdAsync(id, ct);
        // ... delete logic
        await _unitOfCommitAsync(ct);

        await AuditAsync(nameof(Account), id, "Deleted", account, null, userId, ct);
        return Result.Ok();
    }
}
```

---

## 8. Audit Log API

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditService _auditService;

    [HttpGet("entity/{entityId}")]
    public async Task<IActionResult> GetByEntity(Guid entityId, CancellationToken ct)
    {
        var result = await _auditService.GetByEntityIdAsync(entityId, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Errors);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _auditService.GetByUserIdAsync(userId, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Errors);
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
}
```

---

## 9. Race Condition Protection

### Pessimistic Lock (para transferências)

```csharp
public interface IAccountRepository
{
    Task<Account?> GetByIdWithLockAsync(Guid id, CancellationToken ct);
}

public class AccountRepository : IAccountRepository
{
    private readonly AppDbContext _context;

    public async Task<Account?> GetByIdWithLockAsync(Guid id, CancellationToken ct)
    {
        return await _context.Accounts
            .FromSqlRaw("SELECT * FROM accounts WHERE id = {0} FOR UPDATE", id)
            .FirstOrDefaultAsync(ct);
    }
}

// No TransactionService
public async Task<Result<Guid>> CreateAsync(CreateTransactionRequest request, Guid userId, CancellationToken ct)
{
    if (request.Type == TransactionType.Transfer)
    {
        var sourceAccount = await _accountRepository.GetByIdWithLockAsync(request.AccountId, ct);
        var destAccount = await _accountRepository.GetByIdWithLockAsync(request.TransferToAccountId!.Value, ct);

        // Transfer logic with lock
    }
}
```

---

## 10. Testes de Concorrência

```csharp
public class AccountConcurrencyTests
{
    [Fact]
    public async Task UpdateAsync_WithStaleRowVersion_ReturnsConflict()
    {
        // Arrange
        var account = await CreateTestAccountAsync();
        var staleVersion = account.RowVersion;

        // Simulate another user updating
        await _context.Accounts.ExecuteUpdateAsync(s =>
            s.SetProperty(x => x.Name, "Updated by other"));

        var request = new UpdateAccountRequest(account.Id, "New Name", staleVersion);

        // Act
        var result = await _service.UpdateAsync(request, userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "concurrency_conflict");
    }

    [Fact]
    public async Task UpdateAsync_WithValidRowVersion_Succeeds()
    {
        // Arrange
        var account = await CreateTestAccountAsync();
        var currentVersion = account.RowVersion;

        var request = new UpdateAccountRequest(account.Id, "New Name", currentVersion);

        // Act
        var result = await _service.UpdateAsync(request, userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}
```

### Audit Tests

```csharp
public class AuditServiceTests
{
    [Fact]
    public async Task UpdateAccount_CreatesAuditLog()
    {
        // Arrange
        var account = await CreateTestAccountAsync();
        var request = new UpdateAccountRequest(account.Id, "New Name", account.RowVersion);

        // Act
        await _accountService.UpdateAsync(request, userId, CancellationToken.None);

        // Assert
        var auditLogs = await _auditService.GetByEntityIdAsync(account.Id, CancellationToken.None);
        auditLogs.Value.Should().Contain(l => l.Operation == "Updated");
    }
}
```

---

## 11. Implementação — Ordem

1. Adicionar RowVersion às entities (Account, Transaction, Category)
2. Configurar EF Core com optimistic concurrency
3. Atualizar Update requests com RowVersion parameter
4. Implementar concurrency check nos Services
5. Criar AuditLog entity + repository
6. Implementar IAuditService
7. Adicionar AuditLogsController
8. Integrar auditoria nos Services
9. Implementar pessimistic lock para transferências
10. Implementar testes de concorrência

---

## 12. Critérios de Conclusão

- [ ] RowVersion configurado em Account, Transaction, Category
- [ ] 409 Conflict retornado em updates com RowVersion expirado
- [ ] Audit logs criados para todas as operações
- [ ] API de consulta de audit logs funcionando
- [ ] Testes de concorrência passando
- [ ] Build verde

---

## 13. Projeto Completo — Resumo

| Fase | Escopo | Arquitetura |
|------|--------|--------------|
| **1** | MVP (Auth, Accounts, Transactions, Dashboard) | MVC + Services + Repositories |
| **2** | FluentValidation, Logging, Testes | Expansão dos Services |
| **3** | Hangfire Jobs, CSV Import, Cache | Background Jobs + Services |
| **4** | RowVersion, Concurrency, Audit | Optimistic Concurrency + Audit |