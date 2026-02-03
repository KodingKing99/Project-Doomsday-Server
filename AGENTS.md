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

Run all API tests:
```bash
dotnet test src/ProjectDoomsdayServer.ApiTests/ProjectDoomsdayServer.ApiTests.csproj
```

Run tests with verbose output:
```bash
dotnet test src/ProjectDoomsdayServer.ApiTests/ProjectDoomsdayServer.ApiTests.csproj --verbosity normal
```

Run a specific test:
```bash
dotnet test src/ProjectDoomsdayServer.ApiTests/ProjectDoomsdayServer.ApiTests.csproj --filter "FullyQualifiedName~TestMethodName"
```

## Project Structure

- `src/ProjectDoomsdayServer.WebApi` - ASP.NET Core Web API
- `src/ProjectDoomsdayServer.Application` - Application layer (use cases, interfaces)
- `src/ProjectDoomsdayServer.Domain` - Domain layer (entities, value objects)
- `src/ProjectDoomsdayServer.Infrastructure` - Infrastructure layer (external services, S3)
- `src/ProjectDoomsdayServer.ApiTests` - Integration tests using xUnit, FluentAssertions, NSubstitute

## Formatting

Format code with CSharpier:
```bash
dotnet csharpier format .
```
