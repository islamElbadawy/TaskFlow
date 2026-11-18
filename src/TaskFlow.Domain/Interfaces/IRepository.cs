using System.Linq.Expressions;

namespace TaskFlow.Domain.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Row counts per key in one <c>GROUP BY</c> query, e.g. tasks per status. Keys without rows are absent.
    /// </summary>
    Task<IReadOnlyDictionary<TKey, int>> CountByAsync<TKey>(
        Expression<Func<T, bool>>? filter,
        Expression<Func<T, TKey>> keySelector,
        CancellationToken cancellationToken = default) where TKey : notnull;

    /// <summary>
    /// Read-only, projected query (no change tracking). Use it for reads that need only some columns
    /// or data from related entities, e.g. <c>t =&gt; new { t.Title, CreatedByName = t.CreatedBy.FirstName }</c>.
    /// </summary>
    Task<IReadOnlyList<TResult>> ListAsync<TResult>(
        Expression<Func<T, bool>>? filter,
        Expression<Func<T, TResult>> selector,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of a read-only, projected query: filtered, sorted by <paramref name="orderBy"/>, then
    /// <paramref name="skip"/>/<paramref name="take"/> applied in the database.
    /// </summary>
    Task<IReadOnlyList<TResult>> ListAsync<TResult, TKey>(
        Expression<Func<T, bool>>? filter,
        Expression<Func<T, TKey>> orderBy,
        bool descending,
        int skip,
        int take,
        Expression<Func<T, TResult>> selector,
        CancellationToken cancellationToken = default);
}
