FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files for layer caching
COPY LedgerFlow.sln .
COPY src/LedgerFlow.Api/LedgerFlow.Api.csproj src/LedgerFlow.Api/
COPY src/LedgerFlow.Application/LedgerFlow.Application.csproj src/LedgerFlow.Application/
COPY src/LedgerFlow.Domain/LedgerFlow.Domain.csproj src/LedgerFlow.Domain/
COPY src/LedgerFlow.Infrastructure/LedgerFlow.Infrastructure.csproj src/LedgerFlow.Infrastructure/
COPY src/LedgerFlow.Shared/LedgerFlow.Shared.csproj src/LedgerFlow.Shared/

RUN dotnet restore src/LedgerFlow.Api/LedgerFlow.Api.csproj

# Copy all source files
COPY src/ src/

RUN dotnet publish src/LedgerFlow.Api/LedgerFlow.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "LedgerFlow.Api.dll"]
