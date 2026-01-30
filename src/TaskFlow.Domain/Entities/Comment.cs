using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

public class Comment : BaseEntity
{
    public string Content { get; set; } = string.Empty;
    
    // Foreign Keys
    public int TaskId { get; set; }
    public int UserId { get; set; }
    
    // Navigation properties
    public TaskItem Task { get; set; } = null!;
    public User User { get; set; } = null!;
}