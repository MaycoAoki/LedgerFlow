# LedgerFlow Fase 1: MVP Implementation Plan

> **For agentic workers:** Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementar um sistema de gestão financeira com autenticação JWT, CRUD de contas e transações, e dashboard com cache Redis.

**Architecture:** MVC + Services + Repositories com injeção de dependência. Camadas: Domain (Entities/Enums), Infrastructure (Repositories/DbContext), Application (Services/DTOs), API (Controllers/Swagger).

**Tech Stack:** C# / ASP.NET Core 8, Entity Framework Core, PostgreSQL, Redis, JWT, Swagger (Swashbuckle), xUnit.

---

## 1. Project Structure

Criar a estrutura de projetos .NET:

```
LedgerFlow.sln
src/
├── LedgerFlow.Api/           (Web API)
├── LedgerFlow.Application/   (Services, DTOs)
├── LedgerFlow.Domain/       (Entities, Enums, Interfaces)
├── LedgerFlow.Infrastructure/ (DbContext, Repositories, Redis)
└── LedgerFlow.Shared/       (Result<T>, Error)
tests/
└── LedgerFlow.Tests/        (Unit Tests)
```

---

## 2. Tasks

### Task 1: Scaffold Solution and Projects

**Files:**
- Create: `LedgerFlow.sln`
- Create: `src/LedgerFlow.Api/LedgerFlow.Api.csproj`
- Create: `src/LedgerFlow.Application/LedgerFlow.Application.csproj`
- Create: `src/LedgerFlow.Domain/LedgerFlow.Domain.csproj`
- Create: `src/LedgerFlow.Infrastructure/LedgerFlow.Infrastructure.csproj`
- Create: `src/LedgerFlow.Shared/LedgerFlow.Shared.csproj`
- Create: `tests/LedgerFlow.Tests/LedgerFlow.Tests.csproj`

**Packages NuGet:**

```xml
<!-- LedgerFlow.Api -->
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.0" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>

<!-- LedgerFlow.Application -->
<PackageReference Include="FluentValidation.AspNetCore" Version="11.3.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.0" />

<!-- LedgerFlow.Infrastructure -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="8.0.0" />
<PackageReference Include="StackExchange.Redis" Version="2.7.10" />

<!-- LedgerFlow.Tests -->
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
<PackageReference Include="xunit" Version="2.6.2" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.5.4" />
<PackageReference Include="Moq" Version="4.20.70" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />

<!-- All projects -->
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
```

**References:**
- Api → Application, Infrastructure, Shared
- Application → Domain, Shared
- Infrastructure → Domain, Shared
- Domain → Shared
- Tests → Application, Infrastructure, Domain, Shared

- [ ] **Step 1: Create solution and projects**

```bash
dotnet new sln -n LedgerFlow
dotnet new webapi -n LedgerFlow.Api -o src/LedgerFlow.Api
dotnet new classlib -n LedgerFlow.Application -o src/LedgerFlow.Application
dotnet new classlib -n LedgerFlow.Domain -o src/LedgerFlow.Domain
dotnet new classlib -n LedgerFlow.Infrastructure -o src/LedgerFlow.Infrastructure
dotnet new classlib -n LedgerFlow.Shared -o src/LedgerFlow.Shared
dotnet new xunit -n LedgerFlow.Tests -o tests/LedgerFlow.Tests

dotnet sln add src/LedgerFlow.Api/LedgerFlow.Api.csproj
dotnet sln add src/LedgerFlow.Application/LedgerFlow.Application.csproj
dotnet sln add src/LedgerFlow.Domain/LedgerFlow.Domain.csproj
dotnet sln add src/LedgerFlow.Infrastructure/LedgerFlow.Infrastructure.csproj
dotnet sln add src/LedgerFlow.Shared/LedgerFlow.Shared.csproj
dotnet sln add tests/LedgerFlow.Tests/LedgerFlow.Tests.csproj
```

- [ ] **Step 2: Add project references**

```bash
dotnet add src/LedgerFlow.Api/LedgerFlow.Api.csproj reference src/LedgerFlow.Application/LedgerFlow.Application.csproj
dotnet add src/LedgerFlow.Api/LedgerFlow.Api.csproj reference src/LedgerFlow.Infrastructure/LedgerFlow.Infrastructure.csproj
dotnet add src/LedgerFlow.Api/LedgerFlow.Api.csproj reference src/LedgerFlow.Shared/LedgerFlow.Shared.csproj
dotnet add src/LedgerFlow.Application/LedgerFlow.Application.csproj reference src/LedgerFlow.Domain/LedgerFlow.Domain.csproj
dotnet add src/LedgerFlow.Application/LedgerFlow.Application.csproj reference src/LedgerFlow.Shared/LedgerFlow.Shared.csproj
dotnet add src/LedgerFlow.Infrastructure/LedgerFlow.Infrastructure.csproj reference src/LedgerFlow.Domain/LedgerFlow.Domain.csproj
dotnet add src/LedgerFlow.Infrastructure/LedgerFlow.Infrastructure.csproj reference src/LedgerFlow.Shared/LedgerFlow.Shared.csproj
dotnet add src/LedgerFlow.Domain/LedgerFlow.Domain.csproj reference src/LedgerFlow.Shared/LedgerFlow.Shared.csproj

dotnet add tests/LedgerFlow.Tests/LedgerFlow.Tests.csproj reference src/LedgerFlow.Application/LedgerFlow.Application.csproj
dotnet add tests/LedgerFlow.Tests/LedgerFlow.Tests.csproj reference src/LedgerFlow.Infrastructure/LedgerFlow.Infrastructure.csproj
dotnet add tests/LedgerFlow.Tests/LedgerFlow.Tests.csproj reference src/LedgerFlow.Domain/LedgerFlow.Domain.csproj
dotnet add tests/LedgerFlow.Tests/LedgerFlow.Tests.csproj reference src/LedgerFlow.Shared/LedgerFlow.Shared.csproj
```

- [ ] **Step 3: Add NuGet packages**

Adicionar os packages listados acima nos respectivos projetos.

- [ ] **Step 4: Verify build**

```bash
dotnet build LedgerFlow.sln
```

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "chore: scaffold solution with 6 projects"
```

---

### Task 2: Shared Layer — Result<T> and Error

**Files:**
- Create: `src/LedgerFlow.Shared/Result/Result.cs`
- Create: `src/LedgerFlow.Shared/Result/ResultOfT.cs`
- Create: `src/LedgerFlow.Shared/Result/Error.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/LedgerFlow.Tests/Shared/ResultTests.cs
namespace LedgerFlow.Tests.Shared;

public class ResultTests
{
    [Fact]
    public void Ok_ShouldReturnSuccessResult()
    {
        var result = Result<string>.Ok("test");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("test");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Fail_ShouldReturnFailureResult()
    {
        var result = Result<string>.Fail(new Error("code", "message"));

        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Errors.Should().Contain(e => e.Code == "code");
    }

    [Fact]
    public void Result_WithoutGeneric_ShouldWork()
    {
        var success = Result.Ok();
        success.IsSuccess.Should().BeTrue();

        var failure = Result.Fail(new Error("code", "message"));
        failure.IsSuccess.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet build tests/LedgerFlow.Tests/LedgerFlow.Tests.csproj
# Build fails - classes don't exist
```

- [ ] **Step 3: Write implementation**

```csharp
// src/LedgerFlow.Shared/Result/Error.cs
namespace LedgerFlow.Shared.Result;

public record Error(string Code, string Message);

// src/LedgerFlow.Shared/Result/Result.cs
namespace LedgerFlow.Shared.Result;

public class Result
{
    public bool IsSuccess { get; }
    public IReadOnlyList<Error> Errors { get; }

    protected Result(bool isSuccess, IEnumerable<Error>? errors = null)
    {
        IsSuccess = isSuccess;
        Errors = errors?.ToList() ?? new List<Error>();
    }

    public static Result Ok() => new(true);
    public static Result Fail(params Error[] errors) => new(false, errors);
    public static Result Fail(IEnumerable<Error> errors) => new(false, errors);
}

// src/LedgerFlow.Shared/Result/ResultOfT.cs
namespace LedgerFlow.Shared.Result;

public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public IReadOnlyList<Error> Errors { get; }

    protected Result(bool isSuccess, T? value, IEnumerable<Error>? errors = null)
    {
        IsSuccess = isSuccess;
        Value = value;
        Errors = errors?.ToList() ?? new List<Error>();
    }

    public static Result<T> Ok(T value) => new(true, value);
    public static Result<T> Fail(params Error[] errors) => new(false, default, errors);
    public static Result<T> Fail(IEnumerable<Error> errors) => new(false, default, errors);
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet test tests/LedgerFlow.Tests/LedgerFlow.Tests.csproj --filter "FullyQualifiedName~ResultTests"
# PASS
```

- [ ] **Step 5: Commit**

```bash
git add src/LedgerFlow.Shared/ tests/LedgerFlow.Tests/Shared/
git commit -m "feat(shared): add Result<T> and Error classes"
```

---

### Task 3: Domain Layer — Entities and Enums

**Files:**
- Create: `src/LedgerFlow.Domain/Enums/TransactionType.cs`
- Create: `src/LedgerFlow.Domain/Enums/CategoryType.cs`
- Create: `src/LedgerFlow.Domain/Entities/User.cs`
- Create: `src/LedgerFlow.Domain/Entities/Account.cs`
- Create: `src/LedgerFlow.Domain/Entities/Transaction.cs`
- Create: `src/LedgerFlow.Domain/Entities/Category.cs`
- Create: `src/LedgerFlow.Domain/Entities/RefreshToken.cs`
- Create: `src/LedgerFlow.Domain/Interfaces/IAccountRepository.cs`
- Create: `src/LedgerFlow.Domain/Interfaces/ITransactionRepository.cs`
- Create: `src/LedgerFlow.Domain/Interfaces/ICategoryRepository.cs`
- Create: `src/LedgerFlow.Domain/Interfaces/IRefreshTokenRepository.cs`
- Create: `src/LedgerFlow.Domain/Interfaces/IUnitOfWork.cs`

- [ ] **Step 1: Create Enums**

```csharp
// src/LedgerFlow.Domain/Enums/TransactionType.cs
namespace LedgerFlow.Domain.Enums;

public enum TransactionType
{
    Income = 1,
    Expense = 2,
    Transfer = 3
}

// src/LedgerFlow.Domain/Enums/CategoryType.cs
namespace LedgerFlow.Domain.Enums;

public enum CategoryType
{
    Income = 1,
    Expense = 2
}
```

- [ ] **Step 2: Create Entities**

```csharp
// src/LedgerFlow.Domain/Entities/User.cs
using Microsoft.AspNetCore.Identity;

namespace LedgerFlow.Domain.Entities;

public class User : IdentityUser<Guid>
{
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<Account> Accounts { get; set; } = new List<Account>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
}

// src/LedgerFlow.Domain/Entities/Account.cs
namespace LedgerFlow.Domain.Entities;

public class Account
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal InitialBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public User? User { get; set; }
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}

// src/LedgerFlow.Domain/Entities/Transaction.cs
using LedgerFlow.Domain.Enums;

namespace LedgerFlow.Domain.Entities;

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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Account? Account { get; set; }
    public Account? TransferToAccount { get; set; }
    public Category? Category { get; set; }
}

// src/LedgerFlow.Domain/Entities/Category.cs
using LedgerFlow.Domain.Enums;

namespace LedgerFlow.Domain.Entities;

public class Category
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public CategoryType Type { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User? User { get; set; }
}

// src/LedgerFlow.Domain/Entities/RefreshToken.cs
namespace LedgerFlow.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public User? User { get; set; }
}
```

- [ ] **Step 3: Create Repository Interfaces**

```csharp
// src/LedgerFlow.Domain/Interfaces/IAccountRepository.cs
namespace LedgerFlow.Domain.Interfaces;

public interface IAccountRepository
{
    Task<List<Account>> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task<Account?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Account account, CancellationToken ct);
    Task UpdateAsync(Account account, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

// src/LedgerFlow.Domain/Interfaces/ITransactionRepository.cs
using LedgerFlow.Domain.Entities;

namespace LedgerFlow.Domain.Interfaces;

public interface ITransactionRepository
{
    Task<List<Transaction>> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task<List<Transaction>> GetByAccountIdAsync(Guid accountId, CancellationToken ct);
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Transaction transaction, CancellationToken ct);
    Task UpdateAsync(Transaction transaction, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

// src/LedgerFlow.Domain/Interfaces/ICategoryRepository.cs
using LedgerFlow.Domain.Entities;

namespace LedgerFlow.Domain.Interfaces;

public interface ICategoryRepository
{
    Task<List<Category>> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task<Category?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Category category, CancellationToken ct);
    Task UpdateAsync(Category category, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

// src/LedgerFlow.Domain/Interfaces/IRefreshTokenRepository.cs
using LedgerFlow.Domain.Entities;

namespace LedgerFlow.Domain.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct);
    Task AddAsync(RefreshToken refreshToken, CancellationToken ct);
    Task RevokeAsync(string token, CancellationToken ct);
}

// src/LedgerFlow.Domain/Interfaces/IUnitOfWork.cs
namespace LedgerFlow.Domain.Interfaces;

public interface IUnitOfWork
{
    Task<int> CommitAsync(CancellationToken ct);
}
```

- [ ] **Step 4: Commit**

```bash
git add src/LedgerFlow.Domain/
git commit -m "feat(domain): add entities and repository interfaces"
```

---

### Task 4: Infrastructure Layer — DbContext and Repositories

**Files:**
- Create: `src/LedgerFlow.Infrastructure/Persistence/AppDbContext.cs`
- Create: `src/LedgerFlow.Infrastructure/Persistence/Configurations/AccountConfiguration.cs`
- Create: `src/LedgerFlow.Infrastructure/Persistence/Configurations/TransactionConfiguration.cs`
- Create: `src/LedgerFlow.Infrastructure/Persistence/Configurations/CategoryConfiguration.cs`
- Create: `src/LedgerFlow.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs`
- Create: `src/LedgerFlow.Infrastructure/Repositories/AccountRepository.cs`
- Create: `src/LedgerFlow.Infrastructure/Repositories/TransactionRepository.cs`
- Create: `src/LedgerFlow.Infrastructure/Repositories/CategoryRepository.cs`
- Create: `src/LedgerFlow.Infrastructure/Repositories/RefreshTokenRepository.cs`
- Create: `src/LedgerFlow.Infrastructure/Repositories/UnitOfWork.cs`
- Create: `src/LedgerFlow.Infrastructure/Services/CacheService.cs`

- [ ] **Step 1: Create AppDbContext**

```csharp
// src/LedgerFlow.Infrastructure/Persistence/AppDbContext.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using LedgerFlow.Domain.Entities;

namespace LedgerFlow.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Identity configuration
        builder.Entity<User>(entity =>
        {
            entity.Property(u => u.Name).HasMaxLength(100);
            entity.HasMany(u => u.RefreshTokens).WithOne(rt => rt.User).HasForeignKey(rt => rt.UserId);
            entity.HasMany(u => u.Accounts).WithOne(a => a.User).HasForeignKey(a => a.UserId);
            entity.HasMany(u => u.Categories).WithOne(c => c.User).HasForeignKey(c => c.UserId);
        });
    }
}
```

- [ ] **Step 2: Create Configurations**

```csharp
// src/LedgerFlow.Infrastructure/Persistence/Configurations/AccountConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LedgerFlow.Domain.Entities;

namespace LedgerFlow.Infrastructure.Persistence.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.InitialBalance).HasPrecision(18, 2);
        builder.Property(x => x.CurrentBalance).HasPrecision(18, 2);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

// src/LedgerFlow.Infrastructure/Persistence/Configurations/TransactionConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LedgerFlow.Domain.Entities;
using LedgerFlow.Domain.Enums;

namespace LedgerFlow.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Type).HasConversion<int>();
        builder.HasOne(x => x.Account).WithMany(a => a.Transactions).HasForeignKey(x => x.AccountId);
        builder.HasOne(x => x.TransferToAccount).WithMany().HasForeignKey(x => x.TransferToAccountId);
        builder.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
    }
}

// src/LedgerFlow.Infrastructure/Persistence/Configurations/CategoryConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LedgerFlow.Domain.Entities;
using LedgerFlow.Domain.Enums;

namespace LedgerFlow.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Type).HasConversion<int>();
    }
}

// src/LedgerFlow.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LedgerFlow.Domain.Entities;

namespace LedgerFlow.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Token).HasMaxLength(500).IsRequired();
    }
}
```

- [ ] **Step 3: Create Repositories**

```csharp
// src/LedgerFlow.Infrastructure/Repositories/AccountRepository.cs
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

// src/LedgerFlow.Infrastructure/Repositories/TransactionRepository.cs
using Microsoft.EntityFrameworkCore;
using LedgerFlow.Domain.Entities;
using LedgerFlow.Domain.Interfaces;
using LedgerFlow.Infrastructure.Persistence;

namespace LedgerFlow.Infrastructure.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly AppDbContext _context;

    public TransactionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Transaction>> GetByUserIdAsync(Guid userId, CancellationToken ct)
    {
        return await _context.Transactions
            .Include(t => t.Account)
            .Where(t => t.Account!.UserId == userId)
            .OrderByDescending(t => t.Date)
            .ToListAsync(ct);
    }

    public async Task<List<Transaction>> GetByAccountIdAsync(Guid accountId, CancellationToken ct)
    {
        return await _context.Transactions
            .Where(t => t.AccountId == accountId)
            .OrderByDescending(t => t.Date)
            .ToListAsync(ct);
    }

    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _context.Transactions.FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task AddAsync(Transaction transaction, CancellationToken ct)
    {
        await _context.Transactions.AddAsync(transaction, ct);
    }

    public async Task UpdateAsync(Transaction transaction, CancellationToken ct)
    {
        _context.Transactions.Update(transaction);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var transaction = await _context.Transactions.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (transaction != null)
        {
            _context.Transactions.Remove(transaction);
            await Task.CompletedTask;
        }
    }
}

// src/LedgerFlow.Infrastructure/Repositories/CategoryRepository.cs
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

// src/LedgerFlow.Infrastructure/Repositories/RefreshTokenRepository.cs
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

// src/LedgerFlow.Infrastructure/Repositories/UnitOfWork.cs
using LedgerFlow.Domain.Interfaces;
using LedgerFlow.Infrastructure.Persistence;

namespace LedgerFlow.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int> CommitAsync(CancellationToken ct)
    {
        return await _context.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Create CacheService**

```csharp
// src/LedgerFlow.Infrastructure/Services/CacheService.cs
using StackExchange.Redis;
using LedgerFlow.Domain.Interfaces;
using System.Text.Json;

namespace LedgerFlow.Infrastructure.Services;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct);
    Task RemoveAsync(string key, CancellationToken ct);
}

public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;

    public RedisCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(key);
        if (value.IsNullOrEmpty) return default;
        return JsonSerializer.Deserialize<T>(value!);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync(key, JsonSerializer.Serialize(value), ttl);
    }

    public async Task RemoveAsync(string key, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(key);
    }
}
```

- [ ] **Step 5: Commit**

```bash
git add src/LedgerFlow.Infrastructure/
git commit -m "feat(infrastructure): add DbContext, repositories and cache service"
```

---

### Task 5: Application Layer — DTOs and Services

**Files:**
- Create: `src/LedgerFlow.Application/DTOs/AuthDTOs.cs`
- Create: `src/LedgerFlow.Application/DTOs/AccountDTOs.cs`
- Create: `src/LedgerFlow.Application/DTOs/TransactionDTOs.cs`
- Create: `src/LedgerFlow.Application/DTOs/DashboardDTOs.cs`
- Create: `src/LedgerFlow.Application/Services/IAuthService.cs`
- Create: `src/LedgerFlow.Application/Services/IAccountService.cs`
- Create: `src/LedgerFlow.Application/Services/ITransactionService.cs`
- Create: `src/LedgerFlow.Application/Services/IDashboardService.cs`
- Create: `src/LedgerFlow.Application/Services/AuthService.cs`
- Create: `src/LedgerFlow.Application/Services/AccountService.cs`
- Create: `src/LedgerFlow.Application/Services/TransactionService.cs`
- Create: `src/LedgerFlow.Application/Services/DashboardService.cs`

- [ ] **Step 1: Create DTOs**

```csharp
// src/LedgerFlow.Application/DTOs/AuthDTOs.cs
namespace LedgerFlow.Application.DTOs;

public record RegisterRequest(string Name, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string AccessToken, string RefreshToken);
public record ForgotPasswordRequest(string Email);
public record AuthResponse(string AccessToken, string RefreshToken);

// src/LedgerFlow.Application/DTOs/AccountDTOs.cs
namespace LedgerFlow.Application.DTOs;

public record CreateAccountRequest(string Name, decimal InitialBalance);
public record UpdateAccountRequest(Guid Id, string Name);
public record AccountDto(Guid Id, string Name, decimal Balance);

// src/LedgerFlow.Application/DTOs/TransactionDTOs.cs
using LedgerFlow.Domain.Enums;

namespace LedgerFlow.Application.DTOs;

public record CreateTransactionRequest(Guid AccountId, decimal Amount, TransactionType Type, string? Description, DateTime Date, Guid? CategoryId, Guid? TransferToAccountId);
public record UpdateTransactionRequest(Guid Id, decimal Amount, string? Description, DateTime Date, Guid? CategoryId);
public record TransactionDto(Guid Id, Guid AccountId, decimal Amount, TransactionType Type, string? Description, DateTime Date, Guid? CategoryId);

// src/LedgerFlow.Application/DTOs/DashboardDTOs.cs
namespace LedgerFlow.Application.DTOs;

public record DashboardSummaryDto(decimal TotalBalance, decimal MonthlyIncome, decimal MonthlyExpense, List<AccountSummaryDto> Accounts);
public record AccountSummaryDto(Guid Id, string Name, decimal Balance);
```

- [ ] **Step 2: Create Service Interfaces**

```csharp
// src/LedgerFlow.Application/Services/IAuthService.cs
using LedgerFlow.Application.DTOs;
using LedgerFlow.Shared.Result;

namespace LedgerFlow.Application.Services;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<Result<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct);
    Task<Result> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct);
}

// src/LedgerFlow.Application/Services/IAccountService.cs
using LedgerFlow.Application.DTOs;
using LedgerFlow.Shared.Result;

namespace LedgerFlow.Application.Services;

public interface IAccountService
{
    Task<Result<Guid>> CreateAsync(CreateAccountRequest request, Guid userId, CancellationToken ct);
    Task<Result> UpdateAsync(UpdateAccountRequest request, Guid userId, CancellationToken ct);
    Task<Result> DeleteAsync(Guid id, Guid userId, CancellationToken ct);
    Task<Result<AccountDto>> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
    Task<Result<List<AccountDto>>> GetAllAsync(Guid userId, CancellationToken ct);
}

// src/LedgerFlow.Application/Services/ITransactionService.cs
using LedgerFlow.Application.DTOs;
using LedgerFlow.Shared.Result;

namespace LedgerFlow.Application.Services;

public interface ITransactionService
{
    Task<Result<Guid>> CreateAsync(CreateTransactionRequest request, Guid userId, CancellationToken ct);
    Task<Result> UpdateAsync(UpdateTransactionRequest request, Guid userId, CancellationToken ct);
    Task<Result> DeleteAsync(Guid id, Guid userId, CancellationToken ct);
    Task<Result<TransactionDto>> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
    Task<Result<List<TransactionDto>>> GetAllAsync(Guid userId, CancellationToken ct);
    Task<Result<List<TransactionDto>>> GetByAccountAsync(Guid accountId, Guid userId, CancellationToken ct);
}

// src/LedgerFlow.Application/Services/IDashboardService.cs
using LedgerFlow.Application.DTOs;
using LedgerFlow.Shared.Result;

namespace LedgerFlow.Application.Services;

public interface IDashboardService
{
    Task<Result<DashboardSummaryDto>> GetSummaryAsync(Guid userId, CancellationToken ct);
}
```

- [ ] **Step 3: Create Service Implementations**

```csharp
// src/LedgerFlow.Application/Services/AuthService.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LedgerFlow.Application.DTOs;
using LedgerFlow.Domain.Entities;
using LedgerFlow.Domain.Interfaces;
using LedgerFlow.Shared.Result;

namespace LedgerFlow.Application.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;

    public AuthService(
        UserManager<User> userManager,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
            return Result<AuthResponse>.Fail(new Error("duplicate_email", "Email already exists"));

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Email = request.Email,
            UserName = request.Email,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => new Error("identity_error", e.Description));
            return Result<AuthResponse>.Fail(errors);
        }

        var (accessToken, refreshToken) = await GenerateTokensAsync(user, ct);
        return Result<AuthResponse>.Ok(new AuthResponse(accessToken, refreshToken));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return Result<AuthResponse>.Fail(new Error("invalid_credentials", "Invalid email or password"));

        var (accessToken, refreshToken) = await GenerateTokensAsync(user, ct);
        return Result<AuthResponse>.Ok(new AuthResponse(accessToken, refreshToken));
    }

    public async Task<Result<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct)
    {
        var refreshToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken, ct);
        if (refreshToken == null)
            return Result<AuthResponse>.Fail(new Error("invalid_token", "Invalid refresh token"));

        await _refreshTokenRepository.RevokeAsync(request.RefreshToken, ct);
        await _unitOfWork.CommitAsync(ct);

        var user = await _userManager.FindByIdAsync(refreshToken.UserId.ToString());
        if (user == null)
            return Result<AuthResponse>.Fail(new Error("user_not_found", "User not found"));

        var (accessToken, newRefreshToken) = await GenerateTokensAsync(user, ct);
        return Result<AuthResponse>.Ok(new AuthResponse(accessToken, newRefreshToken));
    }

    public async Task<Result> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return Result.Ok(); // Don't reveal if email exists

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        Console.WriteLine($"[DEV] Password reset token for {request.Email}: {token}");
        return Result.Ok();
    }

    private async Task<(string accessToken, string refreshToken)> GenerateTokensAsync(User user, CancellationToken ct)
    {
        var jwtSettings = _configuration.GetSection("Jwt").Get<JwtSettings>()!;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings.Issuer,
            audience: jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(jwtSettings.ExpiryMinutes),
            signingCredentials: creds);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = Guid.NewGuid().ToString(),
            ExpiresAt = DateTime.UtcNow.AddDays(jwtSettings.RefreshTokenExpiryDays)
        };
        await _refreshTokenRepository.AddAsync(refreshToken, ct);
        await _unitOfWork.CommitAsync(ct);

        return (accessToken, refreshToken.Token);
    }
}

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; }
    public int RefreshTokenExpiryDays { get; set; }
}

// src/LedgerFlow.Application/Services/AccountService.cs
using LedgerFlow.Application.DTOs;
using LedgerFlow.Domain.Entities;
using LedgerFlow.Domain.Interfaces;
using LedgerFlow.Shared.Result;

namespace LedgerFlow.Application.Services;

public class AccountService : IAccountService
{
    private readonly IAccountRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public AccountService(IAccountRepository repository, IUnitOfWork unitOfWork, ICacheService cache)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<Result<Guid>> CreateAsync(CreateAccountRequest request, Guid userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<Guid>.Fail(new Error("validation_error", "Account name is required"));

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name,
            InitialBalance = request.InitialBalance,
            CurrentBalance = request.InitialBalance,
            CreatedAt = DateTime.UtcNow
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
        account.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(account, ct);
        await _unitOfWork.CommitAsync(ct);

        await _cache.RemoveAsync($"ledgerflow:dashboard:{userId}:summary", ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var account = await _repository.GetByIdAsync(id, ct);
        if (account == null || account.UserId != userId)
            return Result.Fail(new Error("not_found", "Account not found"));

        await _repository.DeleteAsync(id, ct);
        await _unitOfWork.CommitAsync(ct);

        await _cache.RemoveAsync($"ledgerflow:dashboard:{userId}:summary", ct);
        return Result.Ok();
    }

    public async Task<Result<AccountDto>> GetByIdAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var account = await _repository.GetByIdAsync(id, ct);
        if (account == null || account.UserId != userId)
            return Result<AccountDto>.Fail(new Error("not_found", "Account not found"));

        return Result<AccountDto>.Ok(new AccountDto(account.Id, account.Name, account.CurrentBalance));
    }

    public async Task<Result<List<AccountDto>>> GetAllAsync(Guid userId, CancellationToken ct)
    {
        var accounts = await _repository.GetByUserIdAsync(userId, ct);
        var dtos = accounts.Select(a => new AccountDto(a.Id, a.Name, a.CurrentBalance)).ToList();
        return Result<List<AccountDto>>.Ok(dtos);
    }
}

// src/LedgerFlow.Application/Services/TransactionService.cs
using LedgerFlow.Application.DTOs;
using LedgerFlow.Domain.Entities;
using LedgerFlow.Domain.Enums;
using LedgerFlow.Domain.Interfaces;
using LedgerFlow.Shared.Result;

namespace LedgerFlow.Application.Services;

public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _repository;
    private readonly IAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public TransactionService(
        ITransactionRepository repository,
        IAccountRepository accountRepository,
        IUnitOfWork unitOfWork,
        ICacheService cache)
    {
        _repository = repository;
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<Result<Guid>> CreateAsync(CreateTransactionRequest request, Guid userId, CancellationToken ct)
    {
        var account = await _accountRepository.GetByIdAsync(request.AccountId, ct);
        if (account == null || account.UserId != userId)
            return Result<Guid>.Fail(new Error("not_found", "Account not found"));

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = request.AccountId,
            Amount = request.Amount,
            Type = request.Type,
            Description = request.Description,
            Date = request.Date,
            CategoryId = request.CategoryId,
            TransferToAccountId = request.TransferToAccountId,
            CreatedAt = DateTime.UtcNow
        };

        // Handle balance updates
        switch (request.Type)
        {
            case TransactionType.Income:
                account.CurrentBalance += request.Amount;
                break;
            case TransactionType.Expense:
                account.CurrentBalance -= request.Amount;
                break;
            case TransactionType.Transfer:
                if (request.TransferToAccountId == null)
                    return Result<Guid>.Fail(new Error("validation_error", "Transfer requires destination account"));
                var destAccount = await _accountRepository.GetByIdAsync(request.TransferToAccountId.Value, ct);
                if (destAccount == null)
                    return Result<Guid>.Fail(new Error("not_found", "Destination account not found"));
                account.CurrentBalance -= request.Amount;
                destAccount.CurrentBalance += request.Amount;
                await _accountRepository.UpdateAsync(destAccount, ct);
                break;
        }

        await _repository.AddAsync(transaction, ct);
        await _accountRepository.UpdateAsync(account, ct);
        await _unitOfWork.CommitAsync(ct);

        await _cache.RemoveAsync($"ledgerflow:dashboard:{userId}:summary", ct);
        return Result<Guid>.Ok(transaction.Id);
    }

    public async Task<Result> UpdateAsync(UpdateTransactionRequest request, Guid userId, CancellationToken ct)
    {
        var transaction = await _repository.GetByIdAsync(request.Id, ct);
        if (transaction == null)
            return Result.Fail(new Error("not_found", "Transaction not found"));

        var account = await _accountRepository.GetByIdAsync(transaction.AccountId, ct);
        if (account == null || account.UserId != userId)
            return Result.Fail(new Error("not_found", "Transaction not found"));

        // Reverse old amount
        switch (transaction.Type)
        {
            case TransactionType.Income:
                account.CurrentBalance -= transaction.Amount;
                break;
            case TransactionType.Expense:
                account.CurrentBalance += transaction.Amount;
                break;
            case TransactionType.Transfer:
                var destAccount = await _accountRepository.GetByIdAsync(transaction.TransferToAccountId!.Value, ct);
                if (destAccount != null)
                {
                    account.CurrentBalance += transaction.Amount;
                    destAccount.CurrentBalance -= transaction.Amount;
                    await _accountRepository.UpdateAsync(destAccount, ct);
                }
                break;
        }

        // Apply new amount
        transaction.Amount = request.Amount;
        transaction.Description = request.Description;
        transaction.Date = request.Date;
        transaction.CategoryId = request.CategoryId;

        switch (request.Type == TransactionType.Income ? transaction.Type : request.Type)
        {
            case TransactionType.Income:
                account.CurrentBalance += request.Amount;
                break;
            case TransactionType.Expense:
                account.CurrentBalance -= request.Amount;
                break;
        }

        await _repository.UpdateAsync(transaction, ct);
        await _accountRepository.UpdateAsync(account, ct);
        await _unitOfWork.CommitAsync(ct);

        await _cache.RemoveAsync($"ledgerflow:dashboard:{userId}:summary", ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var transaction = await _repository.GetByIdAsync(id, ct);
        if (transaction == null)
            return Result.Fail(new Error("not_found", "Transaction not found"));

        var account = await _accountRepository.GetByIdAsync(transaction.AccountId, ct);
        if (account == null || account.UserId != userId)
            return Result.Fail(new Error("not_found", "Transaction not found"));

        // Reverse the transaction
        switch (transaction.Type)
        {
            case TransactionType.Income:
                account.CurrentBalance -= transaction.Amount;
                break;
            case TransactionType.Expense:
                account.CurrentBalance += transaction.Amount;
                break;
            case TransactionType.Transfer:
                var destAccount = await _accountRepository.GetByIdAsync(transaction.TransferToAccountId!.Value, ct);
                if (destAccount != null)
                {
                    account.CurrentBalance += transaction.Amount;
                    destAccount.CurrentBalance -= transaction.Amount;
                    await _accountRepository.UpdateAsync(destAccount, ct);
                }
                break;
        }

        await _repository.DeleteAsync(id, ct);
        await _accountRepository.UpdateAsync(account, ct);
        await _unitOfWork.CommitAsync(ct);

        await _cache.RemoveAsync($"ledgerflow:dashboard:{userId}:summary", ct);
        return Result.Ok();
    }

    public async Task<Result<TransactionDto>> GetByIdAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var transaction = await _repository.GetByIdAsync(id, ct);
        if (transaction == null)
            return Result<TransactionDto>.Fail(new Error("not_found", "Transaction not found"));

        var account = await _accountRepository.GetByIdAsync(transaction.AccountId, ct);
        if (account == null || account.UserId != userId)
            return Result<TransactionDto>.Fail(new Error("not_found", "Transaction not found"));

        return Result<TransactionDto>.Ok(new TransactionDto(
            transaction.Id,
            transaction.AccountId,
            transaction.Amount,
            transaction.Type,
            transaction.Description,
            transaction.Date,
            transaction.CategoryId));
    }

    public async Task<Result<List<TransactionDto>>> GetAllAsync(Guid userId, CancellationToken ct)
    {
        var transactions = await _repository.GetByUserIdAsync(userId, ct);
        var dtos = transactions.Select(t => new TransactionDto(
            t.Id, t.AccountId, t.Amount, t.Type, t.Description, t.Date, t.CategoryId)).ToList();
        return Result<List<TransactionDto>>.Ok(dtos);
    }

    public async Task<Result<List<TransactionDto>>> GetByAccountAsync(Guid accountId, Guid userId, CancellationToken ct)
    {
        var account = await _accountRepository.GetByIdAsync(accountId, ct);
        if (account == null || account.UserId != userId)
            return Result<List<TransactionDto>>.Fail(new Error("not_found", "Account not found"));

        var transactions = await _repository.GetByAccountIdAsync(accountId, ct);
        var dtos = transactions.Select(t => new TransactionDto(
            t.Id, t.AccountId, t.Amount, t.Type, t.Description, t.Date, t.CategoryId)).ToList();
        return Result<List<TransactionDto>>.Ok(dtos);
    }
}

// src/LedgerFlow.Application/Services/DashboardService.cs
using LedgerFlow.Application.DTOs;
using LedgerFlow.Domain.Enums;
using LedgerFlow.Domain.Interfaces;
using LedgerFlow.Shared.Result;

namespace LedgerFlow.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICacheService _cache;

    private const string CacheKeyPrefix = "ledgerflow:dashboard:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public DashboardService(
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        ICacheService cache)
    {
        _accountRepository = accountRepository;
        _transactionRepository = transactionRepository;
        _cache = cache;
    }

    public async Task<Result<DashboardSummaryDto>> GetSummaryAsync(Guid userId, CancellationToken ct)
    {
        var cacheKey = $"{CacheKeyPrefix}{userId}:summary";

        var cached = await _cache.GetAsync<DashboardSummaryDto>(cacheKey, ct);
        if (cached != null)
            return Result<DashboardSummaryDto>.Ok(cached);

        var accounts = await _accountRepository.GetByUserIdAsync(userId, ct);
        var accountSummaries = accounts.Select(a => new AccountSummaryDto(a.Id, a.Name, a.CurrentBalance)).ToList();

        var totalBalance = accounts.Sum(a => a.CurrentBalance);

        var transactions = await _transactionRepository.GetByUserIdAsync(userId, ct);
        var now = DateTime.UtcNow;
        var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);

        var monthlyTransactions = transactions.Where(t => t.Date >= firstDayOfMonth).ToList();
        var monthlyIncome = monthlyTransactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
        var monthlyExpense = monthlyTransactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);

        var summary = new DashboardSummaryDto(totalBalance, monthlyIncome, monthlyExpense, accountSummaries);

        await _cache.SetAsync(cacheKey, summary, CacheDuration, ct);
        return Result<DashboardSummaryDto>.Ok(summary);
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add src/LedgerFlow.Application/
git commit -m "feat(application): add DTOs and service implementations"
```

---

### Task 6: API Layer — Controllers, DI, Swagger

**Files:**
- Create: `src/LedgerFlow.Api/Controllers/AuthController.cs`
- Create: `src/LedgerFlow.Api/Controllers/AccountsController.cs`
- Create: `src/LedgerFlow.Api/Controllers/TransactionsController.cs`
- Create: `src/LedgerFlow.Api/Controllers/DashboardController.cs`
- Create: `src/LedgerFlow.Api/Extensions/ServiceCollectionExtensions.cs`
- Modify: `src/LedgerFlow.Api/Program.cs`
- Create: `src/LedgerFlow.Api/appsettings.json`
- Create: `src/LedgerFlow.Api/Dockerfile`

- [ ] **Step 1: Create Controllers**

```csharp
// src/LedgerFlow.Api/Controllers/AuthController.cs
using Microsoft.AspNetCore.Mvc;
using LedgerFlow.Application.DTOs;
using LedgerFlow.Application.Services;

namespace LedgerFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

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

// src/LedgerFlow.Api/Controllers/AccountsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using LedgerFlow.Application.DTOs;
using LedgerFlow.Application.Services;

namespace LedgerFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountsController(IAccountService accountService)
    {
        _accountService = accountService;
    }

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
        request = request with { Id = id };
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

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

// src/LedgerFlow.Api/Controllers/TransactionsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using LedgerFlow.Application.DTOs;
using LedgerFlow.Application.Services;

namespace LedgerFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _transactionService;

    public TransactionsController(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

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

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _transactionService.GetByIdAsync(id, userId, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Errors);
    }

    [HttpGet("account/{accountId}")]
    public async Task<IActionResult> GetByAccount(Guid accountId, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _transactionService.GetByAccountAsync(accountId, userId, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Errors);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTransactionRequest request, CancellationToken ct)
    {
        request = request with { Id = id };
        var userId = GetUserId();
        var result = await _transactionService.UpdateAsync(request, userId, ct);
        return result.IsSuccess ? NoContent() : BadRequest(result.Errors);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _transactionService.DeleteAsync(id, userId, ct);
        return result.IsSuccess ? NoContent() : BadRequest(result.Errors);
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

// src/LedgerFlow.Api/Controllers/DashboardController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using LedgerFlow.Application.Services;

namespace LedgerFlow.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _dashboardService.GetSummaryAsync(userId, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Errors);
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
```

- [ ] **Step 2: Create DI Extensions**

```csharp
// src/LedgerFlow.Api/Extensions/ServiceCollectionExtensions.cs
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;
using LedgerFlow.Application.Services;
using LedgerFlow.Domain.Interfaces;
using LedgerFlow.Infrastructure.Persistence;
using LedgerFlow.Infrastructure.Repositories;
using LedgerFlow.Infrastructure.Services;

namespace LedgerFlow.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IDashboardService, DashboardService>();
        return services;
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("Default")));

        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        var redisConfig = config.GetSection("Redis").Get<RedisConfig>()!;
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect($"{redisConfig.ConnectionString},password={redisConfig.Password}"));
        services.AddScoped<ICacheService, RedisCacheService>();

        services.AddIdentity<User, Microsoft.AspNetCore.Identity.IdentityRole<Guid>>()
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

        return services;
    }
}

public class RedisConfig
{
    public string ConnectionString { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
```

- [ ] **Step 3: Create/Update Program.cs**

```csharp
// src/LedgerFlow.Api/Program.cs
using LedgerFlow.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "LedgerFlow API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuth(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "LedgerFlow API v1"));
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

- [ ] **Step 4: Create appsettings.json**

```json
// src/LedgerFlow.Api/appsettings.json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=ledgerflow;Username=ledger;Password=ledger_secret"
  },
  "Redis": {
    "ConnectionString": "localhost",
    "Password": "redis_secret"
  },
  "Jwt": {
    "Secret": "dev-secret-min-32-chars-for-hs256",
    "Issuer": "ledgerflow",
    "Audience": "ledgerflow-client",
    "ExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

- [ ] **Step 5: Create Dockerfile**

```dockerfile
# src/LedgerFlow.Api/Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["LedgerFlow.Api/LedgerFlow.Api.csproj", "LedgerFlow.Api/"]
COPY ["LedgerFlow.Application/LedgerFlow.Application.csproj", "LedgerFlow.Application/"]
COPY ["LedgerFlow.Domain/LedgerFlow.Domain.csproj", "LedgerFlow.Domain/"]
COPY ["LedgerFlow.Infrastructure/LedgerFlow.Infrastructure.csproj", "LedgerFlow.Infrastructure/"]
COPY ["LedgerFlow.Shared/LedgerFlow.Shared.csproj", "LedgerFlow.Shared/"]
RUN dotnet restore "LedgerFlow.Api/LedgerFlow.Api.csproj"
COPY . .
WORKDIR "/src/LedgerFlow.Api"
RUN dotnet build "LedgerFlow.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "LedgerFlow.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LedgerFlow.Api.dll"]
```

- [ ] **Step 6: Commit**

```bash
git add src/LedgerFlow.Api/
git commit -m "feat(api): add controllers, DI, Swagger and Dockerfile"
```

---

### Task 7: Database Migrations

**Files:**
- Create: `src/LedgerFlow.Infrastructure/Persistence/Migrations/`

- [ ] **Step 1: Create initial migration**

```bash
cd src/LedgerFlow.Api
dotnet ef migrations add InitialCreate --project ../LedgerFlow.Infrastructure --output-dir Persistence/Migrations
```

- [ ] **Step 2: Commit**

```bash
git add src/LedgerFlow.Infrastructure/
git commit -m "chore: add EF Core migrations"
```

---

### Task 8: Build and Verify

- [ ] **Step 1: Build solution**

```bash
dotnet build LedgerFlow.sln
```

- [ ] **Step 2: Run tests**

```bash
dotnet test tests/LedgerFlow.Tests/LedgerFlow.Tests.csproj --verbosity normal
```

- [ ] **Step 3: Commit**

```bash
git add .
git commit -m "chore: build and verify solution"
```

---

## 3. Summary

**Created:**
- 6 projects (Api, Application, Domain, Infrastructure, Shared, Tests)
- 4 controllers (Auth, Accounts, Transactions, Dashboard)
- 4 services with full CRUD logic
- 5 repositories
- Redis cache integration
- JWT authentication
- Swagger documentation
- Docker support

**Commands to run:**
```bash
# Build
dotnet build LedgerFlow.sln

# Run migrations (after setting DB)
dotnet ef database update --project src/LedgerFlow.Api

# Test
dotnet test

# Run (dev)
dotnet run --project src/LedgerFlow.Api
```