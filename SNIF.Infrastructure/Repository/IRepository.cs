
using System.Linq.Expressions;
using SNIF.Core.Specifications;

namespace SNIF.Infrastructure.Repository
{
    public interface IRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(object id);
        Task<IReadOnlyList<T>> GetAllAsync();
        Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate);
        Task<T> AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(T entity);
        Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);
        Task<int> CountAsync(Expression<Func<T, bool>> predicate);
        Task<T?> GetBySpecificationAsync(IQuerySpecification<T> specification);
        Task<IReadOnlyList<T>> FindBySpecificationAsync(IQuerySpecification<T> specification);
    }
}