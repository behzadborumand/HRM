using HRMS.Core.Entities;
using HRMS.Core.Security;
using HRMS.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Data.Context
{
    /// <summary>Idempotent reference-data seeding so the app is usable on a freshly scripted database.</summary>
    public static class DbInitializer
    {
        private sealed record SeedEmployee(
            string Number, string FirstName, string LastName, string Email, string Phone,
            DateTime HireDate, DateTime DateOfBirth, string Gender, string MaritalStatus,
            string Department, string JobTitle, string? ManagerNumber, string EmploymentType,
            string PayType, decimal PayRate, string Status);

        private static readonly SeedEmployee[] Roster =
        {
            new("EMP-0001", "سمانه", "میرقلی", "s.mirqoli@ariatech.ir", "021-88770011", new DateTime(2018, 4, 22), new DateTime(1979, 6, 30), "Female", "Married", "عملیات", "مدیر عامل", null, "FullTime", "Salary", 120_000_000, "Active"),
            new("EMP-0002", "بهزاد", "بهرامی", "b.bahrami@ariatech.ir", "021-88770012", new DateTime(2019, 7, 6), new DateTime(1986, 3, 14), "Male", "Married", "منابع انسانی", "مدیر منابع انسانی", "EMP-0001", "FullTime", "Salary", 78_000_000, "Active"),
            new("EMP-0003", "نگار", "صادقی", "n.sadeghi@ariatech.ir", "021-88770013", new DateTime(2021, 2, 14), new DateTime(1992, 9, 2), "Female", "Single", "منابع انسانی", "کارشناس استخدام", "EMP-0002", "FullTime", "Salary", 42_000_000, "Active"),
            new("EMP-0004", "امیر", "رحیمی", "a.rahimi@ariatech.ir", "021-88770014", new DateTime(2020, 9, 19), new DateTime(1988, 1, 25), "Male", "Married", "فناوری اطلاعات", "مدیر فنی", "EMP-0001", "FullTime", "Salary", 85_000_000, "Active"),
            new("EMP-0005", "مریم", "کاظمی", "m.kazemi@ariatech.ir", "021-88770015", new DateTime(2022, 5, 8), new DateTime(1994, 5, 19), "Female", "Single", "فناوری اطلاعات", "توسعه‌دهنده ارشد", "EMP-0004", "FullTime", "Salary", 63_000_000, "Active"),
            new("EMP-0006", "پیمان", "نجفی", "p.najafi@ariatech.ir", "021-88770016", new DateTime(2023, 1, 29), new DateTime(1991, 11, 7), "Male", "Married", "فناوری اطلاعات", "طراح رابط کاربری", "EMP-0004", "FullTime", "Salary", 51_000_000, "Active"),
            new("EMP-0007", "الهام", "یوسفی", "e.yousefi@ariatech.ir", "021-88770017", new DateTime(2021, 11, 15), new DateTime(1989, 8, 3), "Female", "Married", "مالی و حسابداری", "حسابدار ارشد", "EMP-0001", "FullTime", "Salary", 55_000_000, "Active"),
            new("EMP-0008", "کامران", "اسدی", "k.asadi@ariatech.ir", "021-88770018", new DateTime(2024, 3, 4), new DateTime(1996, 2, 21), "Male", "Single", "مالی و حسابداری", "کارشناس فروش", "EMP-0007", "PartTime", "Hourly", 900_000, "Active"),
            new("EMP-0009", "شیرین", "طاهری", "s.taheri@ariatech.ir", "021-88770019", new DateTime(2022, 8, 21), new DateTime(1993, 12, 11), "Female", "Married", "بازاریابی و فروش", "کارشناس فروش", "EMP-0001", "FullTime", "Salary", 44_000_000, "Active"),
            new("EMP-0010", "وحید", "محقق", "v.mohaghegh@ariatech.ir", "021-88770020", new DateTime(2025, 6, 30), new DateTime(1995, 7, 8), "Male", "Single", "فناوری اطلاعات", "توسعه‌دهنده ارشد", "EMP-0004", "Contract", "Hourly", 1_200_000, "Active"),
            new("EMP-0011", "ترانه", "شریفی", "t.sharifi@ariatech.ir", "021-88770021", new DateTime(2024, 10, 12), new DateTime(1997, 4, 27), "Female", "Married", "منابع انسانی", "کارشناس استخدام", "EMP-0002", "FullTime", "Salary", 39_000_000, "OnLeave"),
            new("EMP-0012", "بهنام", "فرهادی", "b.farhadi@ariatech.ir", "021-88770022", new DateTime(2019, 2, 25), new DateTime(1985, 10, 16), "Male", "Married", "عملیات", "مدیر فنی", "EMP-0001", "FullTime", "Salary", 72_000_000, "Active"),
        };

        public static async Task SeedAsync(HrmsDbContext db)
        {
            var now = DateTime.Now;
            var year = now.Year;

            if (!await db.Companies.AnyAsync())
            {
                db.Companies.Add(new Company { CompanyName = "شرکت فناوری آریاتاب", CreatedDate = now });
                await db.SaveChangesAsync();
            }

            var companyId = await db.Companies.Select(c => c.CompanyId).FirstAsync();

            if (!await db.Departments.AnyAsync())
            {
                db.Departments.AddRange(
                    new Department { DepartmentName = "منابع انسانی" },
                    new Department { DepartmentName = "فناوری اطلاعات" },
                    new Department { DepartmentName = "مالی و حسابداری" },
                    new Department { DepartmentName = "بازاریابی و فروش" },
                    new Department { DepartmentName = "عملیات" });
                await db.SaveChangesAsync();
            }

            if (!await db.JobTitles.AnyAsync())
            {
                db.JobTitles.AddRange(
                    new JobTitle { TitleName = "مدیر عامل", Description = "مدیریت ارشد سازمان" },
                    new JobTitle { TitleName = "مدیر منابع انسانی", Description = "راهبری تیم منابع انسانی" },
                    new JobTitle { TitleName = "مدیر فنی", Description = "راهبری تیم توسعه" },
                    new JobTitle { TitleName = "توسعه‌دهنده ارشد", Description = "توسعه نرم‌افزار" },
                    new JobTitle { TitleName = "طراح رابط کاربری", Description = "طراحی تجربه کاربری" },
                    new JobTitle { TitleName = "حسابدار ارشد", Description = "امور مالی و حقوق" },
                    new JobTitle { TitleName = "کارشناس استخدام", Description = "جذب و انتخاب کارکنان" },
                    new JobTitle { TitleName = "کارشناس فروش", Description = "فروش و بازاریابی" });
                await db.SaveChangesAsync();
            }

            if (!await db.TimeOffPolicies.AnyAsync())
            {
                db.TimeOffPolicies.AddRange(
                    new TimeOffPolicy { PolicyName = "مرخصی استحقاقی", CompanyId = companyId, Category = "Vacation", AnnualHours = 216, Description = "۲۷ روز کاری در سال، هر روز ۸ ساعت", Active = true },
                    new TimeOffPolicy { PolicyName = "مرخصی استعلاجی", CompanyId = companyId, Category = "Sick", AnnualHours = 120, Description = "با گواهی پزشکی، سقف ۱۵ روز", Active = true },
                    new TimeOffPolicy { PolicyName = "مرخصی بدون حقوق", CompanyId = companyId, Category = "Unpaid", AnnualHours = 0, Description = "بدون سقف ساعتی، نیازمند تایید مدیر", Active = true },
                    new TimeOffPolicy { PolicyName = "مرخصی ویژه", CompanyId = companyId, Category = "Special", AnnualHours = 24, Description = "اتاق عقد، سوگواری و موارد مشابه", Active = true });
                await db.SaveChangesAsync();
            }

            if (!await db.TrainingCourses.AnyAsync())
            {
                db.TrainingCourses.AddRange(
                    new TrainingCourse { Title = "آشناسازی با سازمان", Category = "ضمن خدمت", Provider = "منابع انسانی", Description = "روز اول حضور، آیین‌نامه‌ها و ابزارهای داخلی", MandatoryForAll = true, Active = true, CreatedDate = now },
                    new TrainingCourse { Title = "ایمنی و بهداشت محیط کار", Category = "ایمنی", Provider = "شرکت ایمنی پارس", Description = "ارزیابی ریسک و واکنش در شرایط اضطراری", RenewalDays = 365, MandatoryForAll = true, Active = true, CreatedDate = now },
                    new TrainingCourse { Title = "کمک‌های اولیه", Category = "ایمنی", Provider = "هلال احمر", RenewalDays = 730, Active = true, CreatedDate = now },
                    new TrainingCourse { Title = "مقابله با آزار و اذیت در محل کار", Category = "انطباق", Provider = "منابع انسانی", RenewalDays = 365, MandatoryForAll = true, Active = true, CreatedDate = now },
                    new TrainingCourse { Title = "حفاظت اطلاعات و رمزنگاری", Category = "حراست", Provider = "فناوری اطلاعات", RenewalDays = 365, Active = true, CreatedDate = now },
                    new TrainingCourse { Title = "کار با سامانه حقوق و دستمزد", Category = "نرم‌افزار", Provider = "تیم فنی", Active = true, CreatedDate = now });
                await db.SaveChangesAsync();
            }

            if (!await db.Roles.AnyAsync())
            {
                db.Roles.AddRange(
                    new Role { RoleName = "Admin", Description = "مدیر سیستم - دسترسی کامل", Permissions = "employees.read,employees.write,employees.delete,timeoff.read,timeoff.approve,timeoff.request,org.read,org.write,users.read,users.write,reports.read" },
                    new Role { RoleName = "Manager", Description = "مدیر واحد - تایید مرخصی و مدیریت کارکنان", Permissions = "employees.read,employees.write,timeoff.read,timeoff.approve,timeoff.request,org.read,reports.read" },
                    new Role { RoleName = "Employee", Description = "کارمند - پروفایل و درخواست مرخصی", Permissions = "timeoff.read,timeoff.request" });
                await db.SaveChangesAsync();
            }

            var departments = await db.Departments.ToListAsync();
            var jobTitles = await db.JobTitles.ToListAsync();

            if (!await db.Employees.AnyAsync())
            {
                var employees = Roster.Select(seed => new Employee
                {
                    EmployeeNumber = seed.Number,
                    FirstName = seed.FirstName,
                    LastName = seed.LastName,
                    Email = seed.Email,
                    Phone = seed.Phone,
                    HireDate = seed.HireDate,
                    DateOfBirth = seed.DateOfBirth,
                    Gender = seed.Gender,
                    MaritalStatus = seed.MaritalStatus,
                    EmploymentStatus = seed.Status,
                    CompanyId = companyId,
                    CreatedDate = seed.HireDate,
                    ModifiedDate = seed.HireDate
                }).ToList();

                db.Employees.AddRange(employees);
                await db.SaveChangesAsync();

                foreach (var seed in Roster)
                {
                    var employee = employees.First(e => e.EmployeeNumber == seed.Number);
                    db.EmployeeJobs.Add(new EmployeeJob
                    {
                        EmployeeId = employee.EmployeeId,
                        DepartmentId = departments.First(d => d.DepartmentName == seed.Department).DepartmentId,
                        JobTitleId = jobTitles.First(t => t.TitleName == seed.JobTitle).JobTitleId,
                        ManagerId = seed.ManagerNumber is null
                            ? null
                            : employees.FirstOrDefault(e => e.EmployeeNumber == seed.ManagerNumber)?.EmployeeId,
                        EmploymentType = seed.EmploymentType,
                        PayType = seed.PayType,
                        PayRate = seed.PayRate,
                        StartDate = seed.HireDate,
                        IsCurrent = true
                    });
                    db.EmployeePersonalInfos.Add(new EmployeePersonalInfo
                    {
                        EmployeeId = employee.EmployeeId,
                        AddressLine1 = "تهران، خیابان ولیعصر، پلاک ۱۲۳",
                        City = "تهران",
                        State = "تهران",
                        Country = "ایران",
                        PostalCode = "15117" + employee.EmployeeId.ToString("000"),
                        MobilePhone = seed.Phone,
                        EmergencyContactName = "خانواده " + seed.LastName,
                        EmergencyContactPhone = seed.Phone,
                        EmergencyContactRelation = "همسر"
                    });
                }

                await db.SaveChangesAsync();
            }

            var allEmployees = await db.Employees.ToListAsync();
            var limitedPolicies = await db.TimeOffPolicies.Where(p => p.AnnualHours > 0).ToListAsync();

            if (!await db.TimeOffBalances.AnyAsync())
            {
                foreach (var employee in allEmployees)
                {
                    foreach (var policy in limitedPolicies)
                    {
                        db.TimeOffBalances.Add(new TimeOffBalance
                        {
                            EmployeeId = employee.EmployeeId,
                            PolicyId = policy.PolicyId,
                            Year = year,
                            AccruedHours = TimeOffRules.ProrateAccrual(employee.HireDate, year, policy.AnnualHours),
                            UsedHours = 0,
                            LastUpdated = now
                        });
                    }
                }

                await db.SaveChangesAsync();
            }

            if (!await db.TimeOffRequests.AnyAsync())
            {
                var policies = await db.TimeOffPolicies.ToListAsync();
                var currentJobs = await db.EmployeeJobs.Where(j => j.IsCurrent).ToListAsync();

                var samples = new (string Number, int OffsetDays, int LengthDays, string Status, string Category)[]
                {
                    ("EMP-0005", -6, 3, "Pending", "Vacation"),
                    ("EMP-0006", -2, 1, "Pending", "Special"),
                    ("EMP-0010", -10, 5, "Pending", "Vacation"),
                    ("EMP-0003", 12, 2, "Approved", "Vacation"),
                    ("EMP-0007", 26, 4, "Approved", "Sick"),
                    ("EMP-0009", 40, 1, "Rejected", "Unpaid"),
                    ("EMP-0012", 60, 2, "Approved", "Vacation"),
                };

                foreach (var sample in samples)
                {
                    var employee = allEmployees.First(e => e.EmployeeNumber == sample.Number);
                    var policy = policies.First(p => p.Category == sample.Category);
                    var start = now.Date.AddDays(sample.OffsetDays);
                    var end = start.AddDays(sample.LengthDays - 1);
                    var hours = TimeOffRules.WorkingHours(start, end);
                    var managerId = currentJobs.FirstOrDefault(j => j.EmployeeId == employee.EmployeeId)?.ManagerId;

                    db.TimeOffRequests.Add(new TimeOffRequest
                    {
                        EmployeeId = employee.EmployeeId,
                        PolicyId = policy.PolicyId,
                        StartDate = start,
                        EndDate = end,
                        TotalHours = hours,
                        Reason = sample.Category switch
                        {
                            "Sick" => "کسر و مراجعه به پزشک",
                            "Special" => "مراسم خانوادگی",
                            "Unpaid" => "مسافرت خارج از کشور",
                            _ => "استراحت و سفر کوتاه"
                        },
                        Status = sample.Status,
                        RequestedBy = employee.EmployeeId,
                        ApprovedBy = sample.Status == "Pending" ? null : managerId,
                        ApprovedDate = sample.Status == "Pending" ? null : start.AddDays(-1),
                        Comments = sample.Status == "Rejected" ? "تداخل با تحویل پروژه" : null,
                        RequestedDate = start.AddDays(-3)
                    });

                    if (sample.Status == "Approved")
                    {
                        var balance = await db.TimeOffBalances.FirstOrDefaultAsync(b =>
                            b.EmployeeId == employee.EmployeeId && b.PolicyId == policy.PolicyId && b.Year == start.Year);
                        if (balance is not null)
                        {
                            balance.UsedHours += hours;
                            balance.LastUpdated = now;
                        }
                    }
                }

                await db.SaveChangesAsync();
            }

            var trainingCourses = await db.TrainingCourses.ToListAsync();
            var hrEmployeeId = allEmployees.First(e => e.EmployeeNumber == "EMP-0002").EmployeeId;

            if (!await db.TrainingAssignments.AnyAsync())
            {
                // Offsets are days relative to today so the sample always shows one overdue, one upcoming and one renewed item.
                var samples = new (string Number, string Title, int AssignedOffset, int? DueOffset, int? CompletedOffset, string? Certificate, string? Note)[]
                {
                    ("EMP-0005", "آشناسازی با سازمان", -120, -106, -110, "CERT-1024", "حضور در جلسه آشناسازی"),
                    ("EMP-0005", "حفاظت اطلاعات و رمزنگاری", -40, -5, null, null, "منتظر هماهنگی کلاس"),
                    ("EMP-0002", "مقابله با آزار و اذیت در محل کار", -60, 305, -30, "CERT-0917", null),
                    ("EMP-0004", "ایمنی و بهداشت محیط کار", 0, 30, null, null, null),
                    ("EMP-0010", "آشناسازی با سازمان", 0, null, null, null, "بدون سررسید"),
                };

                foreach (var sample in samples)
                {
                    var employee = allEmployees.First(e => e.EmployeeNumber == sample.Number);
                    var course = trainingCourses.First(c => c.Title == sample.Title);

                    db.TrainingAssignments.Add(new TrainingAssignment
                    {
                        EmployeeId = employee.EmployeeId,
                        CourseId = course.CourseId,
                        AssignedDate = now.Date.AddDays(sample.AssignedOffset),
                        DueDate = sample.DueOffset is null ? null : now.Date.AddDays(sample.DueOffset.Value),
                        CompletedDate = sample.CompletedOffset is null ? null : now.Date.AddDays(sample.CompletedOffset.Value),
                        CertificateNumber = sample.Certificate,
                        Notes = sample.Note,
                        AssignedBy = hrEmployeeId
                    });
                }

                await db.SaveChangesAsync();
            }

            if (!await db.Users.AnyAsync())
            {
                var roles = await db.Roles.ToListAsync();

                db.Users.AddRange(
                    new User
                    {
                        EmployeeId = allEmployees.First(e => e.EmployeeNumber == "EMP-0001").EmployeeId,
                        Username = "admin",
                        PasswordHash = PasswordHasher.Hash("Admin@1234"),
                        RoleId = roles.First(r => r.RoleName == "Admin").RoleId,
                        IsActive = true,
                        CreatedDate = now
                    },
                    new User
                    {
                        EmployeeId = allEmployees.First(e => e.EmployeeNumber == "EMP-0002").EmployeeId,
                        Username = "hr.manager",
                        PasswordHash = PasswordHasher.Hash("Manager@1234"),
                        RoleId = roles.First(r => r.RoleName == "Manager").RoleId,
                        IsActive = true,
                        CreatedDate = now
                    },
                    new User
                    {
                        EmployeeId = allEmployees.First(e => e.EmployeeNumber == "EMP-0005").EmployeeId,
                        Username = "m.kazemi",
                        PasswordHash = PasswordHasher.Hash("User@1234"),
                        RoleId = roles.First(r => r.RoleName == "Employee").RoleId,
                        IsActive = true,
                        CreatedDate = now
                    });

                await db.SaveChangesAsync();
            }
        }
    }
}
