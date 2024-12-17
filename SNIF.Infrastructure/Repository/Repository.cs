using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SNIF.Core.Specifications;
using SNIF.Infrastructure.Data;

namespace SNIF.Infrastructure.Repository
{

    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly SNIFContext _context;
        private readonly DbSet<T> _dbSet;

        public Repository(SNIFContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public virtual async Task<T?> GetByIdAsync(object id)
        {
            return await _dbSet.FindAsync(id);
        }

        public virtual async Task<IReadOnlyList<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        public virtual async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(predicate).ToListAsync();
        }

        public virtual async Task<T> AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public virtual async Task UpdateAsync(T entity)
        {
            _context.Entry(entity).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        public virtual async Task DeleteAsync(T entity)
        {
            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
        }

        public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.AnyAsync(predicate);
        }

        public virtual async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.CountAsync(predicate);
        }

        private IQueryable<T> ApplySpecification(IQuerySpecification<T> spec)
        {
            var query = _dbSet.AsQueryable();

            if (spec.Criteria != null)
                query = query.Where(spec.Criteria);

            query = spec.Includes.Aggregate(query, (current, include) => current.Include(include));
            query = spec.IncludeStrings.Aggregate(query, (current, include) => current.Include(include));

            return query;
        }

        public async Task<T?> GetBySpecificationAsync(IQuerySpecification<T> specification)
        {
            return await ApplySpecification(specification).FirstOrDefaultAsync();
        }

        public async Task<IReadOnlyList<T>> FindBySpecificationAsync(IQuerySpecification<T> specification)
        {
            return await ApplySpecification(specification).ToListAsync();
        }
    }

}



