# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

LedgerFlow is a personal/professional financial management platform built to demonstrate modern enterprise architecture. The project starts as a well-structured modular monolith and evolves toward enterprise patterns.

The codebase does not exist yet — planning docs live in `ledgerflow_markdowns/`.

## Planned Tech Stack

**Backend**: C# / ASP.NET Core, Entity Framework Core, PostgreSQL, Redis, FluentValidation, Hangfire, xUnit  
**Frontend**: Angular, RxJS, Angular Signals, Angular Material, Chart.js  
**Infrastructure**: Docker (API, PostgreSQL, Redis, Angular containers)

## Backend Architecture

Clean Architecture with MVC + Services + Repositories. Project layout:

```
src/
├── LedgerFlow.Api/              # Controllers, middleware, DI composition root
├── LedgerFlow.Application/     # DTOs, Service interfaces
├── LedgerFlow.Domain/           # Entities, Enums, Repository interfaces
├── LedgerFlow.Infrastructure/   # EF Core DbContext, Repositories, Redis
└── LedgerFlow.Shared/          # Helpers (Result<T>, Error)
```

Key patterns: Repository + Unit of Work, Result<T> Pattern, Dependency Injection.

## Frontend Architecture

Angular app with lazy-loaded feature modules:

```
src/app/
├── core/       # Singleton services, interceptors, guards, app-wide config
├── shared/     # Reusable components, pipes, directives
├── modules/    # Feature modules (auth, accounts, transactions, dashboard) — lazy loaded
├── layout/     # Shell, navbar, sidebar
└── state/      # Angular Signals-based state management
```

## Domain Model

Core entities: `User` (Id, Name, Email), `Account` (Id, UserId, Balance), `Transaction` (Id, AccountId, Amount, Type), `Category` (Id, Name, Type).

## Roadmap Phases

1. **MVP**: Auth (register/login/refresh/password reset), Accounts CRUD, Transactions (income/expense/transfer), Dashboard
2. **Enterprise patterns**: FluentValidation, Logging centralizado, Global exception handler, unit + integration tests
3. **Async processing**: Hangfire jobs, CSV importer, Redis caching (dashboard, reports, categories)
4. **Concurrency**: RowVersion optimistic concurrency, audit log

## Commands

> Commands will be added here once the project is scaffolded. Expected entry points:
> - `dotnet build` / `dotnet test` (backend)
> - `ng serve` / `ng test` / `ng build` (frontend)
> - `docker compose up` (full stack)
