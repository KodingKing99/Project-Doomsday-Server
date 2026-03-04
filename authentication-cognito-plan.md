# Cognito Authentication Implementation Plan

**Assumes:** `doomsday-cdk-plan.md` has been completed. The Cognito User Pool and App Client are deployed, and CDK stack outputs (`CognitoAuthority`, `UserPoolClientId`) are available.

## Overview

The JWT bearer scaffolding already exists in `Program.cs` and is guarded by the `Authentication:Enabled` config flag. The `File` domain model already has a `UserId` field. This plan completes the implementation: wiring the CDK-deployed Cognito values into config, enabling the middleware, enforcing `[Authorize]`, scoping data to the authenticated user, and updating tests.

---

## Step 1 — Update Configuration

Retrieve the CDK stack outputs if you don't have them handy:

```bash
aws cloudformation describe-stacks \
  --stack-name DoomsdayStack \
  --query "Stacks[0].Outputs" \
  --profile DoomsdayAdmin-027903755990
```

Copy the `CognitoAuthority` and `UserPoolClientId` values into **`appsettings.Development.json`** and enable auth:

```json
"Authentication": {
  "Enabled": true,
  "Cognito": {
    "Authority": "<CognitoAuthority output>",
    "Audience": "<UserPoolClientId output>"
  }
}
```

> For production, supply these via environment variables or AWS Systems Manager Parameter Store — never commit real values to source control.

---

## Step 2 — Verify / Harden JWT Configuration in `Program.cs`

The existing `AddJwtBearer` block (`Program.cs:70-77`) already reads `Authority` and `Audience` from config. Two improvements worth considering:

1. **Validate token use** — Cognito `access_token`s have `token_use: "access"`. Verify this claim to reject `id_token`s being used as bearer tokens.

2. **Map the subject claim** — Cognito puts the user's unique ID in the `sub` claim. ASP.NET Core maps this to `ClaimTypes.NameIdentifier` automatically, but confirm the correct claim type is used downstream.

Updated JWT options in `Program.cs`:

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
        // Cognito puts user ID in "sub"
        NameClaimType = "sub",
    };
});
```

---

## Step 3 — Add `[Authorize]` to `FilesController`

**`FilesController.cs`** — add the attribute at the class level so all endpoints require a valid token:

```csharp
[ApiController]
[Route("files")]
[Authorize]   // ← add this
public sealed class FilesController : ControllerBase
{ ... }
```

This requires adding `using Microsoft.AspNetCore.Authorization;`.

---

## Step 4 — Scope Data to the Authenticated User

### 5a. Extract `UserId` in the controller

The authenticated user's Cognito `sub` (unique UUID per user) is available via `User.FindFirstValue(ClaimTypes.NameIdentifier)` or `User.FindFirstValue("sub")`.

Pass it into service calls instead of relying on the client-supplied body:

```csharp
// In Create:
record.UserId = User.FindFirstValue("sub");

// In List — pass userId so only that user's files are returned:
Ok(await _filesService.ListAsync(userId, skip, Math.Clamp(take, 1, 200), ct))
```

### 5b. Update `IFilesService` and `FilesService`

Add `userId` parameter to `CreateAsync` and `ListAsync` (and `GetAsync`/`DeleteAsync` for ownership checks):

```csharp
// IFilesService
Task<CreateFileResult> CreateAsync(File record, string userId, CancellationToken ct);
Task<IReadOnlyList<File>> ListAsync(string userId, int skip, int take, CancellationToken ct);
Task<File?> GetAsync(string id, string userId, CancellationToken ct);
Task DeleteAsync(string id, string userId, CancellationToken ct);
```

In `FilesService.CreateAsync`, the `record.UserId` is already set by the controller, so the storage key `{UserId}/{FileId}` will be correct.

### 5c. Update `IFileRepository` and implementations

Add `userId` filtering to `ListAsync` and ownership validation to `GetAsync`/`DeleteAsync`:

```csharp
// IFileRepository (Ports.cs)
Task<File?> GetAsync(string id, string userId, CancellationToken ct);
Task<IReadOnlyList<File>> ListAsync(string userId, int skip, int take, CancellationToken ct);
Task DeleteAsync(string id, string userId, CancellationToken ct);
```

**`MongoDbFileRepository`** — add a `UserId` filter to queries:

```csharp
// ListAsync
var filter = Builders<File>.Filter.Eq(f => f.UserId, userId);
// ... apply filter to Find()

// GetAsync — filter by both id AND userId to prevent cross-user access
var filter = Builders<File>.Filter.And(
    Builders<File>.Filter.Eq(f => f.Id, id),
    Builders<File>.Filter.Eq(f => f.UserId, userId)
);
```

**`InMemoryFileRepository`** — apply the same `UserId` filter in LINQ.

### 5d. Ownership check on Update

In `FilesController.Update`, verify the existing record belongs to the current user before allowing updates:

```csharp
var userId = User.FindFirstValue("sub")!;
var existing = await _filesService.GetAsync(id, userId, ct);
if (existing is null) return NotFound();  // returns 404 for both missing and wrong-user
```

---

## Step 5 — Add MongoDB Index on `UserId`

Add a compound index on `(UserId, UpdatedAtUtc)` in `MongoDbFileRepository` at startup for efficient per-user list queries:

```csharp
var indexKeys = Builders<File>.IndexKeys
    .Ascending(f => f.UserId)
    .Descending(f => f.UpdatedAtUtc);
await _collection.Indexes.CreateOneAsync(new CreateIndexModel<File>(indexKeys));
```

---

## Step 6 — Update Integration Tests

The `CustomWebApplicationFactory` currently has no auth. Two approaches:

### Option A — Disable auth in tests (simpler, already works)

Set `Authentication:Enabled = false` in the test factory configuration (it is already off by default). Tests will continue to work without tokens.

If you want to test auth behavior (unauthorized access, per-user isolation), use Option B below.

### Option B — Add a test JWT handler (for auth integration tests)

Add a fake authentication handler to the test factory that injects a controllable `sub` claim:

```csharp
// In CustomWebApplicationFactory.ConfigureWebHost:
builder.ConfigureTestServices(services =>
{
    services.AddAuthentication("Test")
        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
});
```

```csharp
// TestAuthHandler.cs in TestSupport/
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string UserId = "test-user-id";

    // Constructor with required parameters...

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

Then tests can assert that files created by `test-user-id` are not visible to other users.

---

## Step 7 — Obtain a Token for Manual Testing

To test locally with Swagger or curl, get an access token from Cognito:

```bash
aws cognito-idp initiate-auth \
  --auth-flow USER_PASSWORD_AUTH \
  --client-id <app-client-id> \
  --auth-parameters USERNAME=testuser@example.com,PASSWORD=YourPassword123! \
  --region us-west-2
```

Use the `AccessToken` from the response as the Bearer token in Swagger or:

```bash
curl -H "Authorization: Bearer <AccessToken>" https://localhost:7xxx/files
```

---

## Implementation Order (recommended)

1. [ ] Copy CDK stack outputs (`CognitoAuthority`, `UserPoolClientId`) into `appsettings.Development.json`, set `Enabled: true`
2. [ ] Harden `AddJwtBearer` options in `Program.cs`
3. [ ] Add `[Authorize]` to `FilesController`
4. [ ] Update `IFileRepository` / `IFilesService` signatures to include `userId`
5. [ ] Update `MongoDbFileRepository` and `InMemoryFileRepository` with `userId` filtering
6. [ ] Update `FilesService` to pass `userId` through
7. [ ] Update `FilesController` to extract and pass `sub` claim
8. [ ] Add MongoDB index on `(UserId, UpdatedAtUtc)`
9. [ ] Update `CustomWebApplicationFactory` and tests (Option A or B)
10. [ ] Manual smoke test with a real token from Cognito

---

## Files to Modify

| File | Change |
|------|--------|
| `appsettings.Development.json` | Fill in Cognito values, set `Enabled: true` |
| `Program.cs` | Harden `TokenValidationParameters` |
| `FilesController.cs` | Add `[Authorize]`, extract `sub` claim, pass to service |
| `IFilesService.cs` | Add `userId` params to relevant methods |
| `FilesService.cs` | Pass `userId` through to repository |
| `Ports.cs` (IFileRepository) | Add `userId` params to relevant methods |
| `MongoDbFileRepository.cs` | Add `UserId` filter to queries, add index |
| `InMemoryFileRepository.cs` | Add `userId` filter to queries |
| `CustomWebApplicationFactory.cs` | Add test auth handler (if Option B) |

---

## Security Considerations

- **Never trust client-supplied `UserId`** in the request body — always derive it from the validated JWT `sub` claim.
- Return `404 Not Found` (not `403 Forbidden`) when a user tries to access another user's resource, to avoid leaking resource existence.
- The `StorageKey` format `{UserId}/{FileId}` naturally partitions S3 objects by user, which also allows IAM prefix-based policies if needed later.
- Ensure HTTPS is enforced in production (`RequireHttpsMetadata = true` is already set).
