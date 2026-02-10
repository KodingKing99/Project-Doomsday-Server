# Project Doomsday Server

## Project Architecture
This is a dotnet 9 project following hexagonal/clean/layered architecture.

```
src/
├── ProjectDoomsdayServer.Domain          (innermost - no dependencies)
│   ├── DB_Models/        File.cs
│   └── Configuration/    MongoDbConfig.cs, S3Config.cs
│
├── ProjectDoomsdayServer.Application     (depends on: Domain)
│   └── Files/            IFilesService, FilesService, IFileStorage, Ports
│
├── ProjectDoomsdayServer.Infrastructure  (depends on: Application, Domain)
│   └── Files/            MongoDbFileRepository, S3FileStorage,
│                         LocalFileStorage, InMemoryFileRepository
│                         InfrastructureServiceCollectionExtensions (DI registration)
│
├── ProjectDoomsdayServer.WebApi          (depends on: Application, Infrastructure)
│   ├── Controllers/      FilesController, WeatherForecastController
│   └── Program.cs        (app entry point + DI composition root)
│
└── ProjectDoomsdayServer.ApiTests        (depends on: WebApi)
    ├── Files/            Integration tests for Files endpoints
    └── TestSupport/      CustomWebApplicationFactory, TestHelpers

Dependency flow (outside-in): WebApi → Infrastructure → Application → Domain
Ports/adapters: Application defines interfaces (IFileStorage, ports), Infrastructure implements them.
Storage: MongoDB for metadata, S3/Local for file blobs.
Test stack: xUnit, FluentAssertions, NSubstitute, Microsoft.AspNetCore.Mvc.Testing.
```

## Formatting
To format code, run: `dotnet csharpier format .`
