# Project Doomsday Server - Architecture & Roadmap

## Project Overview

| Attribute | Value |
|-----------|-------|
| **Framework** | ASP.NET Core 9.0 (C#) |
| **Architecture** | Clean/Layered (Domain, Application, Infrastructure, WebApi, ApiTests) |
| **Blob Storage** | AWS S3 |
| **Metadata Storage** | MongoDB (driver installed) |
| **Current Storage** | In-Memory (development) |
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
│   └── Storage/
│       ├── S3FileStorage.cs                # AWS S3 implementation
│       └── InMemoryFileRepository.cs       # Development repository
├── ProjectDoomsdayServer.WebApi/           # API endpoints
│   └── Files/
│       └── FilesController.cs              # File CRUD endpoints
└── ProjectDoomsdayServer.ApiTests/         # Integration tests
```

---

## Current Implementation Status

| Feature | Status | Notes |
|---------|--------|-------|
| S3 Presigned URLs | ✅ Done | `GetPresignedUploadUrlAsync` implemented |
| S3 SaveAsync | ❌ Not implemented | Stub only |
| S3 OpenReadAsync | ❌ Not implemented | Stub only |
| S3 DeleteAsync | ❌ Not implemented | Stub only |
| S3 ExistsAsync | ❌ Not implemented | Stub only |
| MongoDB Repository | ❌ Not implemented | Driver installed, no implementation |
| Cognito Auth | ⚠️ Configured | Disabled in development |
| File CRUD Endpoints | ✅ Done | 7 endpoints available |
| CSV Export | ❌ Not started | - |
| Query Document API | ❌ Not started | - |

### File CRUD Endpoints (7 total)

1. `GET /files/presigned-upload-url?fileName={name}` - Get presigned S3 upload URL
2. `POST /files` - Upload file (multipart/form-data)
3. `GET /files` - List files with pagination
4. `GET /files/{id}` - Get file metadata
5. `GET /files/{id}/download` - Download file content
6. `PATCH /files/{id}/metadata` - Update file metadata
7. `DELETE /files/{id}` - Delete file

---

## Roadmap

### Phase 1: Complete S3 Integration

**Goal:** Full AWS S3 blob storage functionality

| Task | Interface Method | Description |
|------|-----------------|-------------|
| 1.1 | `SaveAsync(Guid id, Stream content, CancellationToken ct)` | Upload file content to S3 bucket |
| 1.2 | `OpenReadAsync(Guid id, CancellationToken ct)` | Stream file content from S3 |
| 1.3 | `DeleteAsync(Guid id, CancellationToken ct)` | Remove file from S3 bucket |
| 1.4 | `ExistsAsync(Guid id, CancellationToken ct)` | Check if file exists in S3 |

**Dependencies:** AWS SDK configured, S3 bucket created

---

### Phase 2: MongoDB Repository

**Goal:** Persistent file metadata storage

| Task | Description |
|------|-------------|
| 2.1 | Create `MongoFileRepository` implementing `IFileRepository` |
| 2.2 | Configure MongoDB connection string and database |
| 2.3 | Create indexes for common queries (by user, by date) |
| 2.4 | Replace `InMemoryFileRepository` DI registration |

**Dependencies:** MongoDB instance available

---

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

| Setting | Development | Production |
|---------|-------------|------------|
| File Storage | In-Memory | S3 |
| Metadata Storage | In-Memory | MongoDB |
| Authentication | Disabled | Cognito JWT |
