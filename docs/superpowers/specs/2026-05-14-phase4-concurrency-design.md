# LedgerFlow — Fase 4: Concorrência

**Date:** 2026-05-14
**Phase:** 4/4
**Prerequisite:** Fase 1 + Fase 2 + Fase 3 completas

---

## 1. Visão Geral

Implementação de optimistic concurrency via RowVersion, sistema de auditoria completo para todas as operações, e proteção contra race conditions em cenários de alta concorrência.

---

## 2. RowVersion — Configuração EF Core

### Entidades

```csharp
public class Account
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal InitialBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsDeleted { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
```

### DbContext Configuration

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder builder)
{
    builder.Conventions.Add(_ => new RowVersionConvention());
}

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.Property(x => x.CurrentBalance).HasPrecision(18, 2);
    }
}
```

---

## 3. Concurrency Handling — API Layer

### Update com Concurrency Token

```csharp
public class UpdateAccountCommand : IRequest<Result<AccountDto>>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class UpdateAccountCommandHandler : IRequestHandler<UpdateAccountCommand, Result<AccountDto>>
{
    public async Task<Result<AccountDto>> Handle(UpdateAccountCommand request, CancellationToken ct)
    {
        var account = await _accountRepository.GetByIdAsync(request.Id, ct);
        if (account == null)
            return Result<AccountDto>.Fail(new Error("not_found", "Account not found"));

        if (account.RowVersion == null || !account.RowVersion.SequenceEqual(request.RowVersion))
            return Result<AccountDto>.Fail(new Error("concurrency_conflict", "The entity was modified by another user"));

        account.Name = request.Name;
        await _unitOfWork.CommitAsync(ct);

        return Result<AccountDto>.Ok(AccountDto.FromEntity(account));
    }
}
```

### Response com RowVersion

```csharp
public record AccountDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public static AccountDto FromEntity(Account account) => new()
    {
        Id = account.Id,
        Name = account.Name,
        Balance = account.CurrentBalance,
        RowVersion = account.RowVersion
    };
}
```

### HTTP Response

- **409 Conflict**: quando RowVersion não corresponde
- **Body**: `{ "type": "https://httpstatuses.com/409", "title": "Conflict", "detail": "Entity was modified by another user" }`

---

## 4. Domain Events — Auditoria

### Events

```csharp
public record EntityModifiedEvent
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Operation { get; set; } = string.Empty; // Created, Updated, Deleted
    public Guid UserId { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object?> OldValues { get; set; } = new();
    public Dictionary<string, object?> NewValues { get; set; } = new();
}
```

### Audit Log Entity

```csharp
public class AuditLog
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateTime Timestamp { get; set; }
    public string? OldValues { get; set; } // JSON
    public string? NewValues { get; set; } // JSON
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
```

### Handler

```csharp
public class EntityModifiedEventHandler : INotificationHandler<EntityModifiedEvent>
{
    private readonly IAuditLogRepository _auditLogRepository;

    public async Task Handle(EntityModifiedEvent notification, CancellationToken ct)
    {
        var log = new AuditLog
        {
            EntityType = notification.EntityType,
            EntityId = notification.EntityId,
            Operation = notification.Operation,
            UserId = notification.UserId,
            Timestamp = notification.Timestamp,
            OldValues = JsonSerializer.Serialize(notification.OldValues),
            NewValues = JsonSerializer.Serialize(notification.NewValues)
        };

        await _auditLogRepository.AddAsync(log, ct);
    }
}
```

---

## 5. Publish Events — Application Layer

### Command Handler com Publish

```csharp
public class UpdateAccountCommandHandler : IRequestHandler<UpdateAccountCommand, Result<AccountDto>>
{
    public async Task<Result<AccountDto>> Handle(UpdateAccountCommand request, CancellationToken ct)
    {
        var oldAccount = await _accountRepository.GetByIdAsync(request.Id, ct);
        // ... update logic ...

        await _unitOfWork.CommitAsync(ct);

        // Publish event for audit
        await _mediator.Publish(new EntityModifiedEvent
        {
            EntityType = nameof(Account),
            EntityId = account.Id,
            Operation = "Updated",
            UserId = request.UserId,
            Timestamp = DateTime.UtcNow,
            OldValues = oldAccount.ToDictionary(),
            NewValues = account.ToDictionary()
        }, ct);

        return Result<AccountDto>.Ok(AccountDto.FromEntity(account));
    }
}
```

### Extension Method para Dictionary

```csharp
public static class EntityExtensions
{
    public static Dictionary<string, object?> ToDictionary(this Account account) => new()
    {
        ["Name"] = account.Name,
        ["InitialBalance"] = account.InitialBalance,
        ["CurrentBalance"] = account.CurrentBalance,
        ["IsDeleted"] = account.IsDeleted
    };
}
```

---

## 6. Audit Log API

### Endpoint

```
GET /api/audit-logs
GET /api/audit-logs/{entityType}/{entityId}
```

### Query

```csharp
public record GetAuditLogsQuery(
    Guid? EntityId,
    string? EntityType,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PagedResult<AuditLogDto>>>;
```

### Response

```csharp
public record AuditLogDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object?> Changes { get; set; } = new();
}
```

---

## 7. Race Condition Protection

### Pessimistic Lock (opcional)

Para cenários críticos (ex: transferência), usar lock no banco:

```csharp
public async Task<Account> GetByIdWithLockAsync(Guid id, CancellationToken ct)
{
    return await _context.Accounts
        .FromSqlRaw("SELECT * FROM accounts WHERE id = {0} FOR UPDATE", id)
        .FirstOrDefaultAsync(ct);
}
```

### Optimistic Lock (padrão)

Para a maioria das operações, o RowVersion já provê proteção automática via EF Core.

---

## 8. Testing

### Concurrency Tests

```csharp
public class AccountConcurrencyTests
{
    [Fact]
    public async Task Update_WithStaleRowVersion_ReturnsConflict()
    {
        // Arrange
        var account = await CreateAccountAsync();
        var staleVersion = account.RowVersion;

        await _context.Accounts.ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Name, "Updated by other"));

        var command = new UpdateAccountCommand(account.Id, "New Name", staleVersion);

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "concurrency_conflict");
    }
}
```

### Audit Log Tests

```csharp
public class AuditLogTests
{
    [Fact]
    public async Task CreateAccount_PublishesAuditEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateAccountCommand("Test", 1000);

        // Act
        await _mediator.Send(command);

        // Assert
        var auditLogs = await _auditLogRepository.GetByEntityIdAsync(command.Result.Value);
        auditLogs.Should().Contain(l => l.Operation == "Created");
    }
}
```

---

## 9. Implementação — Ordem

1. Adicionar RowVersion às entidades (Account, Transaction, Category)
2. Configurar EF Core com optimistic concurrency
3. Atualizar Commands com RowVersion parameter
4. Implementar EntityModifiedEvent + Handler
5. Criar AuditLog entity + repository
6. Adicionar AuditLog API endpoints
7. Atualizar handlers para publish events
8. Implementar concurrency tests

---

## 10. Dependências Novas

```xml
<!-- Nenhuma dependência nova necessária - tudo com EF Core existente -->
```

---

## 11. Critérios de Conclusão

- [ ] RowVersion configurado em Account, Transaction, Category
- [ ] 409 Conflict retornado em updates com RowVersion expirado
- [ ] Audit logs criados para todas as operações
- [ ] API de consulta de audit logs
- [ ] Testes de concorrência passando
- [ ] Build verde

---

## 12. Complete Project Summary

| Fase | Escopo | Status |
|------|--------|--------|
| **1** | MVP (Auth, Accounts, Transactions, Dashboard) | ✓ Spec |
| **2** | CQRS, MediatR, Validators, Tests | ✓ Spec |
| **3** | Jobs (Hangfire), CSV Import, Cache | ✓ Spec |
| **4** | RowVersion, Concurrency, Audit | ✓ Spec |

Todos os 4 specs estão prontos para implementação sequencial.