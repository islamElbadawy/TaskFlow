# TaskFlow database guide (PostgreSQL)

TaskFlow stores its data in **PostgreSQL 17**, running locally in Docker. The schema is managed by
**EF Core migrations**, and a **seeder** fills it with sample accounts, tasks, comments and notifications
so you can try every feature straight away.

| | |
|---|---|
| Engine | PostgreSQL 17 (`postgres:17` image), container `taskflow-postgres` |
| Host / port | `localhost:5433` (**not** 5432: another PostgreSQL already uses that port on this machine) |
| Database / user | `taskflow` / `taskflow` |
| Password | Your choice, kept in `.env` (Docker) and user-secrets (API). Never committed. |
| EF Core provider | `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 with retry on transient failures |
| Naming | snake_case tables and columns (`tasks.assigned_to_user_id`) via `EFCore.NamingConventions` |
| Migrations | `src/TaskFlow.Infrastructure/Data/Migrations`, history table `__ef_migrations_history` |
| Seed data | `src/TaskFlow.Infrastructure/Data/Seeding/DataSeeder.cs` |

---

## 1. One-time setup

### 1.1 Install Docker Desktop
Install [Docker Desktop for Windows](https://www.docker.com/products/docker-desktop/) (WSL 2 backend) and start it.
Check it works with `docker version`.

### 1.2 Choose the database password
From the repository root:

```powershell
Copy-Item .env.example .env
notepad .env          # set POSTGRES_PASSWORD to something of your own
```

`.env` is in `.gitignore`. Docker Compose reads it automatically.

### 1.3 Give the API the connection string and JWT secret (user-secrets)
`appsettings.json` only has a connection string **without** a password. Put the full one, with the same
password as in `.env`, in user-secrets (stored in your Windows profile, outside the repository):

```powershell
cd src\TaskFlow.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5433;Database=taskflow;Username=taskflow;Password=<your password>"

# JWT signing key, if not set already (any random value of 32+ bytes):
$bytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
dotnet user-secrets set "JwtSettings:Secret" ([Convert]::ToBase64String($bytes))

dotnet user-secrets list      # shows both keys
```

### 1.4 Start PostgreSQL

```powershell
docker compose up -d
docker compose ps             # wait until STATUS shows "(healthy)"
```

The data lives in the Docker volume `taskflow_taskflow-pgdata`, so it survives `docker compose down` and restarts.

---

## 2. Run the API

```powershell
cd src\TaskFlow.API
dotnet run --launch-profile https
```

In the **Development** environment, startup:
1. applies any pending migrations (`Database.MigrateAsync()`), creating the database on first run;
2. runs the seeder, which inserts every seed record that is **missing** (matched by its fixed id).
   It's safe on every start: existing rows, including ones you edited, are left alone, and rows you
   deleted come back on the next start. Use the reset (section 6) for a completely clean state.

Swagger UI: <https://localhost:7103/swagger> (or <http://localhost:5078/swagger>).

Outside Development nothing is migrated or seeded automatically. Apply migrations explicitly instead:

```powershell
$env:TASKFLOW_DB_CONNECTION = "Host=...;Port=...;Database=taskflow;Username=...;Password=..."
dotnet ef database update --project src/TaskFlow.Infrastructure --startup-project src/TaskFlow.API
```

The `dotnet ef` commands use `DesignTimeDbContextFactory`, which reads `TASKFLOW_DB_CONNECTION`
(default: the password-less local connection string). Creating migrations or SQL scripts doesn't need a
database or the JWT secret; `database update` does connect, so it needs the password. You can also pass
`--connection "<connection string>"`.

---

## 3. Seed data reference

Every account's password is **`P@ssw0rd`**. Dates are relative to the moment the seeder first ran
("-10d" = ten days before, "+3d" = three days after).

### 3.1 Accounts

| Id | Email | Name | Role | Active | What it's for |
|---|---|---|---|---|---|
| `11111111-1111-1111-1111-111111111111` | admin@taskflow.dev | Admin User | Admin | yes | Everything: change roles, edit or delete any user |
| `22222222-2222-2222-2222-222222222222` | manager@taskflow.dev | Manager User | Manager | yes | Creates and assigns most tasks; can manage only their own account |
| `33333333-3333-3333-3333-333333333333` | user@taskflow.dev | Regular User | User | yes | Main "team member": most assigned tasks and notifications |
| `44444444-4444-4444-4444-444444444444` | user2@taskflow.dev | Sam Taylor | User | yes | Second team member; created no tasks, so an admin can delete it |
| `55555555-5555-5555-5555-555555555555` | inactive@taskflow.dev | Inactive User | User | **no** | Deactivated account: **login is rejected** with the generic "Invalid email or password" |

What each role can do with the current endpoints:

| Action | Endpoint | User / Manager | Admin |
|---|---|---|---|
| Register, log in | `POST /api/auth/register`, `POST /api/auth/login` | anyone (no token) | anyone |
| Change own password | `POST /api/auth/change-password` | own account | own account |
| List users, get a user, get me | `GET /api/users`, `GET /api/users/{id}`, `GET /api/users/me` | yes | yes |
| Update a user's name/email | `PUT /api/users/{id}` | **own account only** (others → 403) | any account |
| Change a role | `PUT /api/users/{id}/role` | 403 | yes (the last admin can't be demoted) |
| Delete a user | `DELETE /api/users/{id}` | own account only | any account (fails if the user created tasks) |
| Create a task | `POST /api/tasks` | yes | yes |

### 3.2 Tasks

| Id | Title | Status | Priority | Due | Created by → assigned to | Why it's there |
|---|---|---|---|---|---|---|
| `aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa` | Setup project | Done | High | -35d | Admin → Manager | Finished task with comments |
| `bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb` | Write docs | InProgress | Medium | +3d | Manager → Regular | **Due soon**; most comments and notifications point here |
| `a0000000-0000-0000-0000-000000000003` | Fix password reset email | Todo | High | -2d | Manager → Regular | **Overdue** (past due, not done) |
| `a0000000-0000-0000-0000-000000000004` | Design notifications panel | Todo | Low | none | Manager → Sam | **No due date** |
| `a0000000-0000-0000-0000-000000000005` | Set up CI pipeline | Todo | Medium | +14d | Admin → nobody | **Unassigned** |
| `a0000000-0000-0000-0000-000000000006` | Review sprint backlog | InProgress | Medium | +1d | Regular → Manager | Created by a plain User; due tomorrow |
| `a0000000-0000-0000-0000-000000000007` | Migrate database to PostgreSQL | InProgress | High | +7d | Admin → Admin | Self-assigned |
| `a0000000-0000-0000-0000-000000000008` | Archive old reports | Done | Low | -30d | Manager → Inactive | Assigned to the deactivated account |

Together they cover every status (Todo / InProgress / Done) and every priority (Low / Medium / High).

### 3.3 Comments

| Id | Task | Author | Text | When |
|---|---|---|---|---|
| `c0000000-0000-0000-0000-000000000001` | Setup project | Manager | Repository and solution structure are in place. | -36d |
| `c0000000-0000-0000-0000-000000000002` | Setup project | Admin | Thanks, looks good. Closing this. | -35d |
| `c0000000-0000-0000-0000-000000000003` | Write docs | Regular | Started on the README; the API section is next. | -5d |
| `c0000000-0000-0000-0000-000000000004` | Write docs | Manager | Please include the seed accounts table. | -4d |
| `c0000000-0000-0000-0000-000000000005` | Fix password reset email | Regular | Reproduced it locally; looking at the SMTP settings. | -1d |
| `c0000000-0000-0000-0000-000000000006` | Review sprint backlog | Manager | I'll go through it before Thursday's planning. | -1d |

### 3.4 Notifications

Every notification type, read and unread. `related_task_id` links to the task.

| Id | For | Type | Message | Read | When |
|---|---|---|---|---|---|
| `e0000000-0000-0000-0000-000000000001` | Regular | TaskAssigned | Manager User assigned you "Fix password reset email" | no | -10d |
| `e0000000-0000-0000-0000-000000000002` | Manager | CommentAdded | Regular User commented on "Write docs" | yes | -5d |
| `e0000000-0000-0000-0000-000000000003` | Admin | TaskCompleted | "Setup project" was marked as done | yes | -35d |
| `e0000000-0000-0000-0000-000000000004` | Regular | DeadlineApproaching | "Write docs" is due in 3 days | no | -1h |
| `e0000000-0000-0000-0000-000000000005` | Sam | TaskAssigned | Manager User assigned you "Design notifications panel" | no | -7d |
| `e0000000-0000-0000-0000-000000000006` | Regular | TaskUpdated | Manager User changed the due date of "Write docs" | yes | -3d |

> There are no task-list, comment or notification endpoints yet (only `POST /api/tasks`), so for now
> inspect tasks, comments and notifications with psql (section 5). They are planned in a follow-up branch.

---

## 4. Try it

The examples use PowerShell's `Invoke-RestMethod` against the HTTP profile (`http://localhost:5078`).
In Swagger, call `POST /api/auth/login`, copy `data.token`, click **Authorize** and paste the token.

### 4.1 Log in as each account

```powershell
$api = "http://localhost:5078"
function Login($email) {
    $body = @{ email = $email; password = "P@ssw0rd" } | ConvertTo-Json
    (Invoke-RestMethod "$api/api/auth/login" -Method Post -ContentType "application/json" -Body $body).data
}

$admin   = Login "admin@taskflow.dev"
$manager = Login "manager@taskflow.dev"
$user    = Login "user@taskflow.dev"
$admin.user          # id, name, role, createdAt
```

Every response is wrapped in `Result<T>`: `{ "isSuccess": true, "data": { ... } }`, or on failure
`{ "isSuccess": false, "errorMessage": "...", "errors": [ ... ] }` with HTTP 400.

### 4.2 The inactive account can't log in

```powershell
Login "inactive@taskflow.dev"
# -> 400 Bad Request, errorMessage "Invalid email or password"
```

The message is the same as for a wrong password or an unknown email, so the API doesn't reveal that the
account exists. Reactivate it in psql (`UPDATE users SET is_active = true WHERE email = 'inactive@taskflow.dev';`)
and the same login succeeds.

### 4.3 403 vs 200: who may edit whom

```powershell
function Auth($session) { @{ Authorization = "Bearer $($session.token)" } }
$adminId = "11111111-1111-1111-1111-111111111111"
$userId  = "33333333-3333-3333-3333-333333333333"
$body = @{ firstName = "Regular"; lastName = "User (edited)"; email = "user@taskflow.dev" } | ConvertTo-Json

# A User editing the Admin -> 403 Forbidden
Invoke-RestMethod "$api/api/users/$adminId" -Method Put -Headers (Auth $user) -ContentType "application/json" -Body $body

# The Admin editing the User -> 200 OK
Invoke-RestMethod "$api/api/users/$userId" -Method Put -Headers (Auth $admin) -ContentType "application/json" -Body $body

# Only an Admin may change roles (Manager -> 403, Admin -> 200)
$role = @{ newRole = "Manager" } | ConvertTo-Json
Invoke-RestMethod "$api/api/users/44444444-4444-4444-4444-444444444444/role" -Method Put -Headers (Auth $manager) -ContentType "application/json" -Body $role
Invoke-RestMethod "$api/api/users/44444444-4444-4444-4444-444444444444/role" -Method Put -Headers (Auth $admin) -ContentType "application/json" -Body $role
```

Deleting: the Admin can delete Sam (`4444…`) or the inactive account (`5555…`), but deleting Regular
(`3333…`) fails with "Cannot delete user with created tasks" because they created "Review sprint backlog".

### 4.4 Create a task

```powershell
$task = @{ title = "Try the API"; description = "Created from PowerShell"; priority = "High";
           dueDate = (Get-Date).ToUniversalTime().AddDays(2).ToString("o");
           assignedToId = "44444444-4444-4444-4444-444444444444" } | ConvertTo-Json
(Invoke-RestMethod "$api/api/tasks" -Method Post -Headers (Auth $manager) -ContentType "application/json" -Body $task).data
```

The creator is always the logged-in user (taken from the token); a `createdById` in the body is ignored.

### 4.5 bash / curl equivalent

```bash
TOKEN=$(curl -s http://localhost:5078/api/auth/login -H "Content-Type: application/json" \
  -d '{"email":"admin@taskflow.dev","password":"P@ssw0rd"}' | jq -r .data.token)
curl -s http://localhost:5078/api/users -H "Authorization: Bearer $TOKEN" | jq
```

---

## 5. Look at the data with psql

Use the psql inside the container, so you can't accidentally connect to the other PostgreSQL on port 5432:

```powershell
docker exec -it taskflow-postgres psql -U taskflow -d taskflow
```

(With a locally installed psql, always pass `-h localhost -p 5433 -U taskflow -d taskflow`.)

```sql
\dt                                                   -- tables: users, tasks, comments, notifications, __ef_migrations_history
\d tasks                                              -- columns, indexes, foreign keys
SELECT migration_id, product_version FROM __ef_migrations_history;

-- Row counts (fresh seed: 5 / 8 / 6 / 6)
SELECT (SELECT count(*) FROM users) AS users, (SELECT count(*) FROM tasks) AS tasks,
       (SELECT count(*) FROM comments) AS comments, (SELECT count(*) FROM notifications) AS notifications;

-- Accounts
SELECT email, role, is_active, created_at FROM users ORDER BY created_at;

-- Tasks with creator and assignee
SELECT t.title, t.status, t.priority, t.due_date::date,
       c.email AS created_by, a.email AS assigned_to
FROM tasks t
JOIN users c ON c.id = t.created_by_user_id
LEFT JOIN users a ON a.id = t.assigned_to_user_id
ORDER BY t.due_date NULLS LAST;

-- Overdue tasks
SELECT title, due_date FROM tasks WHERE due_date < now() AND status <> 'Done';

-- Comments on a task
SELECT u.email, c.content, c.created_at
FROM comments c JOIN users u ON u.id = c.user_id
WHERE c.task_id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb' ORDER BY c.created_at;

-- Unread notifications for Regular User (uses index ix_notifications_user_id_is_read)
SELECT type, message, created_at FROM notifications
WHERE user_id = '33333333-3333-3333-3333-333333333333' AND NOT is_read ORDER BY created_at DESC;
```

Notes: all timestamps are `timestamp with time zone` stored in UTC; `updated_at` is `NULL` until a row
is first modified; enums are stored as text (`'InProgress'`, `'High'`, `'TaskAssigned'`); new ids are
time-ordered version 7 GUIDs.

---

## 6. Reset

| Goal | Command |
|---|---|
| Drop the database, re-apply migrations, reseed (Development only) | `dotnet run -- --Seed:ResetDatabase=true` (from `src/TaskFlow.API`) |
| Same, then run a quick notification smoke test and exit (exit code 0 = passed) | `dotnet run -- --Seed:ResetDatabase=true --Seed:RunSmokeTestAndExit=true` |
| Stop PostgreSQL, keep the data | `docker compose down` |
| Delete everything, including the volume | `docker compose down -v` (next `up` + API start recreates and reseeds) |

---

## 7. Changing the schema (new migrations)

1. Change the entity in `TaskFlow.Domain` and/or its `IEntityTypeConfiguration` in `Data/Configurations`.
2. Add a migration from the repository root:
   ```powershell
   dotnet ef migrations add <Name> --project src/TaskFlow.Infrastructure --startup-project src/TaskFlow.API --output-dir Data/Migrations
   ```
3. Review the generated code and the SQL:
   ```powershell
   dotnet ef migrations script --idempotent --project src/TaskFlow.Infrastructure --startup-project src/TaskFlow.API
   ```
4. Run the API (Development applies it) or `dotnet ef database update`.
5. Undo the last migration that hasn't been applied: `dotnet ef migrations remove ...` (same `--project` options).

Keep the tools in step with the runtime: `dotnet tool update -g dotnet-ef --version 10.0.4`.

---

## 8. Troubleshooting

| Symptom | Fix |
|---|---|
| `Failed to connect to 127.0.0.1:5433` | PostgreSQL isn't running: `docker compose up -d`, then `docker compose ps` until healthy. |
| `password authentication failed for user "taskflow"` | The user-secrets password doesn't match `.env`. The password is set only when the volume is **first** created, so changing `.env` later has no effect: either use the original password, or `docker compose down -v` and start again. |
| `required variable POSTGRES_PASSWORD is missing` | `.env` is missing or empty: `Copy-Item .env.example .env` and set it. |
| `port is already allocated` on 5433 | Something else uses 5433. Change the left side of `"5433:5432"` in `docker-compose.yml` and the `Port=` in your connection string. Don't use 5432. |
| `ConnectionStrings:DefaultConnection is not configured` / `JwtSettings:Secret is missing` | Set the user-secrets (section 1.3); they're read only in the Development environment. |
| `Cannot write DateTime with Kind=Local ...` | Shouldn't happen (a converter normalizes every `DateTime` to UTC); if you add raw SQL, pass UTC values. |
| A seed record you deleted came back | Expected: the seeder restores missing seed records on every Development start. |
| `The Entity Framework tools version '10.0.2' is older than that of the runtime` | Harmless warning; update with the command in section 7. |
