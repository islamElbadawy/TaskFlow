# TaskFlow — Project Overview

A real-time collaborative task management system built with **.NET Core**, following **Clean Architecture** and **CQRS** principles.

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

| Capability | Description |
|---|---|
| **User Accounts** | Register, log in, manage profile. Roles: `User`, `Manager`, `Admin` |
| **Task Lifecycle** | Create → Assign → Work (`Todo` → `InProgress` → `Done`) |
| **Prioritization** | Tasks have `Low` / `Medium` / `High` priority and optional due dates |
| **Collaboration** | Comment threads on each task |
| **Awareness** | In-app notifications + email alerts when tasks are assigned/updated/commented |
| **Oversight** | Dashboard with stats (total/todo/in-progress/done/overdue) and "my tasks" |
| **Access Control** | Role-based authorization (e.g., only Admins can delete users) |

### Example User Journey
1. Manager registers and logs in → gets a JWT token
2. Manager creates a task, assigns it to a team member
3. Team member gets a notification + email
4. Team member updates status to `InProgress`, adds comments
5. Manager checks the Dashboard to see overall progress and overdue items

---

## 🛠️ Tools & Technologies

### Platform
- **.NET 8+ / ASP.NET Core Web API** — main framework
- **C# 12** (records, pattern matching, nullable reference types)

### Architecture & Patterns
- **Clean Architecture** — Domain / Application / Infrastructure / API layered separation
- **CQRS** (Command Query Responsibility Segregation) — custom-built (no MediatR), using:
  - `ICommand<T>` / `ICommandHandler<T,R>`
  - `IQuery<T>` / `IQueryHandler<T,R>`
  - `ICommandDispatcher` / `IQueryDispatcher` (resolve handlers via DI + reflection)
- **Repository Pattern + Unit of Work** — abstracts data access from business logic
- **Result Pattern** — `Result<T>` wrapper for success/failure instead of exceptions
- **Record DTOs** — immutable, value-equality data transfer objects

### Data Access
- **Entity Framework Core** (Code-First)
- **SQL Server** as the database
- **Fluent API configurations** (`IEntityTypeConfiguration<T>`) for entity mapping
- **EF Core Migrations** for schema versioning

### Security
- **JWT Bearer Authentication** (`System.IdentityModel.Tokens.Jwt`)
- **BCrypt.Net** for password hashing
- **Role-based Authorization** (`[Authorize(Roles = "Admin")]`)

### API & Docs
- **ASP.NET Core Web API Controllers**
- **Swagger / Swashbuckle** with JWT "Authorize" support for interactive testing
- **CORS** enabled for frontend integration

### Cross-Cutting Services
- `ICurrentUserService` — reads current user from JWT claims via `HttpContext`
- `INotificationService` — creates in-app notifications *(stubbed, to be completed later)*
- `IEmailService` — sends email alerts *(stubbed, to be completed later)*
- `IJwtService` — generates & validates JWT tokens

### Planned / Upcoming
- **SignalR** — real-time push notifications
- **MailKit / SMTP** — actual email delivery
- **Docker** — containerized deployment
- **xUnit** — unit & integration testing

---

## 🏗️ Architecture Diagram

```
┌─────────────────────────────────────────┐
│           TaskFlow.API                   │
│   Controllers, Middleware, Program.cs    │
│   (Auth, Tasks, Comments, Notifications, │
│    Dashboard, Users)                     │
└───────────────────┬───────────────────────┘
                     │ depends on
┌────────────────────▼──────────────────────┐
│        TaskFlow.Application                │
│  Commands / Queries / Handlers / DTOs      │
│  CQRS Dispatchers, Result<T>                │
└───────────────────┬────────────────────────┘
                     │ depends on
┌────────────────────▼────────────────────┐
│           TaskFlow.Domain                 │
│  Entities, Enums, Repository Interfaces   │
└────────────────────▲────────────────────┘
                     │ implements
┌────────────────────┴────────────────────┐
│        TaskFlow.Infrastructure            │
│  EF Core DbContext, Repositories,         │
│  JWT/Password/Notification/Email Services │
└────────────────────────────────────────────┘
```

**Key rule:** Dependencies only point *inward* (API → Application → Domain). Infrastructure *implements* Domain interfaces — it never gets referenced directly by Application command/query logic (fixed the earlier circular dependency issue).

---

## 📂 Domain Model

```
Users            Tasks                 Comments        Notifications
├─ Id            ├─ Id                 ├─ Id           ├─ Id
├─ FirstName     ├─ Title              ├─ Content      ├─ UserId (FK)
├─ LastName      ├─ Description        ├─ TaskId (FK)  ├─ Message
├─ Email         ├─ Status             ├─ UserId (FK)  ├─ Type
├─ PasswordHash  ├─ Priority           └─ CreatedAt     ├─ IsRead
├─ Role          ├─ DueDate                             ├─ RelatedTaskId
└─ CreatedAt     ├─ CreatedById (FK)                    └─ CreatedAt
                 ├─ AssignedToId (FK)
                 └─ CreatedAt/UpdatedAt
```

---

## ✅ Current Status

| Layer / Feature | Status |
|---|---|
| Domain entities & enums | ✅ Done |
| EF Core DbContext + configurations | ✅ Done |
| Repository + Unit of Work | ✅ Done |
| CQRS infrastructure (dispatchers, results) | ✅ Done |
| All Commands/Queries (24 handlers) | ✅ Done |
| Record DTOs | ✅ Done |
| JWT Authentication (Login/Register) | ✅ Done |
| API Controllers (Auth, Users, Tasks, Comments, Notifications, Dashboard) | ✅ Done |
| Database migration | ✅ Ready to run |
| Circular dependency fix (repository includes) | ✅ Done |
| Email sending | ⏳ Stubbed — to implement |
| In-app notification persistence | ⏳ Stubbed — to implement |
| SignalR real-time updates | ⏳ Not started |
| Automated tests | ⏳ Not started |
| Docker deployment | ⏳ Not started |

---

## 🚀 How to Run (Quick Reference)

```bash
# 1. Apply database migration
dotnet ef database update --project TaskFlow.Infrastructure --startup-project TaskFlow.API

# 2. Run the API
cd TaskFlow.API
dotnet run

# 3. Open Swagger UI
https://localhost:5001/swagger
```

Basic flow: **Register → Login (get JWT) → Authorize in Swagger → Create/Assign/Comment on Tasks → Check Dashboard**
