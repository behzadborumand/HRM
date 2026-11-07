using HRMS.Data.Context;
using HRMS.Web.ModelBinding;
using HRMS.Web.Security;
using HRMS.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

// The host OS culture (fa-IR on this machine) parses ISO form dates with the Persian calendar,
// so 2026-06-22 would become year 2647. Dates are parsed by the Jalali binder instead.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
    options.ModelBinderProviders.Insert(0, new JalaliDateModelBinderProvider()));
builder.Services.AddHrmsData(builder.Configuration);
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<TimeOffService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<EmployeePhotoService>();
builder.Services.AddScoped<TrainingService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "HRMS_BHR.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization(options =>
{
    foreach (var permission in Permissions.All)
    {
        options.AddPolicy(Permissions.Policy(permission), policy => policy.RequireAssertion(context =>
            context.User.IsInRole("Admin") || context.User.HasClaim(HrmsClaims.Permission, permission)));
    }
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await DbInitializer.SeedAsync(scope.ServiceProvider.GetRequiredService<HrmsDbContext>());
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
