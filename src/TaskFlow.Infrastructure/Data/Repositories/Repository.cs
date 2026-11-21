using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Interfaces;
using TaskFlow.Infrastructure.Data.Context;

namespace TaskFlow.Infrastructure.Data.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly ApplicationDbContext _context;
    protected readonly DbSet<T> _dbSet;


    public Repository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync([id], cancellationToken);
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
        return entity;
    }

    public virtual Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Remove(entity);
        return Task.CompletedTask;
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.CountAsync(predicate, cancellationToken);
    }

    public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(predicate, cancellationToken);
    }

    public virtual async Task<IReadOnlyDictionary<TKey, int>> CountByAsync<TKey>(
        Expression<Func<T, bool>>? filter,
        Expression<Func<T, TKey>> keySelector,
        CancellationToken cancellationToken = default) where TKey : notnull
    {
        IQueryable<T> query = _dbSet.AsNoTracking();
        if (filter is not null)
            query = query.Where(filter);

        return await query
            .GroupBy(keySelector)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
    }

    public virtual async Task<IReadOnlyList<TResult>> ListAsync<TResult>(
        Expression<Func<T, bool>>? filter,
        Expression<Func<T, TResult>> selector,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _dbSet.AsNoTracking();
        if (filter is not null)
            query = query.Where(filter);

        return await query.Select(selector).ToListAsync(cancellationToken);
    }

    public virtual async Task<IReadOnlyList<TResult>> ListAsync<TResult, TKey>(
        Expression<Func<T, bool>>? filter,
        Expression<Func<T, TKey>> orderBy,
        bool descending,
        int skip,
        int take,
        Expression<Func<T, TResult>> selector,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _dbSet.AsNoTracking();
        if (filter is not null)
            query = query.Where(filter);

        query = descending ? query.OrderByDescending(orderBy) : query.OrderBy(orderBy);

        return await query.Skip(skip).Take(take).Select(selector).ToListAsync(cancellationToken);
    }

    public virtual async Task<IReadOnlyList<TResult>> ListAsync<TResult>(
        Expression<Func<T, bool>>? filter,
        Func<IQueryable<T>, IOrderedQueryable<T>> orderBy,
        int skip,
        int take,
        Expression<Func<T, TResult>> selector,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _dbSet.AsNoTracking();
        if (filter is not null)
            query = query.Where(filter);

        return await orderBy(query).Skip(skip).Take(take).Select(selector).ToListAsync(cancellationToken);
    }
}
