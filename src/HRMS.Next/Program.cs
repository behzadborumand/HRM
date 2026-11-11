using HRMS.Next;
using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using HRMS.Next.Modules.Leave;
using HRMS.Next.Modules.Training;
using HRMS.Next.Modules.Documents;
using HRMS.Next.Modules.Reporting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("HrmsNext");
if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("ConnectionStrings:HrmsNext must name a separate destination database.");

builder.Services.AddRazorPages();
builder.Services.AddDbContext<AccessDbContext>(o => o.UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "access")));
builder.Services.AddDbContext<OrganizationDbContext>(o => o.UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "organization")));
builder.Services.AddDbContext<PersonnelDbContext>(o => o.UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "personnel")));
builder.Services.AddDbContext<LeaveDbContext>(o => o.UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "leave")));
builder.Services.AddDbContext<TrainingDbContext>(o => o.UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "training")));
builder.Services.AddDbContext<DocumentDbContext>(o => o.UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "documents")));
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddScoped<AccessService>();
builder.Services.AddScoped<AccountAdministrationService>();
builder.Services.AddScoped<IOrganizationDirectory, OrganizationDirectory>();
builder.Services.AddScoped<JobTitleService>();
builder.Services.AddScoped<DepartmentService>();
builder.Services.AddScoped<OrganizationLinkService>();
builder.Services.AddScoped<ReportingService>();
builder.Services.AddScoped<HomeHighlightsService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<EmployeeJobService>();
builder.Services.AddScoped<IEmployeeDirectory, PersonnelDirectory>();
builder.Services.AddScoped<LeaveService>();
builder.Services.AddScoped<TrainingService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.LoginPath = "/Login";
    o.AccessDeniedPath = "/Denied";
    o.Cookie.Name = "HRMS.Next.Auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.Events.OnValidatePrincipal = async context =>
    {
        var service = context.HttpContext.RequestServices.GetRequiredService<AccessService>();
        if (context.Principal is null || await service.GetActiveAsync(context.Principal, context.HttpContext.RequestAborted) is null)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();

var app = builder.Build();
if (args.Contains("--bootstrap", StringComparer.Ordinal))
{
    await Bootstrap.RunAsync(app.Services);
    return;
}
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapHealthChecks("/health/live");
app.Run();
