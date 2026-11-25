using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public class TaskItem : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public DateTime? DueDate { get; set; }

    /// <summary>Place in its status column on the board, 0 = top. Only the relative order matters (gaps are fine).</summary>
    public int Position { get; set; }

    public Guid CreatedByUserId { get; set; }
    public Guid? AssignedToUserId { get; set; }

    // Navigation properties
    public User CreatedBy { get; set; } = null!;
    public User? AssignedTo { get; set; }
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();

    // ---- Permission rules (one place for every task handler) ----

    /// <summary>Edit every field and (re)assign: the creator, any Manager or Admin.</summary>
    public bool CanBeEditedBy(Guid userId, UserRole role) =>
        CreatedByUserId == userId || role is UserRole.Manager or UserRole.Admin;

    /// <summary>Change the status: everyone who may edit, plus the assignee.</summary>
    public bool CanChangeStatusBy(Guid userId, UserRole role) =>
        CanBeEditedBy(userId, role) || AssignedToUserId == userId;

    /// <summary>Delete: the creator or an Admin.</summary>
    public bool CanBeDeletedBy(Guid userId, UserRole role) =>
        CreatedByUserId == userId || role == UserRole.Admin;

    // ---- State changes ----

    public void UpdateDetails(string title, string description, TaskPriority priority, DateTime? dueDate)
    {
        Title = title.Trim();
        Description = description;
        Priority = priority;
        DueDate = dueDate;
    }

    /// <summary>Puts the task in the <paramref name="status"/> column at <paramref name="position"/>.</summary>
    public void MoveTo(TaskItemStatus status, int position)
    {
        Status = status;
        Position = position;
    }

    /// <summary>Assigns the task to <paramref name="userId"/>, or unassigns it when null.</summary>
    public void AssignTo(Guid? userId) => AssignedToUserId = userId;
}
