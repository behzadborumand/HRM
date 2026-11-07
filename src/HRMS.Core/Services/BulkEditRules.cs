namespace HRMS.Core.Services
{
    /// <summary>
    /// A bulk edit only touches the fields the user filled in; an empty field means "leave as is",
    /// which is also why bulk editing cannot clear a value.
    /// </summary>
    public static class BulkEditRules
    {
        public static bool ChangesJob(int? departmentId, int? jobTitleId, int? managerId, string? employmentType) =>
            departmentId is not null || jobTitleId is not null || managerId is not null || !string.IsNullOrWhiteSpace(employmentType);

        public static bool ChangesStatus(string? employmentStatus) => !string.IsNullOrWhiteSpace(employmentStatus);

        public static bool HasAnyChange(int? departmentId, int? jobTitleId, int? managerId, string? employmentType,
            string? employmentStatus) =>
            ChangesJob(departmentId, jobTitleId, managerId, employmentType) || ChangesStatus(employmentStatus);

        /// <summary>An employee can never become their own manager, so that selection is ignored.</summary>
        public static int? ManagerFor(int? requestedManagerId, int employeeId) =>
            requestedManagerId == employeeId ? null : requestedManagerId;
    }
}
