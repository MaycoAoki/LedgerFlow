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

**Arquitetura:** MVC + Services + Repositories com DI

---

## 2. Estrutura do Projeto

```
LedgerFlow.sln
src/
├── LedgerFlow.Api/              # Controllers, Program.cs, DI
├── LedgerFlow.Application/     # DTOs, Interfaces de Services
├── LedgerFlow.Domain/         # Entities, Enums
├── LedgerFlow.Infrastructure/  # Repositories, AppDbContext, Redis
└── LedgerFlow.Shared/         # Result<T>, Error
```

**Dependências:**
```
Api → Application → Domain ← Infrastructure
Api → Shared ← Application ← Infrastructure
Domain → Shared
```

---

## 3. Domain Layer

**Location:** `src/LedgerFlow.Domain/`

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

---

## 4. Infrastructure Layer

**Location:** `src/LedgerFlow.Infrastructure/`

### Repositories

```csharp
// Interfaces em Domain/Interfaces/
public interface IAccountRepository {
    Task<List<Account>> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task<Account?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Account account, CancellationToken ct);
    Task UpdateAsync(Account account, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

public interface ITransactionRepository {
    Task<List<Transaction>> GetByAccountIdAsync(Guid accountId, CancellationToken ct);
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Transaction transaction, CancellationToken ct);
    Task UpdateAsync(Transaction transaction, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

public interface ICategoryRepository {
    Task<List<Category>> GetByUserIdAsync(Guid userId, CancellationToken ct);
}

public interface IRefreshTokenRepository {
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct);
    Task AddAsync(RefreshToken token, CancellationToken ct);
    Task RevokeAsync(string token, CancellationToken ct);
}

public interface IUnitOfWork {
    Task<int> CommitAsync(CancellationToken ct);
}
```

### AppDbContext

```csharp
class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
```

- Configurações em `Persistence/Configurations/`
- Migrations em `Persistence/Migrations/`

### Redis Cache

```csharp
public interface ICacheService {
    Task<T?> GetAsync<T>(string key, CancellationToken ct);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct);
    Task RemoveAsync(string key, CancellationToken ct);
}
```

- Keys: `ledgerflow:{entity}:{id}:{variant}`
- Dashboard TTL: 5 minutos

---

## 5. Application Layer (Services)

**Location:** `src/LedgerFlow.Application/Services/`

### Interfaces

```csharp
public interface IAuthService {
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<Result<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct);
    Task<Result> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct);
}

public interface IAccountService {
    Task<Result<Guid>> CreateAsync(CreateAccountRequest request, Guid userId, CancellationToken ct);
    Task<Result> UpdateAsync(UpdateAccountRequest request, Guid userId, CancellationToken ct);
    Task<Result> DeleteAsync(Guid id, Guid userId, CancellationToken ct);
    Task<Result<AccountDto>> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
    Task<Result<List<AccountDto>>> GetAllAsync(Guid userId, CancellationToken ct);
}

public interface ITransactionService {
    Task<Result<Guid>> CreateAsync(CreateTransactionRequest request, Guid userId, CancellationToken ct);
    Task<Result> UpdateAsync(UpdateTransactionRequest request, Guid userId, CancellationToken ct);
    Task<Result> DeleteAsync(Guid id, Guid userId, CancellationToken ct);
    Task<Result<TransactionDto>> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
    Task<Result<List<TransactionDto>>> GetAllAsync(Guid userId, CancellationToken ct);
    Task<Result<List<TransactionDto>>> GetByAccountAsync(Guid accountId, Guid userId, CancellationToken ct);
}

public interface IDashboardService {
    Task<Result<DashboardSummaryDto>> GetSummaryAsync(Guid userId, CancellationToken ct);
}
```

### Implementations

```csharp
public class AccountService : IAccountService
{
    private readonly IAccountRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public async Task<Result<Guid>> CreateAsync(CreateAccountRequest request, Guid userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<Guid>.Fail(new Error("validation_error", "Account name is required"));

        var account = new Account {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name,
            InitialBalance = request.InitialBalance,
            CurrentBalance = request.InitialBalance
        };

        await _repository.AddAsync(account, ct);
        await _unitOfWork.CommitAsync(ct);

        return Result<Guid>.Ok(account.Id);
    }

    public async Task<Result> UpdateAsync(UpdateAccountRequest request, Guid userId, CancellationToken ct)
    {
        var account = await _repository.GetByIdAsync(request.Id, ct);
        if (account == null || account.UserId != userId)
            return Result.Fail(new Error("not_found", "Account not found"));

        account.Name = request.Name;
        await _repository.UpdateAsync(account, ct);
        await _unitOfWork.CommitAsync(ct);

        await _cache.RemoveAsync($"ledgerflow:dashboard:{userId}:summary", ct);
        return Result.Ok();
    }
}
```

---

## 6. Shared Layer

```csharp
record Error(string Code, string Message);

class Result<T> {
    bool IsSuccess { get; }
    T? Value { get; }
    IReadOnlyList<Error> Errors { get; }
    static Result<T> Ok(T value);
    static Result<T> Fail(params Error[] errors);
    static Result<T> Fail(IEnumerable<Error> errors);
}

// overload para Result sem tipo
class Result {
    bool IsSuccess { get; }
    IReadOnlyList<Error> Errors { get; }
    static Result Ok();
    static Result Fail(params Error[] errors);
}
```

**HTTP Mapping:**

| Scenario | HTTP Status |
|-----------|-------------|
| Success | 200 OK / 201 Created |
| Validation failure | 400 Bad Request |
| Not found | 404 Not Found |
| Conflict | 409 Conflict |
| Unhandled exception | 500 Internal Server Error |

---

## 7. API Layer

**Location:** `src/LedgerFlow.Api/`

### Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _authService.RegisterAsync(request, ct);
        return result.IsSuccess ? Created("", result.Value) : BadRequest(result.Errors);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        return result.IsSuccess ? Ok(result.Value) : Unauthorized(result.Errors);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var result = await _authService.RefreshTokenAsync(request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Errors);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        var result = await _authService.ForgotPasswordAsync(request, ct);
        return result.IsSuccess ? Ok() : BadRequest(result.Errors);
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _accountService.CreateAsync(request, userId, ct);
        return result.IsSuccess ? Created($"api/accounts/{result.Value}", null) : BadRequest(result.Errors);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _accountService.GetAllAsync(userId, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Errors);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _accountService.GetByIdAsync(id, userId, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Errors);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAccountRequest request, CancellationToken ct)
    {
        request.Id = id;
        var userId = GetUserId();
        var result = await _accountService.UpdateAsync(request, userId, ct);
        return result.IsSuccess ? NoContent() : BadRequest(result.Errors);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _accountService.DeleteAsync(id, userId, ct);
        return result.IsSuccess ? NoContent() : BadRequest(result.Errors);
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _transactionService;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTransactionRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _transactionService.CreateAsync(request, userId, ct);
        return result.IsSuccess ? Created($"api/transactions/{result.Value}", null) : BadRequest(result.Errors);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _transactionService.GetAllAsync(userId, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Errors);
    }

    [HttpGet("account/{accountId}")]
    public async Task<IActionResult> GetByAccount(Guid accountId, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _transactionService.GetByAccountAsync(accountId, userId, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Errors);
    }

    // ... outras actions

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
}

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _dashboardService.GetSummaryAsync(userId, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Errors);
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
}
```

### DI Registration

```csharp
// Extensions/ServiceCollectionExtensions.cs
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // DbContext
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("Default")));

        // Repositories
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Redis
        services.AddScoped<ICacheService, RedisCacheService>();

        // Identity
        services.AddIdentity<User, IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        return services;
    }

    public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration config)
    {
        var jwtSettings = config.GetSection("Jwt").Get<JwtSettings>()!;

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret))
            };
        });

        services.AddScoped<IJwtService, JwtService>();
        return services;
    }
}

// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuth(builder.Configuration);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### Middleware

```csharp
// Middleware/ExceptionHandlingMiddleware.cs
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://httpstatuses.com/500",
                title = "Internal Server Error",
                detail = ex.Message
            });
        }
    }
}
```

---

## 8. DTOs

```csharp
// Auth
public record RegisterRequest(string Name, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string AccessToken, string RefreshToken);
public record ForgotPasswordRequest(string Email);
public record AuthResponse(string AccessToken, string RefreshToken);

// Accounts
public record CreateAccountRequest(string Name, decimal InitialBalance);
public record UpdateAccountRequest(Guid Id, string Name);
public record AccountDto(Guid Id, string Name, decimal Balance);

// Transactions
public record CreateTransactionRequest(Guid AccountId, decimal Amount, string Type, string? Description, DateTime Date, Guid? CategoryId, Guid? TransferToAccountId);
public record UpdateTransactionRequest(Guid Id, decimal Amount, string? Description, DateTime Date, Guid? CategoryId);
public record TransactionDto(Guid Id, Guid AccountId, decimal Amount, string Type, string? Description, DateTime Date, Guid? CategoryId);

// Dashboard
public record DashboardSummaryDto(decimal TotalBalance, decimal MonthlyIncome, decimal MonthlyExpense, List<AccountSummaryDto> Accounts);
public record AccountSummaryDto(Guid Id, string Name, decimal Balance);
```

---

## 9. Implementação — Ordem

1. Scaffold projetos + packages NuGet
2. Domain → Entities + Enums
3. Shared → Result<T> + Error
4. Infrastructure → DbContext + Repositories
5. Application → Interfaces Services + Implementations
6. Api → Controllers + DI + Auth + Middleware
7. docker compose up → health check
8. EF Core migrations
9. Auth (CRUD + testes)
10. Accounts (CRUD + testes)
11. Transactions (CRUD + testes)
12. Dashboard (summary + cache + testes)

---

## 10. Out of Scope

- Jobs (Fase 3)
- CSV import (Fase 3)
- RowVersion enforcement (Fase 4)
- Audit log (Fase 4)
- Email real (MVP: console log)

---

## 11. Critérios de Conclusão

- [ ] Arquitetura MVC + Services + Repositories funcionando
- [ ] Auth com JWT funcionando
- [ ] Accounts CRUD completo
- [ ] Transactions CRUD com transferência
- [ ] Dashboard com cache Redis
- [ ] Validação básica nos Services
- [ ] Build verde