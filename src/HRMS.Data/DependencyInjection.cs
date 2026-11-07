using HRMS.Core.Repositories;
using HRMS.Data.Context;
using HRMS.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddHrmsData(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<HrmsDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("HRMSDatabase")));

            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            services.AddScoped<IEmployeeRepository, EmployeeRepository>();
            services.AddScoped<ITimeOffRepository, TimeOffRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IReportingRepository, ReportingRepository>();
            services.AddScoped<ITrainingRepository, TrainingRepository>();
            services.AddScoped<IDashboardRepository, DashboardRepository>();

            return services;
        }
    }
}
