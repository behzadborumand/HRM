namespace HRMS.Core.Services
{
    public class OperationResult
    {
        public bool Succeeded { get; init; }
        public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
        public int EntityId { get; init; }

        public static OperationResult Ok(int entityId = 0) => new() { Succeeded = true, EntityId = entityId };

        public static OperationResult Fail(params string[] errors) =>
            new() { Succeeded = false, Errors = errors };
    }
}
