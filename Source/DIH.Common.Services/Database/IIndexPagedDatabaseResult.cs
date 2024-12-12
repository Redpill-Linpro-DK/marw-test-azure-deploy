namespace DIH.Common.Services.Database
{
    public interface IIndexPagedDatabaseResult<T> where T : class
    {
        IEnumerable<T> ResultItems { get; }
        int PageSize { get; }
        int ResultItemsIndexStart { get; }
        int ResultItemsIndexEnd { get; }
        bool HasNextPage { get; }
        bool HasPreviousPage { get; }
        int ResultItemsCount { get; }
        int TotalItemsCount { get; }
        int TotalPagesCount { get; }
    }
}

