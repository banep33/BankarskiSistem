namespace BankarskiSistem.Core.Models
{
    public class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; init; } = [];
        public int TotalCount { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPrevious => Page > 1;
        public bool HasNext => Page < TotalPages;
    }

    public record PageRequest
    {
        public const int MaxPageSize = 100;
        public const int DefaultPageSize = 20;

        public int Page { get; }
        public int PageSize { get; }
        public int Skip => (Page - 1) * PageSize;

        public PageRequest(int page = 1, int pageSize = DefaultPageSize)
        {
            Page = page < 1 ? 1 : page;
            PageSize = pageSize is < 1 or > MaxPageSize ? DefaultPageSize : pageSize;
        }
    }
}
