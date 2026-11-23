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

    /// <summary>
    /// Runs <paramref name="work"/> in a database transaction that is retried as a whole on transient failures.
    /// <paramref name="work"/> must be safe to re-run and should call <see cref="SaveChangesAsync"/> itself;
    /// the transaction is committed after it returns and rolled back if it throws.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken = default);
}
