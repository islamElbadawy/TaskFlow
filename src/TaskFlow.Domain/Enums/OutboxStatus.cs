namespace TaskFlow.Domain.Enums;

public enum OutboxStatus
{
    Pending,
    Processing,
    Sent,
    DeadLetter
}
