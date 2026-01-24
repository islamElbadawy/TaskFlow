using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public class Notification : BaseEntity
{
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; } =  false;
    
    
    public int UserId { get; set; }

    public User User { get; set; } = null!;
}