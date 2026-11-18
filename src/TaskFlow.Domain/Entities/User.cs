using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public class User : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsActive { get; set; } = true;

    /// <summary>Copied into every access token. Changing it (see <see cref="RevokeTokens"/>) makes every token issued
    /// before the change invalid, so a password or role change takes effect immediately.</summary>
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();

    /// <summary>Invalidates every access token this user holds; they must log in again.</summary>
    public void RevokeTokens() => SecurityStamp = Guid.NewGuid();

    public ICollection<TaskItem> CreatedTasks { get; set; } = new List<TaskItem>();
    public ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();


    public string FullName => $"{FirstName} {LastName}";
}