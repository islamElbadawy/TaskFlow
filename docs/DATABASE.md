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
| Board order | `tasks.position integer not null default 0`: the task's place in its status column (0 = top), index `ix_tasks_status_position` on `(status, position)`. Added by migration `AddTaskPosition`, which numbers each existing column from 0 by `due_date` (none last), then `created_at` |

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
The same command also starts Mailpit, the development mail catcher (section 4.10).

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

Settings you may want to change (appsettings.json, appsettings.Development.json, user-secrets or `--Key=value`):

| Setting | Default | Development | Meaning |
|---|---|---|---|
| `Cors:AllowedOrigins` | none | `http://localhost:3000`, `:4200`, `:5173` | Browser origins that may call the API and the SignalR hub (with credentials) |
| `RateLimiting:Auth:PermitLimit` | `10` | `100` | Logins + registrations per client IP per window; more get 429 |
| `RateLimiting:Auth:WindowSeconds` | `60` | `60` | Length of that window |
| `Reminders:*`, `Smtp:*` | see section 4.10 | | Deadline reminders and email |

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
| Change a role | `PUT /api/users/{id}/role` | 403 | yes (the last active Admin can't be demoted; the user's tokens are revoked) |
| Deactivate / reactivate a user | `PATCH /api/users/{id}/active` | 403 | yes (not themselves, not the last active Admin) |
| Delete a user | `DELETE /api/users/{id}` | own account only | any account (fails if the user created tasks) |
| List tasks (filters, paging), get a task | `GET /api/tasks`, `GET /api/tasks/{id}` | yes (all tasks) | yes |
| Create a task | `POST /api/tasks` | yes | yes |
| Edit a task / (re)assign it | `PUT /api/tasks/{id}`, `PATCH /api/tasks/{id}/assignee` | User: own tasks only (creator); Manager: any task | any task |
| Change a task's status, move it on the board (also within its column) | `PATCH /api/tasks/{id}/status`, `PATCH /api/tasks/{id}/move` | User: tasks they created **or are assigned to**; Manager: any task | any task |
| Delete a task | `DELETE /api/tasks/{id}` | own tasks only (creator); **a Manager can't delete others' tasks** | any task |
| Read and add comments | `GET`/`POST /api/tasks/{taskId}/comments` | yes (any task) | yes |
| Edit a comment | `PUT /api/comments/{id}` | own comments only | own comments only (an Admin can't edit others' words) |
| Delete a comment | `DELETE /api/comments/{id}` | own comments only | any comment (moderation) |
| Notifications: list, unread count, mark read, read all | `/api/notifications…` | own notifications only | own notifications only |
| Dashboard | `GET /api/dashboard` | all-task counts + own personal section | same |

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

Board order (`position`) of the seeded tasks, numbered like the `AddTaskPosition` backfill (due date, none last, then
creation time): **Todo** Fix password reset email (0), Set up CI pipeline (1), Design notifications panel (2);
**InProgress** Review sprint backlog (0), Write docs (1), Migrate database to PostgreSQL (2); **Done** Setup
project (0), Archive old reports (1).

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

> Everything above can be read through the API: tasks (4.5), comments (4.7) and each user's own
> notifications (4.8). New notifications are created automatically by task and comment changes, see
> [REALTIME.md](REALTIME.md).

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

A successful response is `Result<T>`: `{ "isSuccess": true, "data": { ... } }` with HTTP 200. A failure is an RFC 9457
ProblemDetails (`application/problem+json`) with the status code of the error, `title` = error type and
`detail` = message, e.g. `{ "title": "NotFound", "status": 404, "detail": "Task not found", ... }`:
400 `Validation` (validator and binding failures also list the fields in `errors`), 401 `Unauthorized` (no/invalid token,
the token of a deactivated or deleted user, failed login), 403 `Forbidden`, 404 `NotFound`, 409 `Conflict` (duplicate email, deleting a user who created
tasks, demoting or deleting the last active Admin, deactivating yourself or the last active Admin), 500 `Failure` (unexpected, logged). In PowerShell, `Invoke-RestMethod` throws
for these; the body is in `$_.ErrorDetails.Message`.

### 4.2 The inactive account can't log in (activation)

```powershell
Login "inactive@taskflow.dev"
# -> 401 Unauthorized, ProblemDetails detail "Invalid email or password"
```

The message is the same as for a wrong password or an unknown email, so the API doesn't reveal that the
account exists. An Admin can reactivate it, and deactivate it again:

```powershell
function Auth($session) { @{ Authorization = "Bearer $($session.token)" } }   # also used from 4.3 on
$inactiveId = "55555555-5555-5555-5555-555555555555"
$on  = @{ isActive = $true }  | ConvertTo-Json
$off = @{ isActive = $false } | ConvertTo-Json
Invoke-RestMethod "$api/api/users/$inactiveId/active" -Method Patch -Headers (Auth $admin) -ContentType "application/json" -Body $on
$s = Login "inactive@taskflow.dev"                                    # works now
Invoke-RestMethod "$api/api/users/$inactiveId/active" -Method Patch -Headers (Auth $admin) -ContentType "application/json" -Body $off
Invoke-RestMethod "$api/api/users/me" -Headers (Auth $s)             # -> 401: the token it got a moment ago is rejected
(Invoke-RestMethod "$api/api/users?isActive=false" -Headers (Auth $admin)).data | Format-Table email, isActive
```

Deactivation takes effect immediately: besides the login check, every authenticated request (SignalR connections
included) looks up `users.is_active` for the token's user by primary key and answers 401 if the account is inactive
or deleted. An already open SignalR connection stays open until it reconnects. Role changes are *not* re-checked
per request: the role comes from the token until the user logs in again. An Admin can't deactivate their own
account, and the last active Admin can't be deactivated (409).

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

### 4.5 Read tasks

`GET /api/tasks` returns a page of tasks, newest first, without descriptions. All query parameters are
optional and combine with AND:

| Parameter | Example | Meaning |
|---|---|---|
| `status` | `InProgress` | `Todo`, `InProgress` or `Done` (case-insensitive) |
| `priority` | `high` | `Low`, `Medium` or `High` (case-insensitive) |
| `assignedToId` | `33333333-…` | Tasks assigned to that user |
| `unassigned` | `true` | Tasks assigned to nobody (not together with `assignedToId`) |
| `createdById` | `11111111-…` | Tasks created by that user |
| `overdue` | `true` | Past the due date and not `Done` |
| `sort` | `position` | `createdAt` (default): newest first. `position`: board order, by status (`Todo`, `InProgress`, `Done`), then `position` in the column (case-insensitive) |
| `page`, `pageSize` | `2`, `10` | Paging: page from 1, size 1–100 (default 20) |

An unknown status/priority/sort or an out-of-range page size returns 400 with the validation errors.
`GET /api/tasks/{id}` returns one task with its description and comment count, or 404.

```powershell
$h = Auth $user
(Invoke-RestMethod "$api/api/tasks?overdue=true" -Headers $h).data.items | Select-Object title, dueDate, assignedToName
(Invoke-RestMethod "$api/api/tasks?assignedToId=$userId&status=InProgress" -Headers $h).data
(Invoke-RestMethod "$api/api/tasks/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb" -Headers $h).data
```

With the seed data: `overdue=true` returns "Fix password reset email", `unassigned=true` returns
"Set up CI pipeline", and `status=InProgress` returns three tasks.

The list response looks like this:

```json
{ "isSuccess": true,
  "data": { "items": [ { "id": "…", "title": "…", "status": "Todo", "priority": "High", "dueDate": "…",
                         "isOverdue": true, "createdByName": "…", "assignedToName": "…", "commentsCount": 1, … } ],
            "pageNumber": 1, "pageSize": 20, "totalCount": 8, "totalPages": 1,
            "hasPreviousPage": false, "hasNextPage": false } }
```

### 4.6 Change, assign and delete a task

Using the request body `$task` from 4.4 (the Manager creates the task, assigned to Sam):

```powershell
$id  = (Invoke-RestMethod "$api/api/tasks" -Method Post -Headers (Auth $manager) -ContentType "application/json" -Body $task).data.id
$sam = Login "user2@taskflow.dev"

# The assignee may change the status (but not edit or reassign the task: 403)
Invoke-RestMethod "$api/api/tasks/$id/status" -Method Patch -Headers (Auth $sam) -ContentType "application/json" `
    -Body '{ "status": "InProgress" }'

# Creator, Manager or Admin: full update (every field is replaced, so send the values you want to keep)
$update = @{ title = "Try the API (edited)"; description = ""; priority = "Low"; status = "InProgress";
             dueDate = $null; assignedToId = "44444444-4444-4444-4444-444444444444" } | ConvertTo-Json
Invoke-RestMethod "$api/api/tasks/$id" -Method Put -Headers (Auth $manager) -ContentType "application/json" -Body $update

# Reassign, or unassign with null. The inactive account can't be assigned (400 "Assigned user is inactive").
Invoke-RestMethod "$api/api/tasks/$id/assignee" -Method Patch -Headers (Auth $manager) -ContentType "application/json" `
    -Body '{ "assignedToId": null }'

# Board: put the task at the top of Done (directly above the seeded "Setup project"), then at the end of Todo
Invoke-RestMethod "$api/api/tasks/$id/move" -Method Patch -Headers (Auth $manager) -ContentType "application/json" `
    -Body '{ "status": "Done", "beforeTaskId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" }'
Invoke-RestMethod "$api/api/tasks/$id/move" -Method Patch -Headers (Auth $manager) -ContentType "application/json" `
    -Body '{ "status": "Todo", "beforeTaskId": null }'
(Invoke-RestMethod "$api/api/tasks?sort=position&status=Todo" -Headers (Auth $manager)).data.items | Select-Object position, title

# Delete: the creator or an Admin (a Manager who didn't create the task gets 403)
Invoke-RestMethod "$api/api/tasks/$id" -Method Delete -Headers (Auth $manager)
```

PUT and the PATCH requests return the updated task (same shape as `GET /api/tasks/{id}`). Errors: 404 unknown
task, 403 not allowed (see the table in 3.1), 400 unknown status/priority or an unknown/inactive new assignee.

Board order: a new task goes to the end of Todo, and a status change through `PATCH .../status` or `PUT` puts the
task at the end of its new column. `PATCH .../move` places it directly above `beforeTaskId` (which must be in the
target column, else 400) or at the end when it is `null`, and renumbers the old and the new column 0..n-1 in one
transaction. A status change through it creates the same notifications as `PATCH .../status`; a reorder within a
column creates none. A change of `position` alone doesn't set `updated_at`. Concurrent moves: last write wins.
Keeping an assignee who has since been deactivated is allowed; only a *new* assignee must be active. Deleting a
task deletes its comments; its notifications stay, with `related_task_id` cleared.

### 4.7 Comments

```powershell
$writeDocs = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"

# The thread of a task, oldest first (paged: ?page=1&pageSize=20)
(Invoke-RestMethod "$api/api/tasks/$writeDocs/comments" -Headers (Auth $user)).data.items |
    Format-Table userName, content, createdAt

# Add a comment (any logged-in user; the author is taken from the token)
$c = (Invoke-RestMethod "$api/api/tasks/$writeDocs/comments" -Method Post -Headers (Auth $user) `
        -ContentType "application/json" -Body '{ "content": "Examples are in." }').data

# Edit (author only, even an Admin gets 403), then delete (author or Admin)
Invoke-RestMethod "$api/api/comments/$($c.id)" -Method Put -Headers (Auth $user) -ContentType "application/json" `
    -Body '{ "content": "Examples are in, please review." }'
Invoke-RestMethod "$api/api/comments/$($c.id)" -Method Delete -Headers (Auth $admin)
```

Content must be 1-1000 characters (surrounding spaces are trimmed); an empty one is a 400 validation error.
Unknown task or comment: 404. The seeded comments above make good test data: e.g. Sam trying to delete
`c0000000-…-0003` (written by Regular User) gets 403. A task's `commentsCount` reflects the changes.

### 4.8 Notifications

```powershell
# Regular User has 2 unread seeded notifications (an assignment and a deadline reminder)
(Invoke-RestMethod "$api/api/notifications/unread-count" -Headers (Auth $user)).data
(Invoke-RestMethod "$api/api/notifications?unread=true" -Headers (Auth $user)).data.items | Format-Table type, message, createdAt

# Changes create notifications automatically: the Manager assigns a task to Sam -> Sam gets "TaskAssigned"
$sam = Login "user2@taskflow.dev"
$body = @{ title = "Notify Sam"; description = ""; priority = "Low"; assignedToId = "44444444-4444-4444-4444-444444444444" } | ConvertTo-Json
$t = (Invoke-RestMethod "$api/api/tasks" -Method Post -Headers (Auth $manager) -ContentType "application/json" -Body $body).data
(Invoke-RestMethod "$api/api/notifications" -Headers (Auth $sam)).data.items[0]   # Manager User assigned you "Notify Sam"

# Mark one, or all, as read
$id = (Invoke-RestMethod "$api/api/notifications?unread=true" -Headers (Auth $sam)).data.items[0].id
Invoke-RestMethod "$api/api/notifications/$id/read" -Method Patch -Headers (Auth $sam)
Invoke-RestMethod "$api/api/notifications/read-all" -Method Post -Headers (Auth $sam)   # data = how many changed

Invoke-RestMethod "$api/api/tasks/$($t.id)" -Method Delete -Headers (Auth $manager)    # clean up
```

Each user only ever sees their own notifications; marking someone else's returns 404. Who is notified about what,
and how to receive them live over SignalR (with a JavaScript client example): [REALTIME.md](REALTIME.md).
Note that the PowerShell example marks Sam's seeded notification as read; reset the database (section 6) to get
the seed state back.

### 4.9 Dashboard

```powershell
(Invoke-RestMethod "$api/api/dashboard" -Headers (Auth $user)).data | ConvertTo-Json -Depth 4
```

On a freshly seeded database, Regular User gets (seed due dates are relative to when the seed ran, so the
overdue and due-soon numbers shift as days pass):

| Field | Value | Meaning |
|---|---|---|
| `tasks` | `{ total: 8, todo: 3, inProgress: 3, done: 2 }` | every task, by status (any user may list all tasks) |
| `overdueTasks` | 1 | past due and not Done: "Fix password reset email" |
| `dueSoonTasks` | 3 | due within the next 7 days and not Done: "Review sprint backlog", "Write docs", "Migrate database to PostgreSQL" |
| `myAssignedTasks` | `{ total: 2, todo: 1, inProgress: 1, done: 0 }` | assigned to the caller |
| `myOpenCreatedTasks` | 1 | created by the caller and not Done ("Review sprint backlog") |
| `unreadNotifications` | 2 | same as `GET /api/notifications/unread-count` |
| `recentTasks` | Review sprint backlog, Write docs, Fix password reset email | up to 5 tasks the caller created or is assigned to, most recently updated (or created) first, as `TaskListDto` |

The counts are computed in seven small queries (two `GROUP BY status`, four `COUNT`s, one page of 5 tasks) on indexed
columns, so the dashboard stays cheap as the tables grow.

### 4.10 Deadline reminders and email

A background job runs at start and then every 15 minutes. Every task that is due within 24 hours, not Done and
assigned to an active user gives its assignee a `DeadlineApproaching` notification, e.g.
`"Review sprint backlog" is due in 13 hours` (pushed over SignalR like the others). The assignee gets one reminder per
due date: later runs skip the task, but a new due date or a new assignee gets a new reminder. Overdue tasks get none
(the dashboard shows them). An Admin can run the same logic at once:

```powershell
$sam = Login "user2@taskflow.dev"
$due = (Get-Date).ToUniversalTime().AddHours(3).ToString("o")
$body = @{ title = "Prepare the demo"; description = ""; priority = "High"; dueDate = $due; assignedToId = "44444444-4444-4444-4444-444444444444" } | ConvertTo-Json
$t = (Invoke-RestMethod "$api/api/tasks" -Method Post -Headers (Auth $manager) -ContentType "application/json" -Body $body).data

(Invoke-RestMethod "$api/api/reminders/run" -Method Post -Headers (Auth $admin)).data    # taskIds contains $t.id
(Invoke-RestMethod "$api/api/notifications" -Headers (Auth $sam)).data.items | Select-Object -First 2 type, message
# DeadlineApproaching "Prepare the demo" is due in 2 hours
# TaskAssigned        Manager User assigned you "Prepare the demo"
(Invoke-RestMethod "$api/api/reminders/run" -Method Post -Headers (Auth $admin)).data    # the task isn't reminded twice

Invoke-RestMethod "$api/api/tasks/$($t.id)" -Method Delete -Headers (Auth $manager)    # clean up
```

| Setting | Default | Meaning |
|---|---|---|
| `Reminders:Enabled` | `true` | Run the background job (`POST /api/reminders/run` works either way) |
| `Reminders:IntervalMinutes` | `15` | Time between runs |
| `Reminders:WindowHours` | `24` | How long before the due date a task is reminded |

**Email.** `TaskAssigned` and `DeadlineApproaching` notifications are also emailed (plain text and HTML) to the
recipient if the account is active; the other types stay in the app. Email is off by default (`Smtp:Enabled` is
`false`). In development, the Mailpit container from `docker-compose.yml` catches every email on your machine:

```powershell
docker compose up -d mailpit                     # SMTP on localhost:1025, inbox at http://localhost:8025
cd src\TaskFlow.API
dotnet run --launch-profile https -- --Smtp:Enabled=true
# or keep it on: dotnet user-secrets set "Smtp:Enabled" "true"
```

Assign a task or run the reminders, then open <http://localhost:8025>. Emails are queued in memory and sent by a
background service, so a slow or unreachable SMTP server never delays or fails a request: a failure is logged
(`Sending the email "..." failed`) and the notification is still stored. Emails still in the queue are lost if the API
stops. For a real server set `Smtp:Host`, `Smtp:Port`, `Smtp:Security` (`Auto`, `StartTls`, `SslOnConnect` or `None`),
`Smtp:FromAddress` and `Smtp:FromName`, and keep `Smtp:Username`/`Smtp:Password` in user-secrets or environment
variables (`Smtp__Password`), never in appsettings.json. Invalid settings (e.g. a malformed `FromAddress`) stop the API
at start.

### 4.11 Postman collection

[`postman/TaskFlow.postman_collection.json`](../postman/TaskFlow.postman_collection.json) runs all of the
checks above as 203 requests with assertions: logins for every account, the inactive-login rejection,
register and change-password, the 400/401/403/404/409 cases (status code plus the ProblemDetails `title` and
`detail`), role changes, task creation, the task filters and paging, and the task lifecycle (update, status,
assign/unassign and delete with every permission case), comments (list, add, edit, delete) and notifications
(automatic creation, list, unread count, mark read, read all), the dashboard, user activation (including the
401 for a deactivated user's existing token), the deadline reminders and the board order (folder 11 "Task position":
reorder and cross-column moves, their notifications and errors, and `sort=position`).

1. In Postman: **Import** → select the file.
2. Check the collection variable `baseUrl` (default `https://localhost:7103`). SSL verification is turned
   off for this collection because Postman doesn't trust the ASP.NET Core development certificate.
3. Right-click the collection → **Run collection** and run it **in order**. Later requests use the tokens
   and ids saved by the login and register requests.

You can run it repeatedly. Each run registers throw-away users and deletes them, folder 5 deletes every task the
run created (titles start with `Postman:`) and folder 6 every comment it added. The last request of folder 5
fails if a `Postman:` task is still there, e.g. after an interrupted run; delete those in psql with
`DELETE FROM tasks WHERE title LIKE 'Postman:%';`.

What a run does leave behind: the automatic notifications that folders 3, 5 and 6 trigger for the seeded
accounts (Sam is assigned tasks, the Manager and Regular User get comment notifications on "Write docs";
about 7 per run). They are history like any other notification. To remove them, delete the ones that are not
seed rows, e.g. `DELETE FROM notifications WHERE id::text NOT LIKE 'e0000000-%';` (this also removes
notifications from your own manual testing), or reset the database (section 6). Folders 7 and 8 work with their own
throw-away users and leave nothing. Folder 9 activates and then deactivates `inactive@taskflow.dev` again,
so its `is_active` ends as seeded (only its `updated_at` changes). Folder 10 deletes its tasks and its throw-away user,
but its reminder run also reminds any other task due within 24 hours (on a fresh seed, "Review sprint backlog" for the
Manager), and those reminders stay. Folder 11 deletes its three tasks; moving one of them through InProgress and Done
renumbers the seeded tasks in those columns but keeps their order, and Sam's and the Manager's notifications from it
(three assignments, one completion) stay.

### 4.12 Automated integration tests

`dotnet test` (from the repository root, Docker running) runs `tests/TaskFlow.IntegrationTests`. It starts its own
throw-away `postgres:17` container with Testcontainers, applies the migrations and this seed data, and runs the API in
memory with a test JWT key. The connection string and JWT secret from your user-secrets are overridden, so the
development database on port 5433 is never touched; the container is removed afterwards. The test API records emails
instead of sending them and switches the reminder job off (the tests run the reminders themselves); one test class
sends real SMTP to a throw-away Mailpit container.

### 4.13 bash / curl equivalent

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

-- The board, column by column (uses index ix_tasks_status_position)
SELECT status, position, title FROM tasks ORDER BY status, position;

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
| No emails in Mailpit | Email is off by default: run with `--Smtp:Enabled=true`. Check that Mailpit runs (`docker compose up -d mailpit`) and look for `Sending the email ... failed` in the API log. |
| A seed record you deleted came back | Expected: the seeder restores missing seed records on every Development start. |
| `429 Too Many Requests` on login or register | The per-IP limit (`RateLimiting:Auth`) was reached: wait for the `Retry-After` seconds, or raise `PermitLimit` for local testing. |
| A browser front end gets CORS errors | Add its origin (scheme, host and port) to `Cors:AllowedOrigins`. |
| `The Entity Framework tools version '10.0.2' is older than that of the runtime` | Harmless warning; update with the command in section 7. |
