# LedgerFlow — Fase 3: Processamento Assíncrono

**Date:** 2026-05-14
**Phase:** 3/4
**Prerequisite:** Fase 1 + Fase 2 completas
**Arquitetura:** MVC + Services + Repositories + Hangfire

---

## 1. Visão Geral

Introdução de processamento assíncrono com Hangfire para jobs recorrentes, importador de transações via CSV, e expansão de caching Redis para relatórios e categorias.

---

## 2. Hangfire — Configuração

### Program.cs

```csharp
// Add Hangfire
services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseStorage(new PostgreSqlStorage(connectionString)));

services.AddHangfireServer(options =>
{
    options.WorkerCount = Environment.ProcessorCount;
    options.Queues = new[] { "critical", "default", "background" };
});

// Dashboard (apenas em desenvolvimento)
if (env.IsDevelopment())
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireDashboardAuthorizationFilter() }
    });
}
```

### Retry Policy

```csharp
services.AddScoped<IRecurringJobManager, RecurringJobManager>();

// Global retry
GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute
{
    Attempts = 3,
    DelaysInSeconds = new[] { 30, 60, 300 },
    OnAttemptsExceeded = AttemptsExceededAction.Delete
});
```

---

## 3. Jobs Recorrentes

### Interfaces

```csharp
public interface IBackgroundJobService
{
    Task ScheduleDailySummaryAsync();
    Task ScheduleMonthlyCloseAsync();
    Task ScheduleCacheExpirationAsync();
}
```

### DailySummaryJob

```csharp
public interface IDailySummaryJob
{
    [Daily]
    Task Execute();
}

public class DailySummaryJob : IDailySummaryJob
{
    private readonly IUserRepository _userRepository;
    private readonly ICacheService _cache;
    private readonly ITransactionRepository _transactionRepository;

    public async Task Execute()
    {
        var users = await _userRepository.GetAllActiveAsync();
        foreach (var user in users)
        {
            var transactions = await _transactionRepository.GetByUserIdAsync(user.Id);
            var todayTransactions = transactions.Where(t => t.Date.Date == DateTime.Today);

            var summary = new DailySummaryDto
            {
                Date = DateTime.Today,
                TotalIncome = todayTransactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount),
                TotalExpense = todayTransactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount),
                TransactionCount = todayTransactions.Count()
            };

            var key = $"ledgerflow:daily:{user.Id}:{DateTime.Today:yyyy-MM-dd}";
            await _cache.SetAsync(key, summary, TimeSpan.FromDays(30));
        }
    }
}
```

### MonthlyCloseJob

```csharp
public interface IMonthlyCloseJob
{
    [Monthly(1, 1)] // First day of month
    Task Execute();
}

public class MonthlyCloseJob : IMonthlyCloseJob
{
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IMonthlySnapshotRepository _snapshotRepository;

    public async Task Execute()
    {
        var accounts = await _accountRepository.GetAllAsync();
        var lastMonth = DateTime.Today.AddMonths(-1);

        foreach (var account in accounts)
        {
            var transactions = await _transactionRepository.GetByAccountIdAsync(account.Id);
            var monthTransactions = transactions.Where(t => t.Date.Month == lastMonth.Month && t.Date.Year == lastMonth.Year);

            var snapshot = new MonthlySnapshot
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Year = lastMonth.Year,
                Month = lastMonth.Month,
                ClosingBalance = account.CurrentBalance,
                TotalIncome = monthTransactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount),
                TotalExpense = monthTransactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount),
                CreatedAt = DateTime.UtcNow
            };

            await _snapshotRepository.AddAsync(snapshot);
        }
    }
}
```

### CacheExpirationJob

```csharp
public interface ICacheExpirationJob
{
    [Daily]
    Task Execute();
}

public class CacheExpirationJob : ICacheExpirationJob
{
    private readonly ICacheService _cache;

    public async Task Execute()
    {
        // Remove old dashboard caches
        var keys = await _cache.GetKeysAsync("ledgerflow:dashboard:*");
        var thirtyDaysAgo = DateTime.Today.AddDays(-30);

        foreach (var key in keys)
        {
            if (key.Contains(thirtyDaysAgo.ToString("yyyy-MM-dd")))
            {
                await _cache.RemoveAsync(key);
            }
        }
    }
}
```

### Scheduling

```csharp
// Extensions/HangfireExtensions.cs
public static class HangfireExtensions
{
    public static void ScheduleRecurringJobs(this IApplicationBuilder app)
    {
        var jobManager = app.ApplicationServices.GetRequiredService<IRecurringJobManager>();

        jobManager.AddOrUpdate<IDailySummaryJob>(
            "daily-summary",
            job => job.Execute(),
            Cron.Daily);

        jobManager.AddOrUpdate<IMonthlyCloseJob>(
            "monthly-close",
            job => job.Execute(),
            Cron.Monthly);

        jobManager.AddOrUpdate<ICacheExpirationJob>(
            "cache-expiration",
            job => job.Execute(),
            Cron.Daily);
    }
}

// No final de Program.cs
app.ScheduleRecurringJobs();
```

---

## 4. Importador CSV

### Endpoint

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ImportController : ControllerBase
{
    private readonly ITransactionImportService _importService;

    [HttpPost("transactions")]
    public async Task<IActionResult> ImportTransactions(IFormFile file, Guid accountId, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new Error("validation_error", "File is required"));

        if (!file.FileName.EndsWith(".csv"))
            return BadRequest(new Error("validation_error", "Only CSV files are allowed"));

        var result = await _importService.ImportAsync(file, accountId, GetUserId(), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Errors);
    }
}
```

### Service

```csharp
public interface ITransactionImportService
{
    Task<Result<ImportReport>> ImportAsync(IFormFile file, Guid accountId, Guid userId, CancellationToken ct);
}

public record ImportReport(int TotalRows, int Imported, int Failed, List<ImportError> Errors);
public record ImportError(int Row, string Message);

public class TransactionImportService : ITransactionImportService
{
    private readonly ITransactionService _transactionService;
    private readonly ITransactionValidator _validator;

    public async Task<Result<ImportReport>> ImportAsync(IFormFile file, Guid accountId, Guid userId, CancellationToken ct)
    {
        using var reader = new StreamReader(file.OpenReadStream());
        var lines = (await reader.ReadToEndAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length == 0)
            return Result<ImportReport>.Fail(new Error("validation_error", "CSV file is empty"));

        // Header line
        var rows = lines.Skip(1).ToList();
        var errors = new List<ImportError>();
        var imported = 0;

        // For large files, enqueue background job
        if (rows.Count > 100)
        {
            await _transactionService.EnqueueImportAsync(file, accountId, userId);
            return Result<ImportReport>.Ok(new ImportReport(rows.Count, 0, 0,
                new List<ImportError> { new(0, "Processing in background - you'll receive a notification when complete") }));
        }

        // Process inline for small files
        for (int i = 0; i < rows.Count; i++)
        {
            try
            {
                var csvRow = ParseCsvRow(rows[i]);
                var validationResult = _validator.Validate(csvRow);

                if (!validationResult.IsValid)
                {
                    errors.Add(new ImportError(i + 1, string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage))));
                    continue;
                }

                var request = new CreateTransactionRequest(
                    accountId,
                    csvRow.Amount,
                    csvRow.Type,
                    csvRow.Description,
                    csvRow.Date,
                    csvRow.CategoryId,
                    null
                );

                var result = await _transactionService.CreateAsync(request, userId, ct);
                if (result.IsSuccess)
                    imported++;
                else
                    errors.Add(new ImportError(i + 1, string.Join(", ", result.Errors.Select(e => e.Message))));
            }
            catch (Exception ex)
            {
                errors.Add(new ImportError(i + 1, ex.Message));
            }
        }

        return Result<ImportReport>.Ok(new ImportReport(rows.Count, imported, errors.Count, errors));
    }

    private CsvRow ParseCsvRow(string line)
    {
        var parts = line.Split(',');
        return new CsvRow
        {
            Date = DateTime.Parse(parts[0]),
            Amount = decimal.Parse(parts[1]),
            Type = Enum.Parse<TransactionType>(parts[2]),
            Description = parts[3],
            CategoryName = parts.Length > 4 ? parts[4] : null
        };
    }
}
```

### Background Job for Large Files

```csharp
public interface ICsvImportBackgroundJob
{
    Task ImportAsync(string filePath, Guid accountId, Guid userId);
}

public class CsvImportBackgroundJob : ICsvImportBackgroundJob
{
    private readonly ITransactionService _transactionService;

    [Queue("background")]
    public async Task ImportAsync(string filePath, Guid accountId, Guid userId)
    {
        // Read and process file...
        // Send notification when complete
    }
}
```

---

## 5. Cache Expansion

### Categories Cache

```csharp
public interface ICategoryService
{
    Task<Result<List<CategoryDto>>> GetAllAsync(Guid userId, CancellationToken ct);
}

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _repository;
    private readonly ICacheService _cache;

    public async Task<Result<List<CategoryDto>>> GetAllAsync(Guid userId, CancellationToken ct)
    {
        var cacheKey = $"ledgerflow:categories:{userId}";

        var cached = await _cache.GetAsync<List<CategoryDto>>(cacheKey, ct);
        if (cached != null)
            return Result<List<CategoryDto>>.Ok(cached);

        var categories = await _repository.GetByUserIdAsync(userId, ct);
        var dtos = categories.Select(c => new CategoryDto(c.Id, c.Name, c.Type)).ToList();

        await _cache.SetAsync(cacheKey, dtos, TimeSpan.FromHours(1), ct);
        return Result<List<CategoryDto>>.Ok(dtos);
    }

    public async Task<Result<Guid>> CreateAsync(CreateCategoryRequest request, Guid userId, CancellationToken ct)
    {
        // ... create logic
        await _cache.RemoveAsync($"ledgerflow:categories:{userId}", ct);
    }
}
```

### Reports Cache

```csharp
public interface IReportService
{
    Task<Result<MonthlyReportDto>> GetMonthlyReportAsync(Guid userId, int year, int month, CancellationToken ct);
}

public class ReportService : IReportService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICacheService _cache;

    public async Task<Result<MonthlyReportDto>> GetMonthlyReportAsync(Guid userId, int year, int month, CancellationToken ct)
    {
        var cacheKey = $"ledgerflow:report:monthly:{userId}:{year}:{month}";

        var cached = await _cache.GetAsync<MonthlyReportDto>(cacheKey, ct);
        if (cached != null)
            return Result<MonthlyReportDto>.Ok(cached);

        var transactions = await _transactionRepository.GetByUserIdAsync(userId, ct);
        var monthTransactions = transactions.Where(t => t.Date.Year == year && t.Date.Month == month);

        var report = new MonthlyReportDto
        {
            Year = year,
            Month = month,
            TotalIncome = monthTransactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount),
            TotalExpense = monthTransactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount),
            NetBalance = monthTransactions.Sum(t => t.Type == TransactionType.Income ? t.Amount : -t.Amount),
            TransactionsByCategory = monthTransactions
                .GroupBy(t => t.CategoryId)
                .Select(g => new CategorySummaryDto(g.Key, g.Sum(t => t.Amount)))
                .ToList()
        };

        await _cache.SetAsync(cacheKey, report, TimeSpan.FromDays(1), ct);
        return Result<MonthlyReportDto>.Ok(report);
    }
}
```

---

## 6. Implementação — Ordem

1. Adicionar Hangfire packages NuGet
2. Configurar Hangfire em Program.cs
3. Implementar IDailySummaryJob + agendar
4. Implementar IMonthlyCloseJob + agendar
5. Implementar ICacheExpirationJob + agendar
6. Implementar ITransactionImportService
7. Implementar ICsvImportBackgroundJob para arquivos grandes
8. Expandir cache para Categories
9. Expandir cache para Reports
10. Adicionar testes para Jobs

---

## 7. Out of Scope

- RowVersion enforcement (Fase 4)
- Audit log (Fase 4)
- Notification real (email/push)

---

## 8. Dependências Novas

```xml
<PackageReference Include="Hangfire.Core" Version="1.8.6" />
<PackageReference Include="Hangfire.AspNetCore" Version="1.8.6" />
<PackageReference Include="Hangfire.PostgreSql" Version="1.9.9" />
<PackageReference Include="CsvHelper" Version="30.0.1" />
```

---

## 9. Critérios de Conclusão

- [ ] Hangfire dashboard acessível
- [ ] 3 jobs recorrentes configurados
- [ ] Importador CSV funcionando (linhas < 100 inline, > 100 async)
- [ ] Cache para Categories e Reports
- [ ] Retry policy configurado
- [ ] Testes para Jobs