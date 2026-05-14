# LedgerFlow — Fase 2: Arquitetura Enterprise

**Date:** 2026-05-14
**Phase:** 2/4
**Prerequisite:** Fase 1 (MVP) completa
**Arquitetura:** MVC + Services + Repositories (expansão)

---

## 1. Visão Geral

 nesta fase vamos expandir a arquitetura com:
- Validação robusta com FluentValidation
- Logging e tratamento de erros centralizado
- Testes unitários e de integração
- Aperfeicoamento de services existentes

---

## 2. FluentValidation — Interfaces e Implementacoes

### Validadores

```csharp
// Application/Validators/AuthValidators.cs
public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain at least one number");
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required");
    }
}

// Application/Validators/AccountValidators.cs
public class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Account name is required")
            .MaximumLength(100).WithMessage("Account name cannot exceed 100 characters");

        RuleFor(x => x.InitialBalance)
            .GreaterThanOrEqualTo(0).WithMessage("Initial balance cannot be negative")
            .LessThanOrEqualTo(999999999).WithMessage("Initial balance exceeds maximum allowed");
    }
}

// Application/Validators/TransactionValidators.cs
public class CreateTransactionRequestValidator : AbstractValidator<CreateTransactionRequest>
{
    public CreateTransactionRequestValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty().WithMessage("Account is required");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero")
            .LessThanOrEqualTo(999999999).WithMessage("Amount exceeds maximum allowed");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid transaction type");

        RuleFor(x => x)
            .Must(x => x.Type != TransactionType.Transfer || x.TransferToAccountId.HasValue)
            .WithMessage("Transfer transactions require a destination account")
            .Must(x => x.Type != TransactionType.Transfer || x.AccountId != x.TransferToAccountId)
            .WithMessage("Cannot transfer to the same account");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters");
    }
}
```

### Integracao com Services

```csharp
public interface IValidationService
{
    Task<Result<T>> ValidateAsync<T>(T request);
}

public class ValidationService : IValidationService
{
    private readonly IServiceProvider _serviceProvider;

    public async Task<Result<T>> ValidateAsync<T>(T request)
    {
        var validatorType = typeof(AbstractValidator<>).MakeGenericType(request.GetType());
        var validator = _serviceProvider.GetService(validatorType) as IValidator;

        if (validator == null)
            return Result<T>.Ok(request); // No validator found, skip validation

        var context = new ValidationContext<T>(request);
        var result = await validator.ValidateAsync(context);

        if (!result.IsValid)
        {
            var errors = result.Errors.Select(e => new Error("validation_error", e.ErrorMessage));
            return Result<T>.Fail(errors);
        }

        return Result<T>.Ok(request);
    }
}

// Registration in Program.cs
services.AddScoped<IValidationService, ValidationService>();
services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
```

---

## 3. Logging Centralizado

### Logging Interface

```csharp
public interface ILoggerService
{
    void LogInformation(string message, params object[] args);
    void LogWarning(string message, params object[] args);
    void LogError(Exception ex, string message, params object[] args);
    void LogDebug(string message, params object[] args);
}

public class LoggerService : ILoggerService
{
    private readonly ILogger<LoggerService> _logger;

    public LoggerService(ILogger<LoggerService> logger)
    {
        _logger = logger;
    }

    public void LogInformation(string message, params object[] args)
        => _logger.LogInformation(message, args);

    public void LogWarning(string message, params object[] args)
        => _logger.LogWarning(message, args);

    public void LogError(Exception ex, string message, params object[] args)
        => _logger.LogError(ex, message, args);

    public void LogDebug(string message, params object[] args)
        => _logger.LogDebug(message, args);
}
```

### Integracao nos Services

```csharp
public class AccountService : IAccountService
{
    private readonly IAccountRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly ILoggerService _logger;

    public async Task<Result<Guid>> CreateAsync(CreateAccountRequest request, Guid userId, CancellationToken ct)
    {
        _logger.LogInformation("Creating account for user {UserId}", userId);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            _logger.LogWarning("Account creation failed: empty name");
            return Result<Guid>.Fail(new Error("validation_error", "Account name is required"));
        }

        var account = new Account { ... };

        await _repository.AddAsync(account, ct);
        await _unitOfWork.CommitAsync(ct);

        _logger.LogInformation("Account {AccountId} created successfully", account.Id);
        return Result<Guid>.Ok(account.Id);
    }
}
```

---

## 4. Tratamento de Erros Avancado

### Custom Exceptions

```csharp
public class LedgerFlowException : Exception
{
    public string Code { get; }
    public LedgerFlowException(string code, string message) : base(message)
    {
        Code = code;
    }
}

public class NotFoundException : LedgerFlowException
{
    public NotFoundException(string entity, Guid id)
        : base("not_found", $"{entity} with id {id} not found") { }
}

public class ValidationException : LedgerFlowException
{
    public ValidationException(string message)
        : base("validation_error", message) { }
}

public class UnauthorizedException : LedgerFlowException
{
    public UnauthorizedException(string message = "Unauthorized")
        : base("unauthorized", message) { }
}
```

### Exception Handler Middleware

```csharp
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (statusCode, title, detail) = exception switch
        {
            NotFoundException => (404, "Not Found", exception.Message),
            ValidationException => (400, "Validation Error", exception.Message),
            UnauthorizedException => (401, "Unauthorized", exception.Message),
            _ => (500, "Internal Server Error", "An unexpected error occurred")
        };

        _logger.LogError(exception, "Exception: {Title}", title);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(new
        {
            type = $"https://httpstatuses.com/{statusCode}",
            title,
            detail
        });

        return true;
    }
}
```

---

## 5. Testes Unitarios

### Estrutura

```
tests/LedgerFlow.Unit.Tests/
├── Services/
│   ├── AuthServiceTests.cs
│   ├── AccountServiceTests.cs
│   ├── TransactionServiceTests.cs
│   └── DashboardServiceTests.cs
├── Validators/
│   ├── RegisterRequestValidatorTests.cs
│   ├── CreateAccountRequestValidatorTests.cs
│   └── CreateTransactionRequestValidatorTests.cs
└── Shared/
    ├── ResultTests.cs
    └── ErrorTests.cs
```

### Exemplos

```csharp
public class AccountServiceTests
{
    private readonly Mock<IAccountRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly Mock<ILoggerService> _loggerMock;
    private readonly AccountService _service;

    public AccountServiceTests()
    {
        _repositoryMock = new Mock<IAccountRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _cacheMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILoggerService>();
        _service = new AccountService(_repositoryMock.Object, _unitOfWorkMock.Object, _cacheMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_ReturnsAccountId()
    {
        // Arrange
        var request = new CreateAccountRequest("Test Account", 1000m);
        var userId = Guid.NewGuid();

        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.CreateAsync(request, userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_WithEmptyName_ReturnsFailure()
    {
        // Arrange
        var request = new CreateAccountRequest("", 1000m);
        var userId = Guid.NewGuid();

        // Act
        var result = await _service.CreateAsync(request, userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "validation_error");
    }
}

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    [Theory]
    [InlineData("", "Name is required")]
    [InlineData("ab", "Name cannot exceed 100 characters")]
    public void Validate_Name_ReturnsErrors(string name, string expectedError)
    {
        var request = new RegisterRequest(name, "test@test.com", "Password1");

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == expectedError);
    }

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        var request = new RegisterRequest("Test User", "test@test.com", "Password1");

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }
}
```

### Dependencies

```xml
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
<PackageReference Include="xunit" Version="2.6.2" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.5.4" />
<PackageReference Include="Moq" Version="4.20.70" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
```

---

## 6. Testes de Integracao

### Estrutura

```
tests/LedgerFlow.Integration.Tests/
├── Api/
│   ├── AuthControllerTests.cs
│   ├── AccountsControllerTests.cs
│   ├── TransactionsControllerTests.cs
│   └── DashboardControllerTests.cs
└── Fixtures/
    └── IntegrationTestFixture.cs
```

### Fixture

```csharp
public class IntegrationTestFixture : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().Build();
    private readonly RedisContainer _redis = new RedisBuilder().Build();

    public string ConnectionString => _postgres.GetConnectionString();
    public string RedisConnectionString => _redis.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(sp => sp.GetServiceType() == typeof(DbContextOptions<AppDbContext>));
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(ConnectionString));

            services.RemoveAll(sp => sp.GetServiceType() == typeof(IConnectionMultiplexer));
            services.AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect(RedisConnectionString));
        });
    }
}

public class AccountsControllerTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;
    private readonly HttpClient _client;

    public AccountsControllerTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task CreateAccount_WithValidData_ReturnsCreated()
    {
        // Arrange
        var request = new { Name = "Test Account", InitialBalance = 1000m };
        var token = await GetAuthToken();

        // Act
        var response = await _client.PostAsJsonAsync("/api/accounts", request, new CancellationToken());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```

---

## 7. Implementacao — Ordem

1. Adicionar FluentValidation NuGet packages
2. Criar validadores em `Application/Validators/`
3. Implementar `IValidationService`
4. Implementar `ILoggerService` + `ExceptionHandler`
5. Criar custom exceptions
6. Atualizar Services para usar validation + logging
7. Criar unit tests para Services
8. Criar unit tests para Validators
9. Criar integration tests
10. Configurar TestContainers

---

## 8. Out of Scope

- Hangfire jobs (Fase 3)
- CSV import (Fase 3)
- RowVersion enforcement (Fase 4)
- Audit log (Fase 4)

---

## 9. Critérios de Conclusão

- [ ] FluentValidation em todos os requests
- [ ] Logging centralizado nos Services
- [ ] Global exception handler funcionando
- [ ] 80%+ coverage em unit tests
- [ ] Integration tests passando
- [ ] Build verde em CI