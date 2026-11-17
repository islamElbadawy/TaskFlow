using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public class Comment : BaseEntity
{
    public string Content { get; set; } = string.Empty;

    // Foreign Keys
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }

    // Navigation properties
    public TaskItem Task { get; set; } = null!;
    public User User { get; set; } = null!;

    /// <summary>Only the author may edit a comment.</summary>
    public bool CanBeEditedBy(Guid userId) => UserId == userId;

    /// <summary>The author or an Admin may delete a comment.</summary>
    public bool CanBeDeletedBy(Guid userId, UserRole role) => UserId == userId || role == UserRole.Admin;

    public void Edit(string content) => Content = content.Trim();
}
