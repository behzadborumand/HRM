namespace HRMS.Core.Paging
{
    public class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
        public int TotalCount { get; init; }
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 20;

        public int PageCount => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < PageCount;
        public int FirstItemIndex => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
        public int LastItemIndex => TotalCount == 0 ? 0 : Math.Min(Page * PageSize, TotalCount);
    }
}
