using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public class TaskItem : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public DateTime DueDate { get; set; }

    public int CreatedByUserId { get; set; }
    public int AssignedToUserId { get; set; }
    
    // Navigation properties
    public User CreatedBy { get; set; } = null!;
    public User? AssignedTo { get; set; }
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public User CreatedByUser { get; set; } = null!;
    public User AssignedToUser { get; set; } = null!;
}