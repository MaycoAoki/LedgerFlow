# LedgerFlow — Fase 1: MVP

**Date:** 2026-05-14
**Phase:** 1/4
**Status:** Ready for implementation

---

## 1. Visão Geral

MVP com as funcionalidades essenciais para um sistema de gestão financeira funcional:
- Autenticação (registro, login, refresh token, recuperação de senha)
- Contas financeiras (CRUD completo)
- Transações (receita, despesa, transferência)
- Dashboard com resumo e cache Redis

---

## 2. Estrutura do Projeto

```
LedgerFlow.sln
src/
├── LedgerFlow.Api/           # ASP.NET Core Web API
├── LedgerFlow.Application/    # Commands, Queries, Handlers
├── LedgerFlow.Domain/         # Entities, Interfaces, Events
├── LedgerFlow.Infrastructure/  # EF Core, Redis, Repositories
└── LedgerFlow.Shared/         # Result<T>, Error, PagedResult
```

---

## 3. Domain Layer

### Entities

| Entity | Props |
|--------|-------|
| **User** | Id (Guid), Name, Email, PasswordHash, CreatedAt, RefreshTokens |
| **Account** | Id, UserId, Name, InitialBalance, CurrentBalance, IsDeleted, RowVersion |
| **Transaction** | Id, AccountId, CategoryId?, Amount, Type, TransferToAccountId?, Description, Date |
| **Category** | Id, UserId, Name, Type (Income/Expense) |
| **RefreshToken** | Id, UserId, Token, ExpiresAt, RevokedAt? |

### Enums

- `TransactionType`: Income, Expense, Transfer
- `CategoryType`: Income, Expense

### Repository Interfaces

```csharp
IAccountRepository
ITransactionRepository
ICategoryRepository
IRefreshTokenRepository
IUnitOfWork
```

### Domain Events (declarados, sem handlers no MVP)

- `TransactionCreatedEvent`
- `AccountBalanceChangedEvent`

---

## 4. Shared Layer

```csharp
record Error(string Code, string Message);

class Result<T> {
    bool IsSuccess { get; }
    T? Value { get; }
    IReadOnlyList<Error> Errors { get; }
    static Result<T> Ok(T value);
    static Result<T> Fail(params Error[] errors);
}

class PagedResult<T> {
    IReadOnlyList<T> Items { get; }
    int TotalCount { get; }
    int Page { get; }
    int PageSize { get; }
}
```

---

## 5. Infrastructure Layer

### AppDbContext

```csharp
class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
```

- Configurações via `IEntityTypeConfiguration<T>`
- Migrations em `Persistence/Migrations/`
- Auto-migrate na inicialização

### Redis Cache

```csharp
interface IRedisCacheService {
    Task<T?> GetAsync<T>(string key, CancellationToken ct);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct);
    Task RemoveAsync(string key, CancellationToken ct);
}
```

- Keys: `ledgerflow:{entity}:{id}:{variant}`
- Dashboard: 5min TTL

### Hangfire

- Registrado no DI
- Servidor inicia vazio (jobs vêm na Fase 3)

---

## 6. Application Layer

### Comandos e Queries

```
Auth/
├── RegisterCommand + Handler + Validator
├── LoginCommand + Handler
├── RefreshTokenCommand + Handler
└── ForgotPasswordCommand + Handler

Accounts/
├── CreateAccountCommand + Handler + Validator
├── UpdateAccountCommand + Handler + Validator
├── DeleteAccountCommand + Handler
├── GetAccountByIdQuery + Handler
└── GetAllAccountsQuery + Handler

Transactions/
├── CreateTransactionCommand + Handler + Validator
├── UpdateTransactionCommand + Handler + Validator
├── DeleteTransactionCommand + Handler
├── GetTransactionByIdQuery + Handler
├── GetAllTransactionsQuery + Handler
└── GetTransactionsByAccountQuery + Handler

Dashboard/
└── GetDashboardSummaryQuery + Handler (com cache Redis)
```

### Padrões

- Cada Command/Query em sua própria pasta
- Handlers injetam repositórios + IUnitOfWork (nunca DbContext)
- `ValidationBehavior` como pipeline behavior MediatR
- FluentValidation com `AbstractValidator<TCommand>`

---

## 7. API Layer

### Controllers

| Controller | Endpoints |
|------------|-----------|
| AuthController | POST /api/auth/register, /login, /refresh, /forgot-password |
| AccountsController | CRUD /api/accounts |
| TransactionsController | CRUD /api/transactions |
| DashboardController | GET /api/dashboard/summary |

### Middleware

- `ExceptionHandlingMiddleware`: returns RFC 7807 ProblemDetails

### DI Registration

```csharp
// Extensions/ServiceCollectionExtensions.cs
AddApplication()      // MediatR, FluentValidation, Behaviors
AddInfrastructure()  // EF Core, Redis, Hangfire, Repositories
AddAuth()            // Identity, JWT Bearer
```

### JWT Config

```json
{
  "Jwt": {
    "Secret": "...",
    "Issuer": "ledgerflow",
    "Audience": "ledgerflow-client",
    "ExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  }
}
```

- Algorithm: HS256
- Claims: sub, email, jti

### Auth Flow

1. **Register**: UserManager.CreateAsync() → 201
2. **Login**: CheckPasswordAsync() → JWT + RefreshToken (salvo no DB)
3. **Refresh**: Valida token no DB, revoga antigo, emite novo
4. **Forgot Password**: Gera reset token (log no console no MVP)

---

## 8. Cache Invalidation

After `IUnitOfWork.CommitAsync()` succeeds:

- `CreateTransactionHandler` → Remove `ledgerflow:dashboard:{userId}:summary`
- `UpdateTransactionHandler` → Remove cache
- `DeleteTransactionHandler` → Remove cache
- `CreateAccountHandler` → Remove cache
- `UpdateAccountHandler` → Remove cache
- `DeleteAccountHandler` → Remove cache

---

## 9. Testing Strategy

### Unit Tests

```
tests/LedgerFlow.Unit.Tests/
├── Auth/
├── Accounts/
├── Transactions/
└── Shared/
```

- xUnit + Moq + FluentAssertions
- Happy path, validation failure, not found, domain rules

### Integration Tests

```
tests/LedgerFlow.Integration.Tests/
├── Auth/
├── Accounts/
├── Transactions/
└── Dashboard/
```

- TestContainers (PostgreSQL + Redis)
- WebApplicationFactory<Program>
- Migrations auto-run no setup

---

## 10. Docker Compose

Serviços: api, frontend, db (PostgreSQL 16), redis (7)

- API: porta 5000
- Frontend: porta 4200
- PostgreSQL: porta 5432
- Redis: porta 6379

---

## 11. Implementação

### Ordem

1. Scaffold projetos + NuGet packages
2. Shared → Domain → Infrastructure → Api
3.docker compose up → health check
4. EF Core migrations
5. Auth (CRUD + testes)
6. Accounts (CRUD + testes)
7. Transactions (CRUD + testes)
8. Dashboard (summary + cache + testes)

---

## 12. Out of Scope

- CQRS hardening (Fase 2)
- CSV import (Fase 3)
- Jobs (Fase 3)
- RowVersion enforcement (Fase 4)
- Audit log (Fase 4)
- Email real (MVP: console log)