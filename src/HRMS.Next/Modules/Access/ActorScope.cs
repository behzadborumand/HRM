namespace HRMS.Next.Modules.Access;

public sealed record ActorScope(int Id, string Username, int CompanyId, bool IsActive, bool IsAdmin, bool CanReadEmployees, bool CanWriteEmployees, int? EmployeeId = null, byte[]? AuthVersion = null);
