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

## Test

Run all tests (requires Docker for E2E containers):
```bash
dotnet test
```

Run only mocked integration tests (no Docker required):
```bash
dotnet test --filter "FullyQualifiedName~ApiTests"
```

Run only E2E tests (requires Docker):
```bash
dotnet test --filter "FullyQualifiedName~E2ETests"
```

Run a specific test class or method:
```bash
dotnet test --filter "FullyQualifiedName~FileCrudIntegrationTests"
dotnet test --filter "FullyQualifiedName~FileCrudIntegrationTests.CreateFile_ReturnsCreated"
```

Run tests with verbose output:
```bash
dotnet test --verbosity normal
```

## Project Structure

- `src/ProjectDoomsdayServer.WebApi` - ASP.NET Core Web API
- `src/ProjectDoomsdayServer.Application` - Application layer (use cases, interfaces)
- `src/ProjectDoomsdayServer.Domain` - Domain layer (entities, value objects)
- `src/ProjectDoomsdayServer.Infrastructure` - Infrastructure layer (MongoDB, S3/MinIO)
- `src/ProjectDoomsdayServer.ApiTests` - Mocked integration tests using xUnit, FluentAssertions, NSubstitute
- `src/ProjectDoomsdayServer.E2ETests` - Full-stack E2E tests using Testcontainers (MongoDB + MinIO)

## Formatting

Format code with CSharpier:
```bash
dotnet csharpier format .
```
