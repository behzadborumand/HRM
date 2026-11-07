using System.Security.Claims;

namespace HRMS.Web.Security
{
    public static class HrmsClaims
    {
        public const string Permission = "permission";
        public const string EmployeeId = "employeeId";
        public const string UserId = "userId";
    }

    public static class Permissions
    {
        public const string EmployeesRead = "employees.read";
        public const string EmployeesWrite = "employees.write";
        public const string EmployeesDelete = "employees.delete";
        public const string TimeOffRead = "timeoff.read";
        public const string TimeOffRequest = "timeoff.request";
        public const string TimeOffApprove = "timeoff.approve";
        public const string OrgRead = "org.read";
        public const string OrgWrite = "org.write";
        public const string UsersRead = "users.read";
        public const string UsersWrite = "users.write";
        public const string ReportsRead = "reports.read";

        public static readonly string[] All =
        {
            EmployeesRead, EmployeesWrite, EmployeesDelete,
            TimeOffRead, TimeOffRequest, TimeOffApprove,
            OrgRead, OrgWrite, UsersRead, UsersWrite, ReportsRead
        };

        public static IReadOnlyDictionary<string, string> Labels { get; } = new Dictionary<string, string>
        {
            [EmployeesRead] = "مشاهده کارکنان",
            [EmployeesWrite] = "افزودن و ویرایش کارکنان",
            [EmployeesDelete] = "حذف کارکنان",
            [TimeOffRead] = "مشاهده مرخصی‌ها",
            [TimeOffRequest] = "ثبت درخواست مرخصی",
            [TimeOffApprove] = "تایید یا رد مرخصی",
            [OrgRead] = "مشاهده ساختار سازمان",
            [OrgWrite] = "مدیریت واحدها و عناوین شغلی",
            [UsersRead] = "مشاهده کاربران",
            [UsersWrite] = "مدیریت کاربران و نقش‌ها",
            [ReportsRead] = "دسترسی به گزارش‌ها"
        };

        public static string Policy(string permission) => "can:" + permission;
    }

    /// <summary>Authorization policy names, one per permission; Admin role always passes.</summary>
    public static class Policies
    {
        public const string EmployeesRead = "can:" + Permissions.EmployeesRead;
        public const string EmployeesWrite = "can:" + Permissions.EmployeesWrite;
        public const string EmployeesDelete = "can:" + Permissions.EmployeesDelete;
        public const string TimeOffRead = "can:" + Permissions.TimeOffRead;
        public const string TimeOffRequest = "can:" + Permissions.TimeOffRequest;
        public const string TimeOffApprove = "can:" + Permissions.TimeOffApprove;
        public const string OrgRead = "can:" + Permissions.OrgRead;
        public const string OrgWrite = "can:" + Permissions.OrgWrite;
        public const string UsersRead = "can:" + Permissions.UsersRead;
        public const string UsersWrite = "can:" + Permissions.UsersWrite;
        public const string ReportsRead = "can:" + Permissions.ReportsRead;
    }

    public static class IdentityClaims
    {
        public static ClaimsPrincipal Create(HRMS.Core.Dtos.AuthenticatedUser user, string authenticationType)
        {
            var identity = new ClaimsIdentity(authenticationType, ClaimTypes.Name, ClaimTypes.Role);
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()));
            identity.AddClaim(new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName));
            identity.AddClaim(new Claim(ClaimTypes.Role, user.RoleName));
            identity.AddClaim(new Claim(HrmsClaims.UserId, user.UserId.ToString()));
            identity.AddClaim(new Claim(HrmsClaims.EmployeeId, user.EmployeeId.ToString()));

            foreach (var permission in (user.Permissions ?? string.Empty)
                         .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                identity.AddClaim(new Claim(HrmsClaims.Permission, permission));
            }

            return new ClaimsPrincipal(identity);
        }
    }

    public static class CurrentUser
    {
        public static int? EmployeeId(this ClaimsPrincipal user) =>
            int.TryParse(user.FindFirstValue(HrmsClaims.EmployeeId), out var id) ? id : null;

        public static int? UserId(this ClaimsPrincipal user) =>
            int.TryParse(user.FindFirstValue(HrmsClaims.UserId), out var id) ? id : null;

        public static string DisplayName(this ClaimsPrincipal user) =>
            user.FindFirstValue(ClaimTypes.Name) ?? "کاربر";

        public static bool HasPermission(this ClaimsPrincipal user, string permission) =>
            user.IsInRole("Admin") || user.HasClaim(HrmsClaims.Permission, permission);
    }
}
