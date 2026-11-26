using TaskFlow.Domain.Entities;

namespace TaskFlow.Domain.Interfaces;

// Not IDisposable: the DbContext behind it is owned and disposed by the DI container.
public interface IUnitOfWork
{
    IRepository<User> Users { get; }
    IRepository<TaskItem> TasksItems { get; }
    IRepository<Comment> Comments { get; }
    IRepository<Notification> Notifications { get; }
    IRepository<OutboxMessage> OutboxMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
