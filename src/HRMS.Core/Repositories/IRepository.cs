using HRMS.Core.Paging;

namespace HRMS.Core.Repositories
{
    public interface IRepository<T> where T : class
    {
        IQueryable<T> Query();
        Task<T?> FindAsync(object? id);
        Task<List<T>> ListAsync();
        Task AddAsync(T entity);
        void Update(T entity);
        void Remove(T entity);
        Task<bool> AnyAsync();
        Task<int> CountAsync();
        Task<int> SaveChangesAsync();
    }
}
