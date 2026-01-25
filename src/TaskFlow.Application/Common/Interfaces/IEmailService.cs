namespace TaskFlow.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendTaskAssignedEmailAsync(string toEmail, string taskTitle, string assignedBy);
    Task SendTaskUpdatedEmailAsync(string toEmail, string taskTitle, string updatedBy);
    Task SendDeadlineReminderEmailAsync(string toEmail, string taskTitle, DateTime dueDate);
}