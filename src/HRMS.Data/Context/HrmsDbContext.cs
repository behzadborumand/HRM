using HRMS.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Data.Context
{
    public class HrmsDbContext : DbContext
    {
        public HrmsDbContext(DbContextOptions<HrmsDbContext> options) : base(options)
        {
        }

        public DbSet<Company> Companies { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<EmployeePersonalInfo> EmployeePersonalInfos { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<JobTitle> JobTitles { get; set; }
        public DbSet<EmployeeJob> EmployeeJobs { get; set; }
        public DbSet<TimeOffPolicy> TimeOffPolicies { get; set; }
        public DbSet<TimeOffBalance> TimeOffBalances { get; set; }
        public DbSet<TimeOffRequest> TimeOffRequests { get; set; }
        public DbSet<EmployeeDocument> EmployeeDocuments { get; set; }
        public DbSet<TrainingCourse> TrainingCourses { get; set; }
        public DbSet<TrainingAssignment> TrainingAssignments { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<OrganizationLink> OrganizationLinks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Employee>().ToTable("Employees");
            modelBuilder.Entity<Company>().ToTable("Companies");
            modelBuilder.Entity<Department>().ToTable("Departments");
            modelBuilder.Entity<JobTitle>().ToTable("JobTitles");
            modelBuilder.Entity<EmployeeJob>().ToTable("EmployeeJobs");
            modelBuilder.Entity<TimeOffPolicy>().ToTable("TimeOffPolicies");
            modelBuilder.Entity<TimeOffBalance>().ToTable("TimeOffBalances");
            modelBuilder.Entity<TimeOffRequest>().ToTable("TimeOffRequests");
            modelBuilder.Entity<EmployeeDocument>().ToTable("EmployeeDocuments");
            modelBuilder.Entity<Role>().ToTable("Roles");
            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<EmployeePersonalInfo>().ToTable("EmployeePersonalInfo");

            // Primary keys whose names do not follow the <Entity>Id convention are declared explicitly.
            modelBuilder.Entity<EmployeePersonalInfo>().HasKey(p => p.PersonalInfoId);
            modelBuilder.Entity<TimeOffPolicy>().HasKey(p => p.PolicyId);
            modelBuilder.Entity<TimeOffBalance>().HasKey(b => b.BalanceId);
            modelBuilder.Entity<TimeOffRequest>().HasKey(r => r.RequestId);
            modelBuilder.Entity<EmployeeDocument>().HasKey(d => d.DocumentId);
            modelBuilder.Entity<TrainingCourse>().HasKey(c => c.CourseId);
            modelBuilder.Entity<TrainingAssignment>().HasKey(a => a.AssignmentId);
            modelBuilder.Entity<OrganizationLink>().ToTable("OrganizationLinks").HasKey(l => l.LinkId);
            modelBuilder.Entity<OrganizationLink>().Property(l => l.Title).HasMaxLength(100);
            modelBuilder.Entity<OrganizationLink>().Property(l => l.Url).HasMaxLength(2000);
            modelBuilder.Entity<OrganizationLink>().Property(l => l.Description).HasMaxLength(300);

            modelBuilder.Entity<TimeOffBalance>().HasIndex(b => new { b.EmployeeId, b.PolicyId, b.Year }).IsUnique();
            modelBuilder.Entity<EmployeeJob>().HasIndex(j => new { j.EmployeeId, j.IsCurrent });
            modelBuilder.Entity<EmployeeDocument>().HasIndex(d => new { d.EmployeeId, d.UploadDate });
            modelBuilder.Entity<TrainingAssignment>().HasIndex(a => new { a.EmployeeId, a.DueDate });
            modelBuilder.Entity<TrainingAssignment>().HasIndex(a => a.CourseId);
        }
    }
}
