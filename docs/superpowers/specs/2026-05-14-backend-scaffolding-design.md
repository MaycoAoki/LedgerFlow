# LedgerFlow Backend — Scaffolding & MVP Design

**Date:** 2026-05-14  
**Scope:** Backend only (C# / ASP.NET Core)  
**Approach:** Option C — Foundation first, then vertical slices  

---

## 1. Scope

This document covers the scaffolding of the LedgerFlow backend and the complete implementation of Phase 1 (MVP) from the project roadmap. The MVP includes:

- Authentication (Register, Login, Refresh Token, Forgot Password)
- Accounts (CRUD)
- Transactions (Income, Expense, Transfer)
- Dashboard (summary with caching)

Phase 2 (Enterprise Architecture), Phase 3 (async jobs/CSV), and Phase 4 (optimistic concurrency/audit) are out of scope here but the architecture is designed to accommodate them.

---

## 2. Solution Structure

```
LedgerFlow.sln
src/
├── LedgerFlow.Api/                  # ASP.NET Core Web API — entry point, controllers, middleware, DI
├── LedgerFlow.Application/          # DTOs, Request/Response models, Interfaces de Services
├── LedgerFlow.Domain/               # Entities, Enums, Repository Interfaces
├── LedgerFlow.Infrastructure/       # EF Core DbContext, Repositories, Redis
└── LedgerFlow.Shared/               # Result<T>, Error — no internal dependencies

tests/
├── LedgerFlow.Unit.Tests/           # Services e Validators (xUnit + Moq + FluentAssertions)
└── LedgerFlow.Integration.Tests/    # Endpoint tests (TestContainers + WebApplicationFactory)
```

**Dependency graph (no cycles):**
```
Api → Application → Domain ← Infrastructure
Api → Infrastructure
Api → Shared ← Application ← Infrastructure
Domain → Shared
```

`Domain` and `Shared` have no dependency on any other internal project.

---

## 3. Domain Layer

**Location:** `src/LedgerFlow.Domain/`

### Entities

```
Entities/
├── User.cs          — extends IdentityUser<Guid>, adds Name, CreatedAt, RefreshTokens
├── Account.cs       — Id, UserId, Name, InitialBalance, CurrentBalance, IsDeleted, RowVersion
├── Transaction.cs   — Id, AccountId, CategoryId?, Amount, Type, TransferToAccountId?, Description, Date
├── Category.cs      — Id, UserId, Name, Type (Income|Expense)
└── RefreshToken.cs  — Id, UserId, Token, ExpiresAt, RevokedAt?
```

**Enums:** `TransactionType` (Income, Expense, Transfer), `CategoryType` (Income, Expense)

`RowVersion` on `Account` is declared but not enforced until Phase 4.

### Repository Interfaces

```
Interfaces/
├── IAccountRepository.cs
├── ITransactionRepository.cs
├── ICategoryRepository.cs
├── IRefreshTokenRepository.cs
└── IUnitOfWork.cs
```

All methods are async. Repositories expose domain-relevant queries (e.g., `GetByUserIdAsync`, `GetByAccountIdAsync`). No `IQueryable` leaks into Application.

### Domain Events

```
Events/
├── TransactionCreatedEvent.cs
└── AccountBalanceChangedEvent.cs
```

Events are declared and published via MediatR `INotification`, but have no handlers in the MVP. Handlers will be added in Phase 2.

---

## 4. Shared Layer

**Location:** `src/LedgerFlow.Shared/`

```csharp
record Error(string Code, string Message);

class Result<T>
{
    bool IsSuccess { get; }
    T? Value { get; }
    IReadOnlyList<Error> Errors { get; }

    static Result<T> Ok(T value);
    static Result<T> Fail(params Error[] errors);
    static Result<T> Fail(IEnumerable<Error> errors);
}

class PagedResult<T>
{
    IReadOnlyList<T> Items { get; }
    int TotalCount { get; }
    int Page { get; }
    int PageSize { get; }
}
```

All Application handlers return `Result<T>`. The API layer maps `Result` to HTTP status codes:

| Scenario | HTTP status |
|---|---|
| Query success | 200 OK |
| Command that creates | 201 Created |
| Validation failure (`IsSuccess = false`, validation errors) | 400 Bad Request |
| Not found | 404 Not Found |
| Conflict (e.g., duplicate email) | 409 Conflict |
| Unhandled exception (middleware) | 500 + ProblemDetails |

---

## 5. Infrastructure Layer

**Location:** `src/LedgerFlow.Infrastructure/`

### EF Core

- `AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>`
- Entity configurations via `IEntityTypeConfiguration<T>` in `Persistence/Configurations/`
- Migrations in `Persistence/Migrations/`
- Applied automatically on startup via `context.Database.MigrateAsync()`

### Repositories

One class per interface in `Domain/Interfaces/`. Each repository receives `AppDbContext` via constructor injection.

### Unit of Work

```csharp
class UnitOfWork : IUnitOfWork
{
    Task<int> CommitAsync(CancellationToken ct);
}
```

Wraps `AppDbContext.SaveChangesAsync()`.

### Redis

```csharp
interface IRedisCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct);
    Task RemoveAsync(string key, CancellationToken ct);
}
```

Implementation uses `IConnectionMultiplexer`. Keys follow the pattern `ledgerflow:{entity}:{id}:{variant}`.

### Hangfire

Registered via `AddHangfire()` + `AddHangfireServer()` in DI. No concrete jobs in the MVP. The server starts but the queue is empty until Phase 3.

---

## 6. Application Layer (Services)

**Location:** `src/LedgerFlow.Application/`

### Structure

```
Application/
├── DTOs/                    # Request e Response models
├── Interfaces/             # Interfaces dos Services
│   ├── IAuthService.cs
│   ├── IAccountService.cs
│   ├── ITransactionService.cs
│   └── IDashboardService.cs
└── Services/               # Implementações
    ├── AuthService.cs
    ├── AccountService.cs
    ├── TransactionService.cs
    └── DashboardService.cs
```

### Conventions

- Services seguem o padrão de injeção de dependência.
- Cada Service recebe as interfaces de Repository e IUnitOfWork via construtor.
- Validação básica feita diretamente nos Services (FluentValidation será adicionado na Fase 2).
- Todos os Services retornam `Result<T>` para tratamento consistente de erros.

### Dashboard caching

`DashboardService.GetSummaryAsync` tenta `IRedisCacheService.GetAsync<DashboardSummary>(key)` primeiro. No caso de cache miss, consulta o DB e chama `SetAsync` com TTL de 5 minutos. Padrão de chave: `ledgerflow:dashboard:{userId}:summary`.

Invalidation de cache: Os métodos Create/Update/Delete de Transactions e Accounts chamam `IRedisCacheService.RemoveAsync(key)` após o `IUnitOfWork.CommitAsync()` ter sucesso.

---

## 7. API Layer

**Location:** `src/LedgerFlow.Api/`

### Controllers

```
Controllers/
├── AuthController.cs        → POST /api/auth/register|login|refresh|forgot-password
├── AccountsController.cs    → CRUD /api/accounts
├── TransactionsController.cs → CRUD /api/transactions
└── DashboardController.cs   → GET /api/dashboard/summary
```

All controllers are `[ApiController]`, receive the appropriate Service interface via constructor injection, call the service method, and map `Result<T>` to HTTP responses.

### Middleware

```
Middleware/
└── ExceptionHandlingMiddleware.cs  → catches unhandled exceptions, returns RFC 7807 ProblemDetails
```

### DI Registration

Feature-based extension methods in `Extensions/ServiceCollectionExtensions.cs`:
- `AddApplication()` — registers Services (IAuthService, IAccountService, etc.)
- `AddInfrastructure(config)` — registers EF Core, Redis, repositories, UoW
- `AddAuth(config)` — registers Identity, JWT Bearer

`Program.cs` calls these and is kept minimal.

### JWT Configuration

```json
"Jwt": {
  "Secret": "...",
  "Issuer": "ledgerflow",
  "Audience": "ledgerflow",
  "ExpiryMinutes": 15,
  "RefreshTokenExpiryDays": 7
}
```

Algorithm: `HS256`. Claims: `sub` (userId), `email`, `jti` (unique token id).

### Auth Flow

1. **Register** — `UserManager.CreateAsync()`, returns 201 with user id.
2. **Login** — `UserManager.CheckPasswordAsync()`, generates JWT + RefreshToken (stored in DB), returns both.
3. **Refresh** — validates token in DB (not expired, not revoked), revokes old token, issues new pair.
4. **Forgot Password** — `UserManager.GeneratePasswordResetTokenAsync()`, token logged to console in MVP (email service is a placeholder).

---

## 8. Testing Strategy

### Unit Tests (`LedgerFlow.Unit.Tests/`)

Tools: `xUnit`, `Moq`, `FluentAssertions`

```
Unit.Tests/
├── Services/     → AuthServiceTests, AccountServiceTests, TransactionServiceTests
├── Validators/   → RegisterRequestValidatorTests (Fase 2)
└── Shared/       → ResultTests
```

Each test class covers: happy path, validation failure, not found, domain rule violation.

### Integration Tests (`LedgerFlow.Integration.Tests/`)

Tools: `TestContainers` (PostgreSQL + Redis), `WebApplicationFactory<Program>`, `HttpClient`

```
Integration.Tests/
├── Infrastructure/
│   └── TestWebApplicationFactory.cs  → overrides connection strings with TestContainers
├── Auth/         → RegisterEndpointTests, LoginEndpointTests, RefreshEndpointTests
├── Accounts/     → AccountsEndpointTests (full CRUD)
├── Transactions/ → TransactionsEndpointTests
└── Dashboard/    → DashboardSummaryTests
```

Each test class creates isolated containers. Migrations run automatically via `MigrateAsync()` in the test factory setup.

---

## 9. Docker Compose

```yaml
version: "3.9"
services:
  api:
    build:
      context: .
      dockerfile: src/LedgerFlow.Api/Dockerfile
    ports: ["5000:8080"]
    environment:
      - ConnectionStrings__Default=Host=postgres;Database=ledgerflow;Username=ledger;Password=secret
      - Redis__ConnectionString=redis:6379
      - Jwt__Secret=dev-secret-min-32-chars-replace-in-prod
    depends_on: [postgres, redis]

  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: ledgerflow
      POSTGRES_USER: ledger
      POSTGRES_PASSWORD: secret
    ports: ["5432:5432"]
    volumes: [postgres_data:/var/lib/postgresql/data]

  redis:
    image: redis:7-alpine
    ports: ["6379:6379"]

  hangfire-worker:
    build:
      context: .
      dockerfile: src/LedgerFlow.Api/Dockerfile
    command: ["--worker"]   # Program.cs checks args: if "--worker" present, skip Kestrel and run only Hangfire server
    environment:
      - ConnectionStrings__Default=Host=postgres;Database=ledgerflow;Username=ledger;Password=secret
    depends_on: [postgres, redis]

volumes:
  postgres_data:
```

`docker compose up` starts all four containers. The API runs on `http://localhost:5000`. Swagger UI is available at `/swagger`.

---

## 10. Implementation Order (Foundation + Vertical Slices)

### Foundation (Phase 1 of implementation)
1. `dotnet new sln` + project scaffolding + NuGet packages
2. `Shared` — Result<T>, Error, PagedResult
3. `Domain` — all entities, enums, interfaces, domain events
4. `Infrastructure` — AppDbContext, Identity setup, UoW, repository skeletons, Redis service, Hangfire registration
5. `Api` — Program.cs, DI wiring, middleware, JWT auth setup, Dockerfile
6. `docker compose up` → API returns 200 on `/health`
7. Initial EF Core migration

### Vertical Slices (Phase 2 of implementation)
1. **Auth** — Register + Login + Refresh + ForgotPassword + unit + integration tests
2. **Accounts** — CRUD + unit + integration tests
3. **Transactions** — CRUD (with transfer logic) + unit + integration tests
4. **Dashboard** — Summary query + Redis caching + unit + integration tests

---

## 11. Out of Scope (Future Phases)

- Enterprise Architecture (Phase 2): FluentValidation, Logging, Global Exception Handler, Tests
- CSV import (Phase 3): file upload, parsing, Hangfire job
- Monthly close job (Phase 3)
- Redis cache for reports and categories (Phase 3)
- RowVersion / optimistic concurrency enforcement (Phase 4)
- Audit log (Phase 4)
- Email service (real SMTP — MVP uses console logging)
- Frontend (separate spec)
