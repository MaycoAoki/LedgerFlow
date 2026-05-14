# LedgerFlow — Fase 2: Arquitetura Enterprise

**Date:** 2026-05-14
**Phase:** 2/4
**Prerequisite:** Fase 1 (MVP) completa

---

## 1. Visão Geral

Consolidação da arquitetura CQRS com separação completa de Commands e Queries, endurecimento de validação com FluentValidation, e estratégia de testes completa (unit + integration).

---

## 2. CQRS — Read/Write Separation

### Antes (Fase 1)

- Same model para leitura e escrita
- handlers retornam a mesma entity com todos os campos

### Depois (Fase 2)

**Commands** (escrita) → recebem DTOs, retornam `Result<T>` com IDs ou status
**Queries** (leitura) → recebem DTOs, retornam `Result<T>` com projection DTOs

### Exemplo: Account

```csharp
// Command
record CreateAccountCommand(string Name, decimal InitialBalance) : IRequest<Result<Guid>>;

// Query
record GetAccountDetailQuery(Guid Id) : IRequest<Result<AccountDetailDto>>;
record GetAccountsListQuery(int Page, int PageSize) : IRequest<Result<PagedResult<AccountListDto>>>;

// DTOs
record AccountDetailDto(Guid Id, string Name, decimal Balance, DateTime CreatedAt);
record AccountListDto(Guid Id, string Name, decimal Balance);
```

---

## 3. DTOs Layer

**Location:** `src/LedgerFlow.Application/DTOs/`

```
DTOs/
├── Auth/
│   ├── RegisterRequest.cs
│   ├── LoginRequest.cs
│   ├── AuthResponse.cs
│   └── RefreshTokenRequest.cs
├── Accounts/
│   ├── CreateAccountRequest.cs
│   ├── UpdateAccountRequest.cs
│   ├── AccountDetailDto.cs
│   └── AccountListDto.cs
├── Transactions/
│   ├── CreateTransactionRequest.cs
│   ├── UpdateTransactionRequest.cs
│   ├── TransactionDetailDto.cs
│   └── TransactionListDto.cs
├── Dashboard/
│   └── DashboardSummaryDto.cs
└── Common/
    └── PaginationRequest.cs
```

---

## 4. MediatR — Pipeline Refinements

### Current (Fase 1)

- `ValidationBehavior` — executa FluentValidation antes do handler

### New (Fase 2)

```csharp
// LoggingBehavior — log request/response
class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly ILogger<TRequest> _logger;
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next) {
        _logger.LogInformation("Handling {RequestType}", typeof(TRequest).Name);
        var response = await next();
        _logger.LogInformation("Handled {ResponseType}", typeof(TResponse).Name);
        return response;
    }
}

// TransactionBehavior — envolvimento UoW
class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next) {
        var result = await next();
        await _unitOfWork.CommitAsync();
        return result;
    }
}

// PerformanceBehavior — métricas de performance
class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next) {
        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();
        if (sw.ElapsedMilliseconds > 500) {
            _logger.LogWarning("Long running request: {RequestType} took {Elapsed}ms",
                typeof(TRequest).Name, sw.ElapsedMilliseconds);
        }
        return response;
    }
}
```

### Pipeline Order (before handlers)

1. `ValidationBehavior` — 1º
2. `LoggingBehavior` — 2º
3. `PerformanceBehavior` — 3º
4. `TransactionBehavior` — 4º (envolve o handler)
5. Handler

---

## 5. FluentValidation — Refinements

### Custom Validators

```csharp
// Exemplo: AccountValidator
public class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Account name is required")
            .MaximumLength(100).WithMessage("Account name cannot exceed 100 characters");

        RuleFor(x => x.InitialBalance)
            .GreaterThanOrEqualTo(0).WithMessage("Initial balance cannot be negative")
            .LessThanOrEqualTo(999999999).WithMessage("Initial balance exceeds maximum allowed");
    }
}

// Exemplo: TransactionValidator com regras de domínio
public class CreateTransactionCommandValidator : AbstractValidator<CreateTransactionCommand>
{
    public CreateTransactionCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid transaction type");

        RuleFor(x => x)
            .Must(x => x.Type != TransactionType.Transfer || x.TransferToAccountId.HasValue)
            .WithMessage("Transfer transactions require a destination account");

        RuleFor(x => x)
            .Must(x => x.Type != TransactionType.Transfer || x.AccountId != x.TransferToAccountId)
            .WithMessage("Cannot transfer to the same account");
    }
}
```

### Validator Locations

```
Application/
├── Auth/
│   └── Commands/
│       ├── Register/
│       │   └── RegisterCommandValidator.cs
│       └── Login/
│           └── LoginCommandValidator.cs
├── Accounts/
│   └── Commands/
│       ├── CreateAccount/
│       │   └── CreateAccountCommandValidator.cs
│       └── UpdateAccount/
│           └── UpdateAccountCommandValidator.cs
└── Transactions/
    └── Commands/
        ├── CreateTransaction/
        │   └── CreateTransactionCommandValidator.cs
        └── UpdateTransaction/
            └── UpdateTransactionCommandValidator.cs
```

---

## 6. Domain Events — Handlers

Agora que a infraestrutura existe (Fase 1), vamos implementar handlers para os eventos declarados:

```csharp
// TransactionCreatedEventHandler
public class TransactionCreatedEventHandler : INotificationHandler<TransactionCreatedEvent>
{
    private readonly IAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;

    public async Task Handle(TransactionCreatedEvent notification, CancellationToken ct)
    {
        var account = await _accountRepository.GetByIdAsync(notification.Transaction.AccountId, ct);
        if (account == null) return;

        account.CurrentBalance += notification.Transaction.Type switch
        {
            TransactionType.Income => notification.Transaction.Amount,
            TransactionType.Expense => -notification.Transaction.Amount,
            TransactionType.Transfer => 0, // handled separately
            _ => 0
        };

        await _unitOfWork.CommitAsync(ct);
    }
}

// AccountBalanceChangedEventHandler — para auditoria futura
public class AccountBalanceChangedEventHandler : INotificationHandler<AccountBalanceChangedEvent>
{
    // Fase 4: publish to audit log
}
```

### Register Events

```csharp
// Program.cs
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CreateAccountCommand).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
});
```

---

## 7. Testes — Estratégia Expandida

### Unit Tests

```
tests/LedgerFlow.Unit.Tests/
├── Auth/
│   ├── RegisterCommandTests.cs
│   ├── LoginCommandTests.cs
│   └── RefreshTokenCommandTests.cs
├── Accounts/
│   ├── CreateAccountCommandTests.cs
│   ├── UpdateAccountCommandTests.cs
│   ├── DeleteAccountCommandTests.cs
│   └── AccountValidatorsTests.cs
├── Transactions/
│   ├── CreateTransactionCommandTests.cs
│   ├── TransferTransactionTests.cs
│   └── TransactionValidatorsTests.cs
├── Domain/
│   ├── AccountTests.cs (domain logic)
│   ├── TransactionTests.cs
│   └── EntityTests.cs
├── Shared/
│   ├── ResultTests.cs
│   └── PagedResultTests.cs
└── Behaviors/
    ├── ValidationBehaviorTests.cs
    └── LoggingBehaviorTests.cs
```

#### Coverage Targets

- **Commands**: happy path, validation failures, domain rule violations, not found
- **Validators**: each validation rule, edge cases
- **Domain**: entity methods, invariants
- **Behaviors**: pipeline execution order

### Integration Tests

```
tests/LedgerFlow.Integration.Tests/
├── Infrastructure/
│   ├── TestWebApplicationFactory.cs
│   └── DatabaseFixture.cs
├── Auth/
│   ├── RegisterEndpointTests.cs
│   ├── LoginEndpointTests.cs
│   └── RefreshTokenEndpointTests.cs
├── Accounts/
│   ├── CreateAccountEndpointTests.cs
│   ├── GetAccountsEndpointTests.cs
│   ├── UpdateAccountEndpointTests.cs
│   └── DeleteAccountEndpointTests.cs
├── Transactions/
│   ├── CreateTransactionEndpointTests.cs
│   ├── TransferEndpointTests.cs
│   └── GetTransactionsEndpointTests.cs
├── Dashboard/
│   └── DashboardSummaryEndpointTests.cs
└── Middleware/
    └── ExceptionHandlingTests.cs
```

#### TestContainers Setup

```csharp
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().Build();
    private readonly RedisContainer _redis = new RedisBuilder().Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(sp => sp.GetServiceKind() == typeof(DbContextOptions<AppDbContext>));
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));

            services.RemoveAll(sp => sp.GetServiceKind() == typeof(IConnectionMultiplexer));
            services.AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect(_redis.GetConnectionString()));
        });
    }
}
```

---

## 8. Test Helpers

```csharp
// TestHelpers/ResultAssertions.cs
public static class ResultAssertions
{
    public static void ShouldBeSuccess<T>(Result<T> result)
    {
        result.IsSuccess.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    public static void ShouldBeFailure<T>(Result<T> result)
    {
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    public static void ShouldHaveError<T>(Result<T> result, string code)
    {
        result.Errors.Should().Contain(e => e.Code == code);
    }
}

// TestHelpers/FakeDataGenerator.cs
public static class FakeData
{
    public static User CreateUser() => new User { ... };
    public static Account CreateAccount(Guid userId) => new Account { ... };
    public static Transaction CreateTransaction(Guid accountId) => new Transaction { ... };
}
```

---

## 9. Implementação — Ordem

1. Criar pasta DTOs com todos os request/response objects
2. Separar Commands (write) de Queries (read) em pastas distintas
3. Criar pipeline behaviors (Logging, Performance, Transaction)
4. Implementar handlers de Domain Events
5. Expandir FluentValidation com regras de domínio
6. Adicionar testes unitários para cada handler
7. Adicionar testes de integração para endpoints
8. Configurar TestContainers

---

## 10. Out of Scope

- CSV import (Fase 3)
- Hangfire jobs (Fase 3)
- RowVersion enforcement (Fase 4)
- Audit log (Fase 4)

---

## 11. Dependências Novas

```xml
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
<PackageReference Include="Moq" Version="4.20.70" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
<PackageReference Include="Testcontainers" Version="3.7.0" />
<PackageReference Include="Testcontainers.PostgreSql" Version="3.7.0" />
<PackageReference Include="Testcontainers.Redis" Version="3.7.0" />
```

---

## 12. Critérios de Conclusão

- [ ] Read/Write models completamente separados
- [ ] 4 pipeline behaviors funcionando
- [ ] Domain events com handlers
- [ ]Validators com regras de domínio
- [ ] 80%+ coverage em unit tests
- [ ] Integration tests passando com TestContainers
- [ ] Build verde em CI