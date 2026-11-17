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
| **User Accounts** | Register, log in, manage profile, change password. Roles: `User`, `Manager`, `Admin`. Deactivated accounts can't log in | ✅ |
| **Task Lifecycle** | Create → Assign → Work (`Todo` → `InProgress` → `Done`) | 🟡 create and read only |
| **Prioritization** | `Low` / `Medium` / `High` priority, optional due dates, overdue filter | ✅ |
| **Collaboration** | Comment threads on each task | 🟡 stored and seeded, no API yet |
| **Awareness** | In-app notifications (real-time via SignalR) + email alerts | 🟡 stored and seeded, no API or triggers yet |
| **Oversight** | Dashboard with stats (total/todo/in-progress/done/overdue) and "my tasks" | ⏳ |
| **Access Control** | Role-based authorization; users manage only their own account, Admins manage everyone | ✅ |

### Example User Journey
1. Manager logs in → gets a JWT token
2. Manager creates a task and assigns it to a team member
3. Team member lists their tasks (`GET /api/tasks?assignedToId=…`) and sees what's overdue
4. *(planned)* Team member gets a notification, moves the task to `InProgress`, adds comments
5. *(planned)* Manager checks the Dashboard for overall progress and overdue items

---

## 🛠️ Tools & Technologies

### Platform
- **ASP.NET Core Web API on .NET 10**, C# with nullable reference types, records and pattern matching

### Architecture & Patterns
- **Clean Architecture** — Domain / Application / Infrastructure / API, dependencies pointing inward
- **CQRS**, custom-built (no MediatR):
  - `ICommand<T>` / `ICommandHandler<T,R>` and `IQuery<T>` / `IQueryHandler<T,R>`
  - `ICommandDispatcher` / `IQueryDispatcher` resolve handlers from DI
- **Repository + Unit of Work** — generic `IRepository<T>` with projected, no-tracking and paged reads; `ExecuteInTransactionAsync` for resilient transactions
- **Result pattern** — handlers return `Result<T>` instead of throwing; a failure carries an error type (Validation, NotFound, Conflict, Unauthorized, Forbidden)
- **ProblemDetails (RFC 9457)** — failures are returned as `application/problem+json` with the matching status code; unexpected exceptions are logged and returned as a 500 ProblemDetails
- **FluentValidation** — request validation before handlers run (400 with field errors)
- **Record DTOs** and `PaginatedList<T>` for paged responses

### Data Access
- **Entity Framework Core 10** (code-first, Fluent API configurations)
- **PostgreSQL 17** in Docker Compose on port **5433**, via Npgsql with retry on transient failures
- **snake_case** tables/columns, UTC `timestamptz` dates, time-ordered (v7) GUID keys
- **EF Core migrations**, applied automatically in Development, plus idempotent seed data
- Full guide: **[docs/DATABASE.md](docs/DATABASE.md)**

### Security
- **JWT Bearer** authentication (self-issued; signing key in user-secrets)
- **BCrypt** password hashing
- Role-based (`[Authorize(Roles = "Admin")]`) and ownership checks in controllers

### API, Real-time & Docs
- **ASP.NET Core controllers**, **Swagger UI** (Swashbuckle) in Development
- **SignalR** hub at `/hubs/notifications` (JWT via `access_token` query string)
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
  TaskFlow.Infrastructure/  Data/ (Context, Configurations, Migrations, Repositories, Seeding), Services/
  TaskFlow.API/             Common/ (ProblemDetails mapping, exception handler), Controllers/, Hubs/, Services/, Program.cs
docs/                       DATABASE.md (setup, seed data, testing), BUGFIXES.md
postman/                    API test collection
tests/                      TaskFlow.IntegrationTests (xUnit, WebApplicationFactory, Testcontainers)
docker-compose.yml          Local PostgreSQL 17
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
├─ is_active       ├─ created_by_user_id (FK)                ├─ created_at
├─ created_at      ├─ assigned_to_user_id (FK)               └─ updated_at
└─ updated_at      ├─ created_at
                   └─ updated_at
```
Enums are stored as text: status `Todo | InProgress | Done`, priority `Low | Medium | High`,
role `User | Manager | Admin`, notification type `TaskAssigned | TaskUpdated | TaskCompleted | CommentAdded | DeadlineApproaching`.
Deleting a task deletes its comments (cascade) and keeps its notifications with `related_task_id` set to null.

---

## 🔌 API Endpoints

All endpoints except register/login need a `Bearer` token.

| Method | Route | Who | Description |
|---|---|---|---|
| POST | `/api/auth/register` | anyone | Create an account (role `User`) |
| POST | `/api/auth/login` | anyone | Get a JWT (inactive accounts are rejected) |
| POST | `/api/auth/change-password` | logged in | Change own password |
| GET | `/api/users` | logged in | List users |
| GET | `/api/users/me` | logged in | Current user |
| GET | `/api/users/{id}` | logged in | One user |
| PUT | `/api/users/{id}` | self or Admin | Update name/email |
| PUT | `/api/users/{id}/role` | Admin | Change role (the last Admin can't be demoted) |
| DELETE | `/api/users/{id}` | self or Admin | Delete (not if the user created tasks) |
| GET | `/api/tasks` | logged in | List tasks: filters `status`, `priority`, `assignedToId`, `unassigned`, `createdById`, `overdue`; paging `page`, `pageSize` |
| GET | `/api/tasks/{id}` | logged in | One task with names and comment count |
| POST | `/api/tasks` | logged in | Create a task (creator = caller) |
| PUT | `/api/tasks/{id}` | creator, Manager, Admin | Replace title, description, priority, due date, status, assignee |
| PATCH | `/api/tasks/{id}/status` | creator, assignee, Manager, Admin | Change status: `{ "status": "InProgress" }` |
| PATCH | `/api/tasks/{id}/assignee` | creator, Manager, Admin | Assign `{ "assignedToId": "<guid>" }` or unassign with `null` (active users only) |
| DELETE | `/api/tasks/{id}` | creator, Admin | Delete a task (its comments too) |

### Responses and errors

Success: `200` with `{ "isSuccess": true, "data": { ... } }`. Failure: an RFC 9457 ProblemDetails
(`application/problem+json`) whose `title` is the error type and `detail` the message:

```json
{ "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5", "title": "NotFound", "status": 404,
  "detail": "Task not found", "traceId": "00-..." }
```

| Status | `title` | When |
|---|---|---|
| 400 | `Validation` (or "One or more validation errors occurred." with an `errors` object per field) | Invalid input: validator errors, unknown priority/status/role, unknown assignee, wrong current password |
| 401 | `Unauthorized` | Missing/invalid token; failed login (same message for unknown email, wrong password and inactive account) |
| 403 | `Forbidden` | Logged in but not allowed (e.g. a User editing another account, a non-Admin changing a role) |
| 404 | `NotFound` | Unknown user or task |
| 409 | `Conflict` | Email already registered/taken, deleting a user who created tasks, demoting the last Admin |
| 500 | `Failure` | Unexpected error (logged; no internals in the response) |

Details, examples and the seeded test data: [docs/DATABASE.md](docs/DATABASE.md).

---

## ✅ Current Status

| Area | Status |
|---|---|
| Domain entities, enums, EF configurations, indexes | ✅ Done |
| PostgreSQL + migrations + seed data (Docker Compose) | ✅ Done |
| Repository / Unit of Work (cancellation, projections, paging, resilient transactions) | ✅ Done |
| CQRS dispatchers, `Result<T>` with error types, FluentValidation | ✅ Done (dispatcher still uses reflection) |
| Auth: register, login, change password, JWT, inactive-user check | ✅ Done |
| Users: list, get, update, role change, delete with permission checks | ✅ Done |
| Tasks: create, list with filters/paging, get by id | ✅ Done |
| Tasks: update, status change, assign/unassign, delete with per-task permissions | ✅ Done |
| Comments API | ⏳ Planned |
| Notifications API + automatic notifications on task events | ⏳ Planned (persistence service exists) |
| SignalR push | 🟡 Hub and notifier exist; connection tracking to be replaced, not triggered yet |
| Dashboard | ⏳ Planned |
| User activation / deactivation endpoint | ⏳ Planned |
| Email alerts, deadline reminders | ⏳ Planned (MailKit referenced, no implementation) |
| Error types → 400/401/403/404/409 + ProblemDetails, global exception handler | ✅ Done |
| Automated tests | 🟡 Integration test project (xUnit + Testcontainers PostgreSQL) with the first auth/users/tasks tests; grows with each feature |

---

## 🚀 How to Run

Prerequisites: .NET 10 SDK and Docker Desktop. Full setup, the seeded accounts/tasks/comments/notifications
and how to use them: **[docs/DATABASE.md](docs/DATABASE.md)**.

```powershell
# 1. Start PostgreSQL (first time: Copy-Item .env.example .env and set a password,
#    then put the connection string and JWT secret in user-secrets, see docs/DATABASE.md)
docker compose up -d

# 2. Run the API (Development applies migrations and seeds the data on start)
cd src\TaskFlow.API
dotnet run --launch-profile https

# 3. Open Swagger UI
https://localhost:7103/swagger
```

Seeded logins (password `P@ssw0rd`): `admin@taskflow.dev`, `manager@taskflow.dev`, `user@taskflow.dev`,
`user2@taskflow.dev`, and the deactivated `inactive@taskflow.dev` (login is rejected).

## 🧪 Testing

- **Integration tests:** `dotnet test` (Docker must be running). `tests/TaskFlow.IntegrationTests` starts the API in memory (`WebApplicationFactory`) against a throw-away PostgreSQL 17 container (Testcontainers) with migrations and seed data. It never touches the development database.
- **Postman:** import [postman/TaskFlow.postman_collection.json](postman/TaskFlow.postman_collection.json) and run the whole collection in order (Collection Runner). It logs in every seeded account and checks auth, permissions, users and tasks.
- **Database:** `docker exec -it taskflow-postgres psql -U taskflow -d taskflow`, or pgAdmin on `localhost:5433` (see [docs/DATABASE.md](docs/DATABASE.md)).
- **Reset:** `dotnet run -- --Seed:ResetDatabase=true` in `src/TaskFlow.API` drops, migrates and reseeds the database.
