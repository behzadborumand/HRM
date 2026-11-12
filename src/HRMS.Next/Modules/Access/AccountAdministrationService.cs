using HRMS.Next.Modules.Personnel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Access;

public sealed record AccountCreateInput(string Username, string Password, int? EmployeeId,
    bool CanReadEmployees, bool CanWriteEmployees, int? RoleId = null);
public sealed record AccountUpdateInput(int? EmployeeId, bool CanReadEmployees,
    bool CanWriteEmployees, bool IsActive, int? RoleId = null);
public sealed record AccountRow(int Id, string Username, int? EmployeeId, bool IsAdmin,
    bool CanReadEmployees, bool CanWriteEmployees, bool IsActive, byte[] RowVersion, int? RoleId = null, string? RoleName = null);
public sealed record AccountPage(IReadOnlyList<AccountRow> Rows, int Total, int Page, int PageSize);

public sealed class AccountAdministrationService(AccessDbContext db, IEmployeeDirectory employees,
    IPasswordHasher<AppUser> hasher, TimeProvider clock)
{
    private const int PageSize = 50;

    public async Task<AccountPage> ListAsync(ActorScope actor, int page, CancellationToken ct)
    {
        RequireAdmin(actor);
        var query = db.Users.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId);
        var total = await query.CountAsync(ct);
        page = Math.Clamp(page, 1, Math.Max(1, (total + PageSize - 1) / PageSize));
        var rows = await query.OrderBy(x => x.Username).ThenBy(x => x.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(x => new AccountRow(x.Id, x.Username, x.EmployeeId, x.IsAdmin,
                x.CanReadEmployees, x.CanWriteEmployees, x.IsActive, x.RowVersion, x.RoleId, db.Roles.Where(r => r.Id == x.RoleId && r.CompanyId == actor.CompanyId).Select(r => r.Name).FirstOrDefault())).ToListAsync(ct);
        return new AccountPage(rows, total, page, PageSize);
    }

    public async Task<int> CreateAsync(ActorScope actor, AccountCreateInput input, CancellationToken ct)
    {
        RequireAdmin(actor);
        var username = ValidateUsername(input.Username);
        ValidatePassword(input.Password);
        await ValidateEmployeeAsync(actor.CompanyId, input.EmployeeId, ct);
        await ValidateRoleAsync(actor.CompanyId, input.RoleId, input.EmployeeId, null, ct);
        if (await db.Users.AnyAsync(x => x.Username == username, ct))
            throw new ArgumentException("نام کاربری تکراری است.");
        if (input.EmployeeId is { } employeeId &&
            await db.Users.AnyAsync(x => x.CompanyId == actor.CompanyId && x.EmployeeId == employeeId, ct))
            throw new ArgumentException("برای این کارمند حساب کاربری ثبت شده است.");
        var user = new AppUser
        {
            Username = username, CompanyId = actor.CompanyId, EmployeeId = input.EmployeeId,
            IsActive = true, IsAdmin = false, CanReadEmployees = input.CanReadEmployees || input.CanWriteEmployees,
            CanWriteEmployees = input.CanWriteEmployees, RoleId = input.RoleId
        };
        user.PasswordHash = hasher.HashPassword(user, input.Password);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        AddAudit(actor, user.Id, "Create", $"RoleId={input.RoleId?.ToString() ?? "-"};EmployeeId={input.EmployeeId?.ToString() ?? "-"}");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return user.Id;
    }

    public async Task UpdateAsync(ActorScope actor, int id, AccountUpdateInput input,
        byte[] expectedVersion, CancellationToken ct)
    {
        RequireAdmin(actor);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == actor.CompanyId, ct)
            ?? throw new KeyNotFoundException("حساب کاربری یافت نشد.");
        if (user.IsAdmin) throw new ArgumentException("ویرایش حساب مدیر از این صفحه مجاز نیست.");
        if (!user.RowVersion.SequenceEqual(expectedVersion))
            throw new DbUpdateConcurrencyException("حساب کاربری توسط کاربر دیگری تغییر کرده است.");
        await ValidateEmployeeAsync(actor.CompanyId, input.EmployeeId, ct);
        await ValidateRoleAsync(actor.CompanyId, input.RoleId, input.EmployeeId, user.RoleId, ct);
        if (input.EmployeeId is { } employeeId &&
            await db.Users.AnyAsync(x => x.CompanyId == actor.CompanyId && x.EmployeeId == employeeId && x.Id != id, ct))
            throw new ArgumentException("برای این کارمند حساب کاربری ثبت شده است.");
        user.EmployeeId = input.EmployeeId;
        user.RoleId = input.RoleId;
        user.CanReadEmployees = input.CanReadEmployees || input.CanWriteEmployees;
        user.CanWriteEmployees = input.CanWriteEmployees;
        user.IsActive = input.IsActive;
        AddAudit(actor, id, "Update", $"RoleId={input.RoleId?.ToString() ?? "-"};EmployeeId={input.EmployeeId?.ToString() ?? "-"};Active={input.IsActive};Read={user.CanReadEmployees};Write={user.CanWriteEmployees}");
        await db.SaveChangesAsync(ct);
    }

    public async Task ResetPasswordAsync(ActorScope actor, int id, string password, CancellationToken ct)
    {
        RequireAdmin(actor);
        ValidatePassword(password);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == actor.CompanyId, ct)
            ?? throw new KeyNotFoundException("حساب کاربری یافت نشد.");
        if (user.IsAdmin) throw new ArgumentException("تغییر رمز مدیر از این صفحه مجاز نیست.");
        user.PasswordHash = hasher.HashPassword(user, password);
        AddAudit(actor, id, "ResetPassword", "Password changed");
        await db.SaveChangesAsync(ct);
    }

    private async Task ValidateRoleAsync(int companyId, int? roleId, int? employeeId, int? currentRoleId, CancellationToken ct)
    {
        if (roleId is null) return;
        var role = await db.Roles.AsNoTracking().SingleOrDefaultAsync(r => r.Id == roleId && r.CompanyId == companyId, ct);
        if (role is null || (!role.Active && roleId != currentRoleId))
            throw new ArgumentException("نقش فعال در شرکت شما یافت نشد.");
        if (role.Active && role.CanManageTeam && employeeId is not > 0)
            throw new ArgumentException("نقش سرپرست به اتصال حساب به پروندهٔ کارمند نیاز دارد.");
    }
    private async Task ValidateEmployeeAsync(int companyId, int? employeeId, CancellationToken ct)
    {
        if (employeeId is null) return;
        if (employeeId <= 0 || await employees.GetAsync(companyId, employeeId.Value, ct) is null)
            throw new ArgumentException("پروندهٔ کارمند در شرکت شما یافت نشد.");
    }

    private void AddAudit(ActorScope actor, int targetId, string action, string summary) =>
        db.Audits.Add(new AccessAudit { CompanyId = actor.CompanyId, TargetUserId = targetId,
            ActorUserId = actor.Id, AtUtc = clock.GetUtcNow(), Action = action, Summary = summary });

    private static string ValidateUsername(string? username)
    {
        var value = username?.Trim() ?? "";
        if (value.Length is < 3 or > 100 || value.Any(char.IsControl))
            throw new ArgumentException("نام کاربری باید بین ۳ تا ۱۰۰ نویسه و بدون نویسهٔ کنترلی باشد.");
        return value;
    }

    private static void ValidatePassword(string? password)
    {
        if (password is null || password.Length is < 12 or > 128)
            throw new ArgumentException("رمز عبور باید بین ۱۲ تا ۱۲۸ نویسه باشد.");
    }

    private static void RequireAdmin(ActorScope actor)
    {
        if (actor is not { IsActive: true, IsAdmin: true } || actor.CompanyId <= 0)
            throw new UnauthorizedAccessException();
    }
}
