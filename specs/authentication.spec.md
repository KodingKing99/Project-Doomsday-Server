# Authentication — Cognito JWT Bearer

**Prerequisite:** CDK stack (`DoomsdayStack`) is deployed. `CognitoAuthority` and `UserPoolClientId` outputs are available.

## Overview

All API endpoints require a valid Cognito JWT access token. Authentication is toggled via `Authentication:Enabled` in config. The JWT bearer scaffolding already exists in `Program.cs`; the `File` domain model already has a `UserId` field. This spec covers wiring Cognito values into config, enforcing `[Authorize]`, scoping data to the authenticated user, and updating tests.

---

## Configuration

`appsettings.Development.json` must have:

```json
"Authentication": {
  "Enabled": true,
  "Cognito": {
    "Authority": "<CognitoAuthority stack output>",
    "Audience": "<UserPoolClientId stack output>"
  }
}
```

> For production, supply these via environment variables or AWS SSM — never commit real values.

To retrieve stack outputs:
```bash
aws cloudformation describe-stacks \
  --stack-name DoomsdayStack \
  --query "Stacks[0].Outputs" \
  --profile DoomsdayAdmin-027903755990
```

---

## JWT Validation (`Program.cs`)

The `AddJwtBearer` block must validate:

```csharp
.AddJwtBearer(o =>
{
    o.Authority = builder.Configuration["Authentication:Cognito:Authority"];
    o.Audience = builder.Configuration["Authentication:Cognito:Audience"];
    o.RequireHttpsMetadata = true;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        NameClaimType = "sub",  // Cognito puts user ID in "sub"
    };
});
```

---

## Authorization

`FilesController` must require authentication at the class level:

```csharp
[ApiController]
[Route("files")]
[Authorize]
public sealed class FilesController : ControllerBase { ... }
```

---

## User Scoping

`UserId` must always be derived from the validated JWT `sub` claim — never from the request body.

### Controller

```csharp
var userId = User.FindFirstValue("sub")!;
```

Pass `userId` into all service calls (create, list, get, delete).

### `IFilesService` / `FilesService`

Add `userId` parameter to `CreateAsync`, `ListAsync`, `GetAsync`, `DeleteAsync`.

### `IFileRepository` / implementations

Add `userId` parameter to `ListAsync`, `GetAsync`, `DeleteAsync`.

**`MongoDbFileRepository`** — filter queries by `UserId`:

```csharp
// ListAsync
var filter = Builders<File>.Filter.Eq(f => f.UserId, userId);

// GetAsync / DeleteAsync — filter by both id AND userId
var filter = Builders<File>.Filter.And(
    Builders<File>.Filter.Eq(f => f.Id, id),
    Builders<File>.Filter.Eq(f => f.UserId, userId)
);
```

**`InMemoryFileRepository`** — apply equivalent LINQ `UserId` filter.

### Update (ownership check)

Before allowing an update, verify the existing record belongs to the current user:

```csharp
var existing = await _filesService.GetAsync(id, userId, ct);
if (existing is null) return NotFound();
```

---

## MongoDB Index

Add a compound index on `(UserId, UpdatedAtUtc)` at startup for efficient per-user list queries:

```csharp
var indexKeys = Builders<File>.IndexKeys
    .Ascending(f => f.UserId)
    .Descending(f => f.UpdatedAtUtc);
await _collection.Indexes.CreateOneAsync(new CreateIndexModel<File>(indexKeys));
```

---

## Security Rules

- **Never trust client-supplied `UserId`** — always use the JWT `sub` claim.
- Return **404 Not Found** (not 403) when a user accesses another user's resource, to avoid leaking resource existence.
- The storage key format `{UserId}/{FileId}` naturally partitions S3 objects by user.
- `RequireHttpsMetadata = true` must be set.

---

## Test Support

### Option A — Disable auth in tests (default)

`Authentication:Enabled` is `false` by default in the test factory. Existing tests continue to work without tokens.

### Option B — Test JWT handler (for auth integration tests)

Add a fake authentication handler to `CustomWebApplicationFactory` that injects a controllable `sub` claim:

```csharp
// In CustomWebApplicationFactory.ConfigureWebHost:
builder.ConfigureTestServices(services =>
{
    services.AddAuthentication("Test")
        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
});
```

```csharp
// TestSupport/TestAuthHandler.cs
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string UserId = "test-user-id";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[] { new Claim("sub", UserId) };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

Tests can assert that files created by `test-user-id` are not visible to other users.

---

## Manual Token Acquisition

```bash
aws cognito-idp initiate-auth \
  --auth-flow USER_PASSWORD_AUTH \
  --client-id <app-client-id> \
  --auth-parameters USERNAME=testuser@example.com,PASSWORD=YourPassword123! \
  --region us-west-2
```

Use the `AccessToken` as the Bearer token:

```bash
curl -H "Authorization: Bearer <AccessToken>" https://localhost:7xxx/files
```

---

## Files to Modify

| File | Change |
|------|--------|
| `appsettings.Development.json` | Fill in Cognito values, set `Enabled: true` |
| `Program.cs` | Harden `TokenValidationParameters` |
| `FilesController.cs` | Add `[Authorize]`, extract `sub` claim, pass to service |
| `IFilesService.cs` | Add `userId` params to relevant methods |
| `FilesService.cs` | Pass `userId` through to repository |
| `Ports.cs` (`IFileRepository`) | Add `userId` params to relevant methods |
| `MongoDbFileRepository.cs` | Add `UserId` filter to queries, add compound index |
| `InMemoryFileRepository.cs` | Add `userId` filter to queries |
| `CustomWebApplicationFactory.cs` | Add `TestAuthHandler` (Option B) |
