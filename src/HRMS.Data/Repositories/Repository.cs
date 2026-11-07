using HRMS.Core.Repositories;
using HRMS.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Data.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly HrmsDbContext Db;
        protected readonly DbSet<T> Set;

        public Repository(HrmsDbContext db)
        {
            Db = db;
            Set = db.Set<T>();
        }

        public IQueryable<T> Query() => Set.AsQueryable();

        public async Task<T?> FindAsync(object? id) => id is null ? null : await Set.FindAsync(id);

        public async Task<List<T>> ListAsync() => await Set.ToListAsync();

        public async Task AddAsync(T entity) => await Set.AddAsync(entity);

        public void Update(T entity) => Set.Update(entity);

        public void Remove(T entity) => Set.Remove(entity);

        public async Task<bool> AnyAsync() => await Set.AnyAsync();

        public async Task<int> CountAsync() => await Set.CountAsync();

        public async Task<int> SaveChangesAsync() => await Db.SaveChangesAsync();
    }
}
