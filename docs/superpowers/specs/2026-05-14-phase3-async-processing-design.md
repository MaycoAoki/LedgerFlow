# LedgerFlow — Fase 3: Processamento Assíncrono

**Date:** 2026-05-14
**Phase:** 3/4
**Prerequisite:** Fase 1 + Fase 2 completas

---

## 1. Visão Geral

Introdução de processamento assíncrono com Hangfire para jobs recorrentes, importador de transações via CSV, e expansão de caching Redis para relatórios e categorias.

---

## 2. Hangfire — Configuração

### Servidor

```csharp
// Program.cs
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
```

### Dashboard

```csharp
// Available em /hangfire
services.AddHangfireDashboard(options => options
{
    DashboardTitle = "LedgerFlow Jobs";
    Authorization = new[] { new HangfireAuthFilter() };
});
```

---

## 3. Jobs Recorrentes

### DailySummaryJob

```csharp
public class DailySummaryJob
{
    private readonly IUserRepository _userRepository;
    private readonly IRedisCacheService _cache;

    [Daily]
    public async Task Execute()
    {
        var users = await _userRepository.GetAllActiveAsync();
        foreach (var user in users)
        {
            var summary = await CalculateDailySummary(user.Id);
            await _cache.SetAsync($"ledgerflow:daily:{user.Id}:{DateTime.Today}", summary, TimeSpan.FromDays(7));
        }
    }
}
```

### MonthlyCloseJob

```csharp
public class MonthlyCloseJob
{
    [Monthly(1, 1)] // First day of month
    public async Task Execute()
    {
        // Create monthly snapshots for all accounts
        // Archive old transactions (move to historical table)
        // Reset monthly counters
    }
}
```

### CacheExpirationJob

```csharp
public class CacheExpirationJob
{
    [Daily]
    public async Task Execute()
    {
        var keys = await _redis.GetKeysAsync("ledgerflow:dashboard:*");
        var expired = keys.Where(k => DateTime.Parse(k.Split(':').Last()) < DateTime.Today.AddDays(-30));
        foreach (var key in expired)
        {
            await _redis.RemoveAsync(key);
        }
    }
}
```

---

## 4. Importador CSV

### Endpoint

```
POST /api/transactions/import
Content-Type: multipart/form-data
Body: file (CSV)
```

### CSV Format

```csv
date,amount,type,description,category
2024-01-15,1500.00,income,Salary,Salary
2024-01-16,-120.50,expense,Grocery,Food
2024-01-17,-500.00,transfer,Savings,Investment
```

### Processo

1. **Upload** → arquivo salvo temporariamente
2. **Parse** → validação de formato (Hangfire job)
3. **Validate** → cada linha validada (date format, amount > 0, type enum)
4. **Import** → transactions criadas em batch
5. **Report** → summary com sucesso/erros

### Implementation

```csharp
// Commands/ImportTransactionsCommand.cs
public record ImportTransactionsCommand(Guid UserId, Guid AccountId, IFormFile File) : IRequest<Result<ImportReport>>;

public record ImportReport(int TotalRows, int Imported, int Failed, List<ImportError> Errors);
public record ImportError(int Row, string Message);

// Handler
public class ImportTransactionsCommandHandler : IRequestHandler<ImportTransactionsCommand, Result<ImportReport>>
{
    public async Task<Result<ImportReport>> Handle(ImportTransactionsCommand request, CancellationToken ct)
    {
        // 1. Validate file
        if (!IsValidCsv(request.File)) return Result<ImportReport>.Fail(...);

        // 2. Parse CSV
        var rows = await ParseCsvAsync(request.File, ct);

        // 3. Enqueue background job for large files
        if (rows.Count > 100)
        {
            await _backgroundJobClient.EnqueueAsync<ICsvImportJob>(job =>
                job.ImportAsync(request.UserId, request.AccountId, rows));
            return Result<ImportReport>.Ok(new ImportReport(rows.Count, 0, 0, new List<ImportError> { new(0, "Processing in background") }));
        }

        // 4. Process inline for small files
        return await ProcessRows(request.UserId, request.AccountId, rows, ct);
    }
}

// Background Job
public class CsvImportJob : ICsvImportJob
{
    public async Task ImportAsync(Guid userId, Guid accountId, List<CsvRow> rows)
    {
        var errors = new List<ImportError>();
        var imported = 0;

        foreach (var row in rows)
        {
            try
            {
                var command = new CreateTransactionCommand(accountId, row.Amount, row.Type, row.Description, row.Date, row.CategoryId);
                await _mediator.Send(command);
                imported++;
            }
            catch (Exception ex)
            {
                errors.Add(new ImportError(row.LineNumber, ex.Message));
            }
        }

        await _notificationService.SendAsync(userId, $"Import completed: {imported} imported, {errors.Count} failed");
    }
}
```

### Validação de Linhas

```csharp
public class CsvRowValidator : AbstractValidator<CsvRow>
{
    public CsvRowValidator()
    {
        RuleFor(x => x.Date).NotEmpty().Must(BeValidDate);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
```

---

## 5. Cache Expansion

### Dashboard (existente)

- Key: `ledgerflow:dashboard:{userId}:summary`
- TTL: 5 minutos

### Categories

```csharp
// GET /api/categories (add caching)
public class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, Result<List<CategoryDto>>>
{
    public async Task<Result<List<CategoryDto>>> Handle(GetCategoriesQuery request, CancellationToken ct)
    {
        var cacheKey = $"ledgerflow:categories:{request.UserId}";
        var cached = await _cache.GetAsync<List<CategoryDto>>(cacheKey, ct);
        if (cached != null) return Result<List<CategoryDto>>.Ok(cached);

        var categories = await _categoryRepository.GetByUserIdAsync(request.UserId, ct);
        await _cache.SetAsync(cacheKey, categories, TimeSpan.FromHours(1), ct);
        return Result<List<CategoryDto>>.Ok(categories);
    }
}
```

### Reports

```csharp
// GET /api/reports/monthly
public class GetMonthlyReportQueryHandler : IRequestHandler<GetMonthlyReportQuery, Result<MonthlyReportDto>>
{
    public async Task<Result<MonthlyReportDto>> Handle(GetMonthlyReportQuery request, ct)
    {
        var cacheKey = $"ledgerflow:report:monthly:{request.UserId}:{request.Year}:{request.Month}";
        var cached = await _cache.GetAsync<MonthlyReportDto>(cacheKey, ct);
        if (cached != null) return Result<MonthlyReportDto>.Ok(cached);

        var report = await _reportService.GenerateMonthlyReportAsync(request, ct);
        await _cache.SetAsync(cacheKey, report, TimeSpan.FromDays(1), ct);
        return Result<MonthlyReportDto>.Ok(report);
    }
}
```

### Invalidation

- `CreateCategory` → invalidate `categories:{userId}`
- `UpdateCategory` → invalidate `categories:{userId}`
- `DeleteCategory` → invalidate `categories:{userId}`

---

## 6. Background Jobs — Retry Policy

```csharp
public class HangfireOptions
{
    public static void Configure(IServiceProvider serviceProvider)
    {
        GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute
        {
            Attempts = 3,
            DelaysInSeconds = new[] { 30, 60, 300 },
            OnAttemptsExceeded = AttemptsExceededAction.Delete
        });
    }
}
```

---

## 7. Job Scheduling UI

O painel do Hangfire já provê interface para:
- Ver jobs agendados
- Disparar jobs manualmente
- Ver histórico de execuções
- Ver falhos com stack trace

---

## 8. Implementação — Ordem

1. Configurar Hangfire com PostgreSQL storage
2. Habilitar dashboard em dev
3. Implementar DailySummaryJob
4. Implementar MonthlyCloseJob
5. Implementar CacheExpirationJob
6. Criar endpoint CSV upload
7. Implementar CsvImportJob com retry
8. Expandir cache para Categories e Reports
9. Adicionar testes para jobs

---

## 9. Out of Scope

- RowVersion enforcement (Fase 4)
- Audit log (Fase 4)
- Notification real (email/push)

---

## 10. Dependências Novas

```xml
<PackageReference Include="Hangfire.Core" Version="1.8.6" />
<PackageReference Include="Hangfire.AspNetCore" Version="1.8.6" />
<PackageReference Include="Hangfire.PostgreSql" Version="1.9.9" />
<PackageReference Include="CsvHelper" Version="30.0.1" />
```

---

## 11. Critérios de Conclusão

- [ ] Hangfire dashboard acessível
- [ ] 3 jobs recorrentes configurados
- [ ] Importador CSV funcionando (linhas < 100 inline, > 100 async)
- [ ] Cache para Categories e Reports
- [ ] Retry policy configurado
- [ ] Testes para jobs