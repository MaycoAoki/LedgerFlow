# LedgerFlow

Uma plataforma de gestão financeira pessoal e profissional construída para demonstrar arquitetura enterprise moderna.

## Tech Stack

### Backend

- **.NET 8** — Runtime
- **ASP.NET Core** — Framework web
- **Entity Framework Core** — ORM
- **PostgreSQL** — Banco de dados relacional
- **Redis** — Cache
- **FluentValidation** — Validação de requests
- **Hangfire** — Processamento de jobs (Fase 3)

### Frontend

- **Angular 17+** — Framework frontend
- **Angular Material** — UI components
- **RxJS** — Programação reativa
- **Chart.js** — Gráficos e visualizações

### Infraestrutura

- **Docker** — Containerização
- **Nginx** — Servidor web (frontend)

## Arquitetura

O projeto segue o padrão de **Clean Architecture** com **MVC + Services + Repositories**, organizado em camadas bem definidas:

```
src/
├── LedgerFlow.Api/              # Controllers, middleware, composição DI
├── LedgerFlow.Application/     # DTOs, Interfaces de Services
├── LedgerFlow.Domain/           # Entities, Enums, Interfaces
├── LedgerFlow.Infrastructure/   # EF Core, Repositories, Redis
└── LedgerFlow.Shared/          # Helpers compartilhados (Result, Error)
```

### Padrões Implementados

- **Repository + Unit of Work** — Abstração de acesso a dados
- **Result<T> Pattern** — Tratamento de erros funcional
- **Dependency Injection** — Inversão de controle
- **Cache com Redis** — Otimização de performance

## Funcionalidades

### Fase 1 — MVP (Implementado)

- **Autenticação**: Registro, Login, Refresh Token, Reset de Senha
- **Contas**: CRUD completo de contas bancárias
- **Transações**: Registro de receitas, despesas e transferências
- **Dashboard**: Visão geral de finances pessoais

### Fase 2 — Arquitetura Enterprise (Em desenvolvimento)

- FluentValidation
- Logging centralizado
- Global exception handler
- Testes unitários e de integração

### Fase 3 — Processamento Assíncrono

- Jobs agendados com Hangfire
- Importador de CSV
- Cache com Redis

### Fase 4 — Concorrência

- RowVersion para optimistic concurrency
- Sistema de auditoria

## Como Executar

### Pré-requisitos

- Docker Desktop
- Docker Compose

### Configuração

1. Clone o repositório:
```bash
git clone https://github.com/seu-usuario/ledgerflow.git
cd ledgerflow
```

2. Configure as variáveis de ambiente (ou use os valores padrão):
```bash
cp .env.example .env
```

3. Execute o projeto com Docker Compose:
```bash
docker compose up --build
```

### Serviços Disponíveis

| Serviço | URL | Porta |
|---------|-----|-------|
| API | http://localhost:5000 | 5000 |
| Frontend | http://localhost:4200 | 4200 |
| PostgreSQL | localhost:5432 | 5432 |
| Redis | localhost:6379 | 6379 |

### Comandos Úteis

```bash
# Ver logs da API
docker compose logs -f api

# Rebuild parcial
docker compose build api

# Parar todos os serviços
docker compose down
```

## Contribuição

1. Fork o projeto
2. Crie uma branch (`git checkout -b feature/nova-funcionalidade`)
3. Commit suas mudanças (`git commit -m 'Add nova funcionalidade'`)
4. Push para a branch (`git push origin feature/nova-funcionalidade`)
5. Abra um Pull Request

## Licença

Este projeto está sob a licença MIT. Veja o arquivo [LICENSE](LICENSE) para detalhes.