# TaskFlow Bug Fixes

Branch: `claude/changes` · 12 files changed · Build: passing

This document explains each bug that was fixed: what was wrong, why it mattered, what changed,
and how it was verified against the running API.

| # | Bug | Severity | Verified |
|---|---|---|---|
| 1 | JWT tokens lasted ~4 years instead of 24 hours | High (security) | Yes |
| 2 | Any user could update or delete any other user (IDOR) | Critical (security) | Yes |
| 3 | Email uniqueness check in UpdateUser was inverted | High (data integrity) | Yes |
| 4 | Duplicate registrations possible with different email casing | Medium (data integrity) | Yes |
| 5 | Updating a user always failed with "UserId is required" | High (broken feature) | Yes |
| 6 | `UpdateTask` crashed with `NullReferenceException` | High (crash) | Build only* |
| 7 | Startup always wiped the database and exited | Critical (data loss) | Yes |
| 8 | JWT signing secret committed to git | Critical (security) | Yes |

\* `TasksController` has no update endpoint yet, so the handler can't be reached over HTTP.

---

## 1. JWT tokens lasted ~4 years

**Files:** `TaskFlow.Infrastructure/Services/JwtService.cs`, `TaskFlow.Application/Common/Interfaces/IJwtService.cs`,
`TaskFlow.Application/Commands/Users/LoginCommand/LoginCommandHandler.cs`

### What was wrong
The setting is called `ExpirationInMinutes` (value `1440` = 24 hours), but the code passed it to `AddDays`:

```csharp
Expires = DateTime.UtcNow.AddDays(_jwtSettings.ExpirationInMinutes), // 1440 days ≈ 3.9 years
```

Meanwhile `LoginCommandHandler` calculated its own, separate expiry and told the client the token
expired in 24 hours:

```csharp
var expiresAt = DateTime.UtcNow.AddHours(24); // unrelated to the real token expiry
```

### Why it matters
A stolen token stays valid for almost four years, and the client believes something false about
when it needs to refresh. Two places computing "the same" value independently will always drift apart.

### The fix
- `AddDays` → `AddMinutes`.
- `GenerateToken` now returns the token **and** its expiry, so there is one source of truth:

```csharp
// IJwtService
(string Token, DateTime ExpiresAt) GenerateToken(User user);

// JwtService
var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationInMinutes);
// ... Expires = expiresAt ...
return (tokenHandler.WriteToken(token), expiresAt);

// LoginCommandHandler
var (token, expiresAt) = _jwtService.GenerateToken(user);
```

- The signing key is now read with `Encoding.UTF8` instead of `Encoding.ASCII` (ASCII silently
  replaces non-ASCII characters with `?`, weakening the key).

### Verified
Decoded the issued token: `exp − nbf = 86400` seconds = exactly 24 hours.

---

## 2. Any user could update or delete any other user (IDOR)

**File:** `TaskFlow.API/Controllers/UsersController.cs`

### What was wrong
`UpdateUser` and `DeleteUser` only required the caller to be logged in. The target user id came
from the URL and was never compared with the caller's identity, so a regular user could rename,
change the email of, or delete the admin account. This class of bug is called an
**Insecure Direct Object Reference (IDOR)**.

### The fix
A single authorization rule, used by both actions: you may manage your own account; admins may manage any account.

```csharp
private bool CanManageUser(Guid targetUserId) =>
    _currentUserService.UserId == targetUserId || User.IsInRole("Admin");
```

```csharp
if (!CanManageUser(id))
    return Forbid(); // 403
```

The update endpoint also accepts `PUT` now, which is the correct HTTP verb for an update.
`POST` is still accepted so existing clients don't break; remove it once clients have moved.

### Verified
| Request | Result |
|---|---|
| Regular user updates admin | 403 |
| Regular user deletes admin | 403 |
| Admin updates regular user | 200 |

---

## 3. Email uniqueness check in UpdateUser was inverted

**File:** `TaskFlow.Application/Commands/Users/UpdateUserCommand/UpdateUserCommandHandler.cs`

### What was wrong
```csharp
u.Email == command.Email.ToLower() && u.Id == command.UserId
```
This asks "does **this same user** already have this email?", which is the opposite of the intent:
- Re-saving your own unchanged email → rejected as "Email already exists".
- Taking an email that belongs to **another** user → allowed (only the database's unique index
  would stop it, with a 500 error instead of a clean message).

The new email was also saved exactly as typed (not lower-cased), unlike Register.

### The fix
```csharp
var email = command.Email.Trim().ToLowerInvariant();

var existingUser = await _unitOfWork.Users.FirstOrDefaultAsync(u =>
    u.Email == email && u.Id != command.UserId); // another user owns it?

if (existingUser != null)
    return Result<UserDto>.Failure("Email already exists");

user.Email = email;
```

### Verified
| Request | Result |
|---|---|
| User re-saves own email as `User@TaskFlow.dev` | 200, stored as `user@taskflow.dev` |
| User tries to take `manager@taskflow.dev` | 400 "Email already exists" |

---

## 4. Duplicate registrations with different email casing

**Files:** `RegisterUserCommandHandler.cs`, `LoginCommandHandler.cs`

### What was wrong
Register compared the **raw** input against stored emails, but stored the **lower-cased** version:

```csharp
.FirstOrDefaultAsync(u => u.Email == command.Email);   // raw "ADMIN@taskflow.dev"
...
Email = command.Email.ToLower(),                        // stored lower-case
```

Whether `ADMIN@…` matched `admin@…` depended on the database collation. Leading/trailing spaces
were never trimmed anywhere, so `" user@taskflow.dev"` could not log in.

### The fix
Normalize once at the start of each handler, and use the same value for comparing and storing:

```csharp
var email = command.Email.Trim().ToLowerInvariant();
```

`ToLowerInvariant()` is used instead of `ToLower()` so the result doesn't depend on the server's
culture (e.g. the Turkish "I" problem).

### Verified
- Registering `ADMIN@taskflow.dev` → 400 "Email already registered".
- Logging in with `" USER@taskflow.dev"` → succeeds.

---

## 5. Updating a user always failed with "UserId is required"

**Files:** `TaskFlow.Application/Validators/UpdateUserCommandValidator.cs`, `UpdateUserRoleCommandValidator.cs`

*Found while testing fix #2.*

### What was wrong
`AddFluentValidationAutoValidation()` validates the request body **before** the controller action
runs. The validators required `UserId`, but `UserId` is never in the body — the controller sets it
from the URL afterwards:

```csharp
command.UserId = id; // runs after validation already failed
```

So `PUT/POST /api/users/{id}` and `PUT /api/users/{id}/role` returned 400 unless the client also
duplicated the id in the JSON body.

### The fix
Removed the `UserId` rule from both validators, with a comment explaining why. The id always comes
from the route; an unknown id is handled by the handler as "User not found".

### Verified
Updates now return 200 (see the tables in #2 and #3).

---

## 6. `UpdateTask` crashed with `NullReferenceException`

**File:** `TaskFlow.Application/Commands/Tasks/UpdateTaskCommand/UpdateTaskCommandHandler.cs`

### What was wrong
The task is loaded through the generic repository, which doesn't `Include` navigation properties.
`task.CreatedBy` is therefore `null`, and building the DTO dereferenced it:

```csharp
CreatedByName: $"{task.CreatedBy.FirstName} {task.CreatedBy.LastName}", // NRE
```

`AssignedTo` had the same problem (it didn't crash only because it was null-checked, but the
assignee's name was always reported as `null`).

### The fix
Load the related users explicitly by their foreign keys when the navigation isn't loaded:

```csharp
var createdBy = task.CreatedBy ?? await _unitOfWork.Users.GetByIdAsync(task.CreatedByUserId);
var assignedTo = task.AssignedToUserId is { } assignedId
    ? task.AssignedTo ?? await _unitOfWork.Users.GetByIdAsync(assignedId)
    : null;
```

> Longer term, replace the generic repository for tasks with a query that projects straight to
> `TaskDto` (see the design notes at the end).

### Verified
Compiles. Not testable over HTTP yet because `TasksController` has no update endpoint.

---

## 7. Startup always wiped the database and exited

**File:** `TaskFlow.API/Program.cs`

### What was wrong
`DataSeeder` is always registered, so this block ran on **every** start, in **every** environment:

1. `EnsureDeleted()` — drop the whole database.
2. `EnsureCreated()` + seed.
3. Run a notification smoke test.
4. `Environment.Exit(0)` — so `app.Run()` was never reached and the API never served requests.

### The fix
The destructive steps are now opt-in and Development-only:

| How you start the app | What happens |
|---|---|
| `dotnet run` (Development) | Creates the database if missing and seeds it once (the seeder skips when users exist). Nothing is deleted. |
| `dotnet run -- --Seed:ResetDatabase=true` | Drops, recreates and reseeds the database, then runs normally. |
| `dotnet run -- --Seed:ResetDatabase=true --Seed:RunSmokeTestAndExit=true` | As above, then runs your notification smoke test and exits (the original behavior). |
| Any non-Development environment | None of the above runs. |

```csharp
var resetDatabase = app.Environment.IsDevelopment()
    && app.Configuration.GetValue<bool>("Seed:ResetDatabase");
```

Your smoke-test code is kept unchanged, just wrapped in the `Seed:RunSmokeTestAndExit` check.

### Verified
The API starts, stays running, and serves requests.

---

## 8. JWT signing secret committed to git

**Files:** `TaskFlow.API/appsettings.json`, `TaskFlow.API/TaskFlow.API.csproj`, `TaskFlow.API/Program.cs`

### What was wrong
`appsettings.json` is tracked by git and contained the signing key:

```json
"Secret": "MyLovelySecretKeyToSecureUserToken!"
```

Anyone with access to the repository can forge a valid token for any user, including an admin.

### The fix
- `Secret` in `appsettings.json` is now empty.
- The API project has a `UserSecretsId`, and a new random 48-byte secret was stored in user-secrets
  on this machine (outside the repository, under `%APPDATA%\Microsoft\UserSecrets`).
- Startup fails fast with a clear message if the secret is missing or shorter than 32 bytes
  (HMAC-SHA256 needs at least 256 bits):

```csharp
var key = Encoding.UTF8.GetBytes(jwtSettings.Get<JwtSettings>()?.Secret ?? "");
if (key.Length < 32)
    throw new InvalidOperationException(
        "JwtSettings:Secret is missing or shorter than 32 bytes. Set it with user-secrets or an environment variable.");
```

- `RequireHttpsMetadata` is now `true` outside Development.

### What you need to do
- **Treat the old secret as leaked** — it is still in git history. Existing tokens signed with it
  no longer validate, so users must log in again.
- On any other machine, set a secret before running:

```bash
dotnet user-secrets set "JwtSettings:Secret" "<random 32+ byte value>" --project src/TaskFlow.API
```

- In deployed environments use the `JwtSettings__Secret` environment variable or a secret vault.

---

## Not changed (recommended next steps)

These came up in the review but are design improvements rather than bugs:

- **CORS** — the `AllowAll` policy accepts any origin; restrict it to your front-end's origin.
- **Error types in `Result`** — every failure returns 400; "not found" should be 404 and
  "already exists" 409, ideally as ProblemDetails.
- **Migrations** — replace `EnsureCreated` with EF Core migrations before deploying.
- **Tests** — add a test project; the manual checks above are good candidates for
  `WebApplicationFactory` integration tests.
- **Task update endpoint** — add `PUT /api/tasks/{id}` so fix #6 can be used.
- **Generic repository** — replace with specific repositories or projected queries, so reads load
  only the fields they need and navigations aren't silently missing.
