using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Interfaces;
using TaskFlow.Infrastructure.Data.Context;

namespace TaskFlow.Infrastructure.Data.Repositories;

// The DbContext is scoped and owned by the DI container, so the unit of work never disposes it.
public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    public IRepository<User> Users { get; }
    public IRepository<TaskItem> TasksItems { get; }
    public IRepository<Comment> Comments { get; }
    public IRepository<Notification> Notifications { get; }
    public IRepository<OutboxMessage> OutboxMessages { get; }

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
        Users = new Repository<User>(_context);
        TasksItems = new Repository<TaskItem>(_context);
        Comments = new Repository<Comment>(_context);
        Notifications = new Repository<Notification>(_context);
        OutboxMessages = new Repository<OutboxMessage>(_context);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
