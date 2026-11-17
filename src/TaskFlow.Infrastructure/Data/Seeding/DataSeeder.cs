using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Infrastructure.Data.Seeding;

/// <summary>
/// Development seed data with fixed ids, documented in docs/DATABASE.md.
/// Idempotent: each record is inserted only if its id is not in the database yet, so it is safe to run on
/// every start, and records you deleted or added by hand are left alone. Dates are relative to the first run.
/// </summary>
public class DataSeeder
{
    public const string DefaultPassword = "P@ssw0rd";

    public static class UserIds
    {
        public static readonly Guid Admin = Guid.Parse("11111111-1111-1111-1111-111111111111");
        public static readonly Guid Manager = Guid.Parse("22222222-2222-2222-2222-222222222222");
        public static readonly Guid User = Guid.Parse("33333333-3333-3333-3333-333333333333");
        public static readonly Guid User2 = Guid.Parse("44444444-4444-4444-4444-444444444444");
        public static readonly Guid Inactive = Guid.Parse("55555555-5555-5555-5555-555555555555");
    }

    public static class TaskIds
    {
        public static readonly Guid SetupProject = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public static readonly Guid WriteDocs = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        public static readonly Guid FixPasswordReset = Guid.Parse("a0000000-0000-0000-0000-000000000003");
        public static readonly Guid DesignNotifications = Guid.Parse("a0000000-0000-0000-0000-000000000004");
        public static readonly Guid SetupCi = Guid.Parse("a0000000-0000-0000-0000-000000000005");
        public static readonly Guid ReviewBacklog = Guid.Parse("a0000000-0000-0000-0000-000000000006");
        public static readonly Guid MigrateToPostgres = Guid.Parse("a0000000-0000-0000-0000-000000000007");
        public static readonly Guid ArchiveReports = Guid.Parse("a0000000-0000-0000-0000-000000000008");
    }

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordService _passwordService;

    public DataSeeder(IUnitOfWork unitOfWork, IPasswordService passwordService)
    {
        _unitOfWork = unitOfWork;
        _passwordService = passwordService;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        DateTime DaysAgo(double days) => now.AddDays(-days);
        DateTime DaysFromNow(double days) => now.AddDays(days);

        // ---- Users (all share DefaultPassword) ----
        var users = new[]
        {
            NewUser(UserIds.Admin, "Admin", "User", "admin@taskflow.dev", UserRole.Admin, createdAt: DaysAgo(60)),
            NewUser(UserIds.Manager, "Manager", "User", "manager@taskflow.dev", UserRole.Manager, createdAt: DaysAgo(45)),
            NewUser(UserIds.User, "Regular", "User", "user@taskflow.dev", UserRole.User, createdAt: DaysAgo(30)),
            NewUser(UserIds.User2, "Sam", "Taylor", "user2@taskflow.dev", UserRole.User, createdAt: DaysAgo(14)),
            NewUser(UserIds.Inactive, "Inactive", "User", "inactive@taskflow.dev", UserRole.User, createdAt: DaysAgo(90),
                isActive: false, updatedAt: DaysAgo(20)),
        };

        var existingUsers = await _unitOfWork.Users.ListAsync(null, u => new { u.Id, u.Email }, cancellationToken);
        var existingUserIds = existingUsers.Select(u => u.Id).ToHashSet();
        var existingEmails = existingUsers.Select(u => u.Email).ToHashSet();
        foreach (var (id, build) in users)
        {
            if (existingUserIds.Contains(id)) continue;
            var user = build();
            // Someone registered this email by hand: keep theirs, skip the seed account (email is unique).
            if (existingEmails.Contains(user.Email)) continue;
            await _unitOfWork.Users.AddAsync(user, cancellationToken);
            existingUserIds.Add(id);
        }

        // ---- Tasks: every status and priority, overdue, due soon, no due date, unassigned ----
        var tasks = new[]
        {
            NewTask(TaskIds.SetupProject, "Setup project", "Initial project setup: solution, layers and CI skeleton.",
                TaskItemStatus.Done, TaskPriority.High, createdBy: UserIds.Admin, assignedTo: UserIds.Manager,
                due: DaysAgo(35), createdAt: DaysAgo(40), updatedAt: DaysAgo(35)),
            NewTask(TaskIds.WriteDocs, "Write docs", "Add README and docs, including the seed accounts table.",
                TaskItemStatus.InProgress, TaskPriority.Medium, createdBy: UserIds.Manager, assignedTo: UserIds.User,
                due: DaysFromNow(3), createdAt: DaysAgo(20), updatedAt: DaysAgo(3)),
            NewTask(TaskIds.FixPasswordReset, "Fix password reset email", "Reset emails are not delivered in staging.",
                TaskItemStatus.Todo, TaskPriority.High, createdBy: UserIds.Manager, assignedTo: UserIds.User,
                due: DaysAgo(2), createdAt: DaysAgo(10)),
            NewTask(TaskIds.DesignNotifications, "Design notifications panel", "Sketch the in-app notifications dropdown.",
                TaskItemStatus.Todo, TaskPriority.Low, createdBy: UserIds.Manager, assignedTo: UserIds.User2,
                due: null, createdAt: DaysAgo(7)),
            NewTask(TaskIds.SetupCi, "Set up CI pipeline", "Build and test on every pull request.",
                TaskItemStatus.Todo, TaskPriority.Medium, createdBy: UserIds.Admin, assignedTo: null,
                due: DaysFromNow(14), createdAt: DaysAgo(5)),
            NewTask(TaskIds.ReviewBacklog, "Review sprint backlog", "Groom and estimate the backlog before planning.",
                TaskItemStatus.InProgress, TaskPriority.Medium, createdBy: UserIds.User, assignedTo: UserIds.Manager,
                due: DaysFromNow(1), createdAt: DaysAgo(3), updatedAt: DaysAgo(1)),
            NewTask(TaskIds.MigrateToPostgres, "Migrate database to PostgreSQL", "Switch EF Core to Npgsql and add migrations.",
                TaskItemStatus.InProgress, TaskPriority.High, createdBy: UserIds.Admin, assignedTo: UserIds.Admin,
                due: DaysFromNow(7), createdAt: DaysAgo(2)),
            NewTask(TaskIds.ArchiveReports, "Archive old reports", "Move last year's reports to cold storage.",
                TaskItemStatus.Done, TaskPriority.Low, createdBy: UserIds.Manager, assignedTo: UserIds.Inactive,
                due: DaysAgo(30), createdAt: DaysAgo(60), updatedAt: DaysAgo(25)),
        };
        var existingTaskIds = (await _unitOfWork.TasksItems.ListAsync(null, t => t.Id, cancellationToken)).ToHashSet();
        foreach (var task in tasks)
        {
            if (existingTaskIds.Contains(task.Id)) continue;
            if (!existingUserIds.Contains(task.CreatedByUserId)) continue;
            if (task.AssignedToUserId is { } assignee && !existingUserIds.Contains(assignee)) continue;
            await _unitOfWork.TasksItems.AddAsync(task, cancellationToken);
            existingTaskIds.Add(task.Id);
        }

        // ---- Comments ----
        var comments = new[]
        {
            NewComment("c0000000-0000-0000-0000-000000000001", TaskIds.SetupProject, UserIds.Manager,
                "Repository and solution structure are in place.", DaysAgo(36)),
            NewComment("c0000000-0000-0000-0000-000000000002", TaskIds.SetupProject, UserIds.Admin,
                "Thanks, looks good. Closing this.", DaysAgo(35)),
            NewComment("c0000000-0000-0000-0000-000000000003", TaskIds.WriteDocs, UserIds.User,
                "Started on the README; the API section is next.", DaysAgo(5)),
            NewComment("c0000000-0000-0000-0000-000000000004", TaskIds.WriteDocs, UserIds.Manager,
                "Please include the seed accounts table.", DaysAgo(4)),
            NewComment("c0000000-0000-0000-0000-000000000005", TaskIds.FixPasswordReset, UserIds.User,
                "Reproduced it locally; looking at the SMTP settings.", DaysAgo(1)),
            NewComment("c0000000-0000-0000-0000-000000000006", TaskIds.ReviewBacklog, UserIds.Manager,
                "I'll go through it before Thursday's planning.", DaysAgo(1)),
        };
        var existingCommentIds = (await _unitOfWork.Comments.ListAsync(null, c => c.Id, cancellationToken)).ToHashSet();
        foreach (var comment in comments)
        {
            if (existingCommentIds.Contains(comment.Id)) continue;
            if (!existingTaskIds.Contains(comment.TaskId) || !existingUserIds.Contains(comment.UserId)) continue;
            await _unitOfWork.Comments.AddAsync(comment, cancellationToken);
        }

        // ---- Notifications: every type, read and unread ----
        var notifications = new[]
        {
            NewNotification("e0000000-0000-0000-0000-000000000001", UserIds.User, NotificationType.TaskAssigned,
                "Manager User assigned you \"Fix password reset email\"", TaskIds.FixPasswordReset, isRead: false, DaysAgo(10)),
            NewNotification("e0000000-0000-0000-0000-000000000002", UserIds.Manager, NotificationType.CommentAdded,
                "Regular User commented on \"Write docs\"", TaskIds.WriteDocs, isRead: true, DaysAgo(5)),
            NewNotification("e0000000-0000-0000-0000-000000000003", UserIds.Admin, NotificationType.TaskCompleted,
                "\"Setup project\" was marked as done", TaskIds.SetupProject, isRead: true, DaysAgo(35)),
            NewNotification("e0000000-0000-0000-0000-000000000004", UserIds.User, NotificationType.DeadlineApproaching,
                "\"Write docs\" is due in 3 days", TaskIds.WriteDocs, isRead: false, now.AddHours(-1)),
            NewNotification("e0000000-0000-0000-0000-000000000005", UserIds.User2, NotificationType.TaskAssigned,
                "Manager User assigned you \"Design notifications panel\"", TaskIds.DesignNotifications, isRead: false, DaysAgo(7)),
            NewNotification("e0000000-0000-0000-0000-000000000006", UserIds.User, NotificationType.TaskUpdated,
                "Manager User changed the due date of \"Write docs\"", TaskIds.WriteDocs, isRead: true, DaysAgo(3)),
        };
        var existingNotificationIds =
            (await _unitOfWork.Notifications.ListAsync(null, n => n.Id, cancellationToken)).ToHashSet();
        foreach (var notification in notifications)
        {
            if (existingNotificationIds.Contains(notification.Id)) continue;
            if (!existingUserIds.Contains(notification.UserId)) continue;
            await _unitOfWork.Notifications.AddAsync(notification, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // Users are built lazily so the (slow, by design) BCrypt hash runs only for accounts actually inserted.
    private (Guid Id, Func<User> Build) NewUser(Guid id, string firstName, string lastName, string email,
        UserRole role, DateTime createdAt, bool isActive = true, DateTime? updatedAt = null) =>
        (id, () => Stamp(new User
        {
            Id = id,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PasswordHash = _passwordService.HashPassword(DefaultPassword),
            Role = role,
            IsActive = isActive
        }, createdAt, updatedAt));

    private static TaskItem NewTask(Guid id, string title, string description, TaskItemStatus status,
        TaskPriority priority, Guid createdBy, Guid? assignedTo, DateTime? due, DateTime createdAt,
        DateTime? updatedAt = null) =>
        Stamp(new TaskItem
        {
            Id = id,
            Title = title,
            Description = description,
            Status = status,
            Priority = priority,
            DueDate = due,
            CreatedByUserId = createdBy,
            AssignedToUserId = assignedTo
        }, createdAt, updatedAt);

    private static Comment NewComment(string id, Guid taskId, Guid userId, string content, DateTime createdAt) =>
        Stamp(new Comment { Id = Guid.Parse(id), TaskId = taskId, UserId = userId, Content = content }, createdAt, null);

    private static Notification NewNotification(string id, Guid userId, NotificationType type, string message,
        Guid? relatedTaskId, bool isRead, DateTime createdAt) =>
        Stamp(new Notification
        {
            Id = Guid.Parse(id),
            UserId = userId,
            Type = type,
            Message = message,
            RelatedTaskId = relatedTaskId,
            IsRead = isRead
        }, createdAt, null);

    private static T Stamp<T>(T entity, DateTime createdAt, DateTime? updatedAt) where T : BaseEntity
    {
        entity.CreatedAt = createdAt;
        entity.UpdatedAt = updatedAt;
        return entity;
    }
}
