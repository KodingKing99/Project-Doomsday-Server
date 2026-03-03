# ProjectDoomsdayServer

## Build

Build the entire solution:
```bash
dotnet build ProjectDoomsdayServer.sln
```

Build just the WebApi project:
```bash
dotnet build src/ProjectDoomsdayServer.WebApi/ProjectDoomsdayServer.WebApi.csproj
```

## Run

Run the WebApi server:
```bash
dotnet run --project src/ProjectDoomsdayServer.WebApi/ProjectDoomsdayServer.WebApi.csproj
```

## Validation 

Run these after implementing changes:

**Default — failures and summary only (use this first):**
```bash
dotnet test --verbosity minimal 2>&1 | head -n 150
```

**Mocked integration tests only (no Docker required):**
```bash
dotnet test --filter "FullyQualifiedName~ApiTests" --verbosity minimal 2>&1 | head -n 150
```

**E2E tests only (requires Docker):**
```bash
dotnet test --filter "FullyQualifiedName~E2ETests" --verbosity minimal 2>&1 | head -n 150
```

**Specific test class or method:**
```bash
dotnet test --filter "FullyQualifiedName~FileCrudIntegrationTests" --verbosity minimal 2>&1 | head -n 100
dotnet test --filter "FullyQualifiedName~FileCrudIntegrationTests.CreateFile_ReturnsCreated" --verbosity minimal 2>&1 | head -n 100
```

**If failures need more context (stack traces, 20 lines after each failure):**
```bash
dotnet test 2>&1 | grep -A 20 "Failed\|Error\|Exception" | head -n 200
```

**Full output (only if above are insufficient):**
```bash
dotnet test --verbosity normal 2>&1
```
- format/lint: dotnet csharpier format .

## Project Structure

- `src/ProjectDoomsdayServer.WebApi` - ASP.NET Core Web API
- `src/ProjectDoomsdayServer.Application` - Application layer (use cases, interfaces)
- `src/ProjectDoomsdayServer.Domain` - Domain layer (entities, value objects)
- `src/ProjectDoomsdayServer.Infrastructure` - Infrastructure layer (MongoDB, S3/MinIO)
- `src/ProjectDoomsdayServer.ApiTests` - Mocked integration tests using xUnit, FluentAssertions, NSubstitute
- `src/ProjectDoomsdayServer.E2ETests` - Full-stack E2E tests using Testcontainers (MongoDB + MinIO)
