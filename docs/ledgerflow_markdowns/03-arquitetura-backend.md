# Arquitetura Backend

## Stack
- ASP.NET Core
- Entity Framework Core
- PostgreSQL
- Redis
- FluentValidation
- Hangfire
- xUnit

## Estrutura

```txt
src/
├── LedgerFlow.Api/
├── LedgerFlow.Application/
├── LedgerFlow.Domain/
├── LedgerFlow.Infrastructure/
└── LedgerFlow.Shared/
```

## Conceitos
- Clean Architecture
- MVC + Services + Repositories
- Repository Pattern
- Unit of Work
- Result<T> Pattern