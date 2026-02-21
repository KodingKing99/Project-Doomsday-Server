# Project Doomsday Server - Architecture & Roadmap

## Project Overview

| Attribute | Value |
|-----------|-------|
| **Framework** | ASP.NET Core 9.0 (C#) |
| **Architecture** | Clean/Hexagonal (Domain, Application, Infrastructure, WebApi, ApiTests, E2ETests) |
| **Blob Storage** | AWS S3 (production) / MinIO via Testcontainers (E2E tests) |
| **Metadata Storage** | MongoDB |
| **Authentication** | Amazon Cognito (JWT Bearer) |

---

## Project Structure

```
src/
├── ProjectDoomsdayServer.Domain/           # Core entities and interfaces
├── ProjectDoomsdayServer.Application/      # Use cases, services, interfaces
│   └── Files/
│       ├── IFileStorage.cs                 # Blob storage abstraction
│       └── IFileRepository.cs              # Metadata repository abstraction
├── ProjectDoomsdayServer.Infrastructure/   # External implementations
│   └── Files/
│       ├── S3FileStorage.cs                # AWS S3 implementation
│       ├── LocalFileStorage.cs             # Local filesystem implementation
│       ├── MongoDbFileRepository.cs        # MongoDB implementation
│       └── InMemoryFileRepository.cs       # In-memory fallback
├── ProjectDoomsdayServer.WebApi/           # API endpoints
│   └── Files/
│       └── FilesController.cs              # File CRUD endpoints
├── ProjectDoomsdayServer.ApiTests/         # Mocked integration tests (NSubstitute, no Docker)
└── ProjectDoomsdayServer.E2ETests/         # Full-stack E2E tests (Testcontainers, requires Docker)
    └── Files/
        ├── PresignedUploadE2ETests.cs      # Presigned URL upload → download round-trip
        ├── FileCrudE2ETests.cs             # CRUD against real MongoDB + MinIO
        └── FileDownloadE2ETests.cs         # Download edge cases
```

---

## Current Implementation Status

| Feature | Status | Notes |
|---------|--------|-------|
| S3 Presigned URLs | ✅ Done | Returned in `POST /files` response |
| S3 Full Implementation | ✅ Done | Save, OpenRead, Delete, Exists all implemented |
| MongoDB Repository | ✅ Done | `MongoDbFileRepository` with `StringObjectIdGenerator` |
| Mocked Integration Tests | ✅ Done | 29 tests in `ApiTests` — no Docker required |
| E2E Tests | ✅ Done | 12 tests in `E2ETests` — real MongoDB + MinIO via Testcontainers |
| Cognito Auth | ⚠️ Configured | Disabled in development, toggled via `Authentication:Enabled` |
| CSV Export | ❌ Not started | - |
| Query Document API | ❌ Not started | - |

### File CRUD Endpoints

1. `POST /files` — Create file metadata, returns presigned S3 PUT URL for direct client upload
2. `PUT /files/{id}` — Update file metadata
3. `GET /files` — List files with pagination (`?skip=0&take=50`, max 200, sorted by `UpdatedAtUtc` desc)
4. `GET /files/{id}` — Get file metadata
5. `GET /files/{id}/content` — Download file content
6. `DELETE /files/{id}` — Delete file and storage object

---

## Roadmap

### ~~Phase 1: Complete S3 Integration~~ ✅ Done

### ~~Phase 2: MongoDB Repository~~ ✅ Done

### Phase 3: Authentication

**Goal:** Secure API with Amazon Cognito

| Task | Description |
|------|-------------|
| 3.1 | Enable Cognito authentication middleware |
| 3.2 | Configure user pool and app client settings |
| 3.3 | Add `[Authorize]` attributes to protected endpoints |
| 3.4 | Extract user ID from JWT claims for file ownership |

**Dependencies:** Cognito user pool configured

---

### Phase 4: Query Document API

**Goal:** Third-party API integration for document queries

| Task | Description |
|------|-------------|
| 4.1 | Define query request/response models |
| 4.2 | Create document query service |
| 4.3 | Implement query endpoint |
| 4.4 | Add rate limiting and error handling |

---

### Phase 5: CSV Export

**Goal:** Export receipt data as CSV

| Task | Description |
|------|-------------|
| 5.1 | Implement receipt data extraction from files |
| 5.2 | Create CSV generation service |
| 5.3 | Add CSV export endpoint |
| 5.4 | Support filtering and date ranges |

---

## Core Requirements Checklist

- [ ] **Cognito Authentication** - JWT Bearer auth with Amazon Cognito
- [x] **File CRUD** - Upload, list, get, update metadata, download, delete
- [x] **S3 Storage** - Full presigned URL workflow + read/delete/exists
- [x] **MongoDB** - Persistent metadata with `MongoDbFileRepository`
- [x] **E2E Test Coverage** - Real infrastructure validation via Testcontainers
- [ ] **Query Document API** - Third-party document query integration
- [ ] **CSV Export** - Receipt data export functionality

---

## Configuration

### Environment Variables

```bash
# AWS
AWS_REGION=us-east-1
AWS_S3_BUCKET=doomsday-files

# MongoDB
MONGODB_CONNECTION_STRING=mongodb://localhost:27017
MONGODB_DATABASE_NAME=doomsday

# Cognito
COGNITO_USER_POOL_ID=us-east-1_xxxxx
COGNITO_CLIENT_ID=xxxxx
```

### Development vs Production

| Setting | Development | E2E Tests | Production |
|---------|-------------|-----------|------------|
| File Storage | S3 (local profile) | MinIO (Testcontainers) | S3 |
| Metadata Storage | MongoDB (localhost) | MongoDB (Testcontainers) | MongoDB |
| Authentication | Disabled | Disabled | Cognito JWT |
