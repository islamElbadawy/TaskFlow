# TaskFlow — Project Overview

A collaborative task management backend built with **ASP.NET Core (.NET 10)**, following **Clean Architecture** and **CQRS** principles, with **PostgreSQL**, JWT authentication and SignalR.

---

## 📌 Business Overview

### What is TaskFlow?
TaskFlow is a backend system that lets teams manage work together: create tasks, assign them to teammates, track progress, discuss via comments, and stay informed through notifications and email alerts.

### Who is it for?
Small-to-medium teams (like Trello/Asana/Jira-lite) that need:
- A central place to track work
- Accountability (who created / who's assigned)
- Visibility into deadlines and priorities
- A history of discussion per task

### Core Business Capabilities

| Capability | Description | State |
|---|---|---|
| **User Accounts** | Register, log in, manage profile, change password. Roles: `User`, `Manager`, `Admin`. Admins can deactivate accounts: no login, and existing tokens stop working | ✅ |
| **Task Lifecycle** | Create → Assign → Work (`Todo` → `InProgress` → `Done`) → Delete, with per-task permissions | ✅ |
| **Board** | A team-shared card order in each status column: drag a task above another or to the end, across columns too | ✅ |
| **Prioritization** | `Low` / `Medium` / `High` priority, optional due dates, overdue filter | ✅ |
| **Collaboration** | Comment threads on each task | ✅ |
| **Awareness** | In-app notifications (real-time via SignalR), email for assignments, deadline reminders a day before a task is due | ✅ |
| **Oversight** | Dashboard: tasks by status, overdue, due within 7 days; my assigned/created tasks, unread notifications, recent tasks | ✅ |
| **Access Control** | Role-based authorization; users manage only their own account, Admins manage everyone | ✅ |

### Example User Journey
1. Manager logs in → gets a JWT token
2. Manager creates a task and assigns it to a team member
3. Team member lists their tasks (`GET /api/tasks?assignedToId=…`) and sees what's overdue
4. Team member gets a notification (live over SignalR), moves the task to `InProgress`, adds comments
5. Manager checks the Dashboard (`GET /api/dashboard`) for overall progress and overdue items
6. Within 24 hours of the due date, the assignee gets a reminder, in the app and by email

---

## 🛠️ Tools & Technologies

### Platform
- **ASP.NET Core Web API on .NET 10**, C# with nullable reference types, records and pattern matching

### Architecture & Patterns
- **Clean Architecture** — Domain / Application / Infrastructure / API, dependencies pointing inward
- **CQRS**, custom-built (no MediatR):
  - `ICommand<T>` / `ICommandHandler<T,R>` and `IQuery<T>` / `IQueryHandler<T,R>`
  - `ICommandDispatcher` / `IQueryDispatcher` run the request's validators, then its handler (a typed wrapper per
    request type, built once and cached; a missing handler is a clear error)
- **Repository + Unit of Work** — generic `IRepository<T>` with projected, no-tracking and paged reads; `ExecuteInTransactionAsync` for resilient transactions
- **Result pattern** — handlers return `Result<T>` instead of throwing; a failure carries an error type (Validation, NotFound, Conflict, Unauthorized, Forbidden)
- **ProblemDetails (RFC 9457)** — failures are returned as `application/problem+json` with the matching status code; unexpected exceptions are logged and returned as a 500 ProblemDetails
- **FluentValidation** — validators run in the dispatchers, after the controller has set the route and token fields
  (400 with field errors)
- **Record DTOs** and `PaginatedList<T>` for paged responses

### Data Access
- **Entity Framework Core 10** (code-first, Fluent API configurations)
- **PostgreSQL 17** in Docker Compose on port **5433**, via Npgsql with retry on transient failures
- **snake_case** tables/columns, UTC `timestamptz` dates, time-ordered (v7) GUID keys
- **EF Core migrations**, applied automatically in Development, plus idempotent seed data
- Full guide: **[docs/DATABASE.md](docs/DATABASE.md)**

### Security
- **JWT Bearer** authentication (self-issued; signing key in user-secrets); every authenticated request also checks
  that the user still exists, is active and has the same security stamp as the token (one primary-key lookup), so
  deactivation takes effect immediately and a password or role change signs out every existing token
- **BCrypt** password hashing
- Role-based (`[Authorize(Roles = "Admin")]`) and ownership checks in controllers
- **Rate limiting** on login, register and change-password: a fixed window per client IP (10 per minute by default), then 429
- **CORS** only for the origins in `Cors:AllowedOrigins` (none by default; local front-end ports in Development)
- No known-vulnerable NuGet packages (`dotnet list package --vulnerable --include-transitive`)

### API, Real-time & Docs
- **ASP.NET Core controllers**, **Swagger UI** (Swashbuckle) in Development
- **SignalR** typed hub at `/hubs/notifications` (JWT via `access_token` query string), pushes `ReceiveNotification` to the user with `Clients.User` — see [docs/REALTIME.md](docs/REALTIME.md)
- **Email** with MailKit: queued in memory and sent by a background service, so SMTP problems never fail a request;
  [Mailpit](https://mailpit.axllent.org/) in Docker Compose catches the emails in development
- **Deadline reminders**: a `BackgroundService` with `PeriodicTimer` (every 15 minutes, idempotent)
- **Postman collection** with assertions: [postman/TaskFlow.postman_collection.json](postman/TaskFlow.postman_collection.json)

---

## 🏗️ Architecture

```
┌──────────────────────────────────────────────┐
│ TaskFlow.API                                  │
│ Controllers (Auth, Users, Tasks),             │
│ NotificationHub, RealTimeNotifier, Program.cs │
└──────────────────────┬───────────────────────┘
                       │ depends on
┌──────────────────────▼───────────────────────┐
│ TaskFlow.Infrastructure                       │
│ ApplicationDbContext, configurations,         │
│ migrations, Repository/UnitOfWork, seeder,    │
│ JWT / password / notification services        │
│ email (MailKit), deadline reminder job        │
└──────────────────────┬───────────────────────┘
                       │ implements ports of
┌──────────────────────▼───────────────────────┐
│ TaskFlow.Application                          │
│ Commands / Queries + handlers, validators,    │
│ DTOs, dispatchers, Result<T>, ports           │
│ (ICurrentUserService, IJwtService, ...)       │
└──────────────────────┬───────────────────────┘
                       │ depends on
┌──────────────────────▼───────────────────────┐
│ TaskFlow.Domain                               │
│ Entities, enums, IRepository<T>, IUnitOfWork  │
└───────────────────────────────────────────────┘
```

**Key rule:** the Application layer defines interfaces (ports) and never references Infrastructure or ASP.NET Core; Infrastructure and API implement them.

### Project layout
```
src/
  TaskFlow.Domain/          Common/BaseEntity, Entities, Enums, Interfaces
  TaskFlow.Application/     Commands/, Queries/, Validators/, DTOs/, Common/ (dispatchers, results, ports)
  TaskFlow.Infrastructure/  Data/ (Context, Configurations, Migrations, Repositories, Seeding), Services/ (incl. Email/), BackgroundJobs/
  TaskFlow.API/             Common/ (ProblemDetails mapping, exception handler), Controllers/, Hubs/, Services/, Program.cs, Dockerfile
  TaskFlow.Web/             Angular 22.3 frontend (Tailwind CSS, SignalR, CDK Board), nginx.conf, Dockerfile
docs/                       DATABASE.md (setup, seed data, testing), REALTIME.md (notifications, SignalR client), BUGFIXES.md
postman/                    API test collection
tests/                      TaskFlow.IntegrationTests (xUnit, WebApplicationFactory, Testcontainers)
docker-compose.yml          Full stack: PostgreSQL 17, Mailpit, TaskFlow.API, and TaskFlow.Web (Nginx)
```

---

## 📂 Domain Model

```
users              tasks                   comments          notifications
├─ id              ├─ id                   ├─ id             ├─ id
├─ first_name      ├─ title                ├─ content        ├─ user_id (FK)
├─ last_name       ├─ description          ├─ task_id (FK)   ├─ message
├─ email (unique)  ├─ status               ├─ user_id (FK)   ├─ type
├─ password_hash   ├─ priority             ├─ created_at     ├─ is_read
├─ role            ├─ due_date             └─ updated_at     ├─ related_task_id (FK)
├─ is_active       ├─ position                               ├─ created_at
├─ created_at      ├─ created_by_user_id (FK)                └─ updated_at
└─ updated_at      ├─ assigned_to_user_id (FK)
                   ├─ created_at
                   └─ updated_at
```
Enums are stored as text: status `Todo | InProgress | Done`, priority `Low | Medium | High`,
role `User | Manager | Admin`, notification type `TaskAssigned | TaskUpdated | TaskCompleted | CommentAdded | DeadlineApproaching`.
Deleting a task deletes its comments (cascade) and keeps its notifications with `related_task_id` set to null.
`position` is the task's place in its status column (0 = top, index on `(status, position)`); only the order
matters, so the gap a deleted task leaves is fine.

---

## 🔌 API Endpoints

All endpoints except register/login need a `Bearer` token.

| Method | Route | Who | Description |
|---|---|---|---|
| POST | `/api/auth/register` | anyone | Create an account (role `User`) |
| POST | `/api/auth/login` | anyone | Get a JWT (inactive accounts are rejected) |
| POST | `/api/auth/change-password` | logged in | Change own password; signs out every session, so log in again with the new password |
| GET | `/api/users` | logged in | List users (each with `isActive`); `?isActive=true\|false` |
| GET | `/api/users/me` | logged in | Current user |
| GET | `/api/users/{id}` | logged in | One user |
| PUT | `/api/users/{id}` | self or Admin | Update name/email |
| PUT | `/api/users/{id}/role` | Admin | Change role (the last active Admin can't be demoted); the user's existing tokens stop working |
| PATCH | `/api/users/{id}/active` | Admin | Deactivate `{ "isActive": false }` or reactivate `true` (not yourself, not the last active Admin) |
| DELETE | `/api/users/{id}` | self or Admin | Delete (not if the user created tasks) |
| GET | `/api/tasks` | logged in | List tasks: filters `status`, `priority`, `assignedToId`, `unassigned`, `createdById`, `overdue`; `sort=createdAt` (default, newest first) or `sort=position` (board order: status, then position); paging `page`, `pageSize` |
| GET | `/api/tasks/{id}` | logged in | One task with names and comment count |
| POST | `/api/tasks` | logged in | Create a task (creator = caller) |
| PUT | `/api/tasks/{id}` | creator, Manager, Admin | Replace title, description, priority, due date, status, assignee |
| PATCH | `/api/tasks/{id}/status` | creator, assignee, Manager, Admin | Change status: `{ "status": "InProgress" }` (the task goes to the end of the new column) |
| PATCH | `/api/tasks/{id}/move` | creator, assignee, Manager, Admin | Board drag and drop: `{ "status": "Done", "beforeTaskId": "<guid>" }` puts the task directly above that task in the column, `null` at the end; a status change notifies like `PATCH .../status` |
| PATCH | `/api/tasks/{id}/assignee` | creator, Manager, Admin | Assign `{ "assignedToId": "<guid>" }` or unassign with `null` (active users only) |
| DELETE | `/api/tasks/{id}` | creator, Admin | Delete a task (its comments too) |
| GET | `/api/tasks/{taskId}/comments` | logged in | A task's comments, oldest first, with author names; paging `page`, `pageSize` |
| POST | `/api/tasks/{taskId}/comments` | logged in | Add a comment `{ "content": "..." }` (1-1000 characters; author = caller) |
| PUT | `/api/comments/{id}` | author | Edit a comment |
| DELETE | `/api/comments/{id}` | author, Admin | Delete a comment |
| GET | `/api/notifications` | logged in (own) | Your notifications, newest first; `unread=true\|false`, `page`, `pageSize` |
| GET | `/api/notifications/unread-count` | logged in (own) | Number of unread notifications |
| PATCH | `/api/notifications/{id}/read` | logged in (own) | Mark one as read (404 if not yours) |
| POST | `/api/notifications/read-all` | logged in (own) | Mark all as read; returns how many changed |
| GET | `/api/dashboard` | logged in | All tasks by status, overdue and due-within-7-days counts; your assigned tasks by status, open tasks you created, unread notifications, your 5 most recently updated tasks |
| POST | `/api/reminders/run` | Admin | Run the deadline reminders now (the background job runs them every 15 minutes); returns the reminded task ids |
| WS | `/hubs/notifications` | logged in | SignalR: receive `ReceiveNotification` live ([docs/REALTIME.md](docs/REALTIME.md)) |

### Responses and errors

Success: `200` with `{ "isSuccess": true, "data": { ... } }`. Failure: an RFC 9457 ProblemDetails
(`application/problem+json`) whose `title` is the error type and `detail` the message:

```json
{ "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5", "title": "NotFound", "status": 404,
  "detail": "Task not found", "traceId": "00-..." }
```

| Status | `title` | When |
|---|---|---|
| 400 | `Validation` (validator and binding errors add an `errors` object per field) | Invalid input: validator errors, malformed JSON, unknown priority/status/role/sort, unknown assignee, a `beforeTaskId` that is the task itself or not in the target column, wrong current password |
| 401 | `Unauthorized` | Missing/invalid token, or the token of a deactivated/deleted user; failed login (same message for unknown email, wrong password and inactive account) |
| 403 | `Forbidden` | Logged in but not allowed (e.g. a User editing another account, a non-Admin changing a role) |
| 404 | `NotFound` | Unknown user or task |
| 409 | `Conflict` | Email already registered/taken, deleting a user who created tasks, demoting or deleting the last active Admin, deactivating yourself or the last active Admin |
| 429 | `TooManyRequests` | Too many logins/registrations from one IP; wait for the `Retry-After` seconds |
| 500 | `Failure` | Unexpected error (logged; no internals in the response) |

Details, examples and the seeded test data: [docs/DATABASE.md](docs/DATABASE.md).

---

## ✅ Current Status

| Area | Status |
|---|---|
| Domain entities, enums, EF configurations, indexes | ✅ Done |
| PostgreSQL + migrations + seed data (Docker Compose) | ✅ Done |
| Repository / Unit of Work (cancellation, projections, paging, resilient transactions) | ✅ Done |
| CQRS dispatchers (cached typed wrappers, validation step), `Result<T>` with error types, FluentValidation | ✅ Done |
| Hardening: no vulnerable packages, unused packages and dead code removed, CORS from configuration, login/register rate limiting | ✅ Done |
| Auth: register, login, change password, JWT, inactive-user check (at login and on every request) | ✅ Done |
| Users: list, get, update, role change, delete with permission checks | ✅ Done |
| Tasks: create, list with filters/paging, get by id | ✅ Done |
| Tasks: update, status change, assign/unassign, delete with per-task permissions | ✅ Done |
| Task position: board order per status column (`PATCH /api/tasks/{id}/move`, `GET /api/tasks?sort=position`) | ✅ Done |
| Comments: list (paged), add, edit (author), delete (author or Admin) | ✅ Done |
| Notifications API + automatic notifications on task/comment events (saved with the change) | ✅ Done |
| SignalR push (typed hub, `Clients.User`, after the save) | ✅ Done |
| Dashboard (`GET /api/dashboard`, a handful of indexed count queries) | ✅ Done |
| User activation / deactivation (`PATCH /api/users/{id}/active`, Admin) | ✅ Done |
| Email (task assigned, deadline reminder) with MailKit, Mailpit for development | ✅ Done |
| Deadline reminders (background job + `POST /api/reminders/run`, one reminder per due date) | ✅ Done |
| Error types → 400/401/403/404/409 + ProblemDetails, global exception handler | ✅ Done |
| Automated tests | ✅ 111 tests (xUnit + Testcontainers PostgreSQL and Mailpit): auth, users, user activation, token revocation, tasks, lifecycle, task position, comments, notifications + SignalR, dashboard, deadline reminders, email, validation, dispatchers, CORS, rate limiting |

---

## 🚀 How to Run

### Option 1: Full Stack with Docker Compose (recommended)

Prerequisite: Docker Desktop.

```powershell
# 1. Configure environment variables (set POSTGRES_PASSWORD and a 32+ char JWT_SECRET)
Copy-Item .env.example .env

# 2. Build and start all 4 services (postgres, mailpit, api, web)
docker compose up -d --build

# 3. Verify all services are running / healthy
docker compose ps
```

- **Web App:** `http://localhost:4200` (Nginx serves the Angular SPA and reverse-proxies `/api/` and `/hubs/` WebSockets to `api:8080`)
- **API Swagger UI:** `http://localhost:8080/swagger` (when `ASPNETCORE_ENVIRONMENT=Development`)
- **API Health Check:** `http://localhost:8080/health`
- **Mailpit Web UI:** `http://localhost:8025` (SMTP is enabled by default in Docker)

### Option 2: Hybrid Local Development (host API + Web, Docker Postgres + Mailpit)

Prerequisites: .NET 10 SDK, Node.js, and Docker Desktop. Full setup, the seeded accounts/tasks/comments/notifications
and how to use them: **[docs/DATABASE.md](docs/DATABASE.md)**.

```powershell
# 1. Start only PostgreSQL and Mailpit (first time: Copy-Item .env.example .env and set POSTGRES_PASSWORD,
#    then put the connection string and JWT secret in user-secrets, see docs/DATABASE.md)
docker compose up -d postgres mailpit

# 2. Run the API (Development applies migrations and seeds the data on start)
cd src\TaskFlow.API
dotnet run --launch-profile https

# 3. Run the Angular web app (in a separate terminal)
cd src\TaskFlow.Web
npm install
npm start
```

- **Web App:** `http://localhost:4200`
- **Swagger UI:** `https://localhost:7103/swagger`

Seeded logins (password `P@ssw0rd`): `admin@taskflow.dev`, `manager@taskflow.dev`, `user@taskflow.dev`,
`user2@taskflow.dev`, and the deactivated `inactive@taskflow.dev` (login is rejected until an Admin reactivates it).

When running the API directly on the host, email is off by default. To see emails in Mailpit (`http://localhost:8025`),
run with `--Smtp:Enabled=true` (or set it in user-secrets); see [docs/DATABASE.md](docs/DATABASE.md#410-deadline-reminders-and-email).

## 🧪 Testing

- **Integration tests:** `dotnet test` (Docker must be running). `tests/TaskFlow.IntegrationTests` starts the API in memory (`WebApplicationFactory`) against a throw-away PostgreSQL 17 container (Testcontainers) with migrations and seed data. It never touches the development database.
- **Postman:** import [postman/TaskFlow.postman_collection.json](postman/TaskFlow.postman_collection.json) and run the whole collection in order (Collection Runner). It logs in every seeded account and checks auth, permissions, users and tasks.
- **Database:** `docker exec -it taskflow-postgres psql -U taskflow -d taskflow`, or pgAdmin on `localhost:5433` (see [docs/DATABASE.md](docs/DATABASE.md)).
- **Reset:** `dotnet run -- --Seed:ResetDatabase=true` in `src/TaskFlow.API` drops, migrates and reseeds the database.
