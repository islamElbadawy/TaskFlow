using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Interfaces;
using TaskFlow.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore.Storage;

namespace TaskFlow.Infrastructure.Data.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private IDbContextTransaction? _transaction;
    public IRepository<User> Users { get; }
    public IRepository<TaskItem> TasksItems { get; }
    public IRepository<Comment> Comments { get; }
    public IRepository<Notification> Notifications { get; }

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
        Users = new Repository<User>(_context);
        TasksItems = new Repository<TaskItem>(_context);
        Comments = new Repository<Comment>(_context);
        Notifications = new Repository<Notification>(_context);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            if (_transaction != null)
            {
                await _transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}