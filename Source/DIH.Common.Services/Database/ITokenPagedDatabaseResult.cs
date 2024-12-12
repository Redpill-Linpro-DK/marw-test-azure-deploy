namespace DIH.Common.Services.Database
{
    public interface ITokenPagedDatabaseResult<T> where T : class
    {
        string? ContinuationToken { get; }
        IAsyncEnumerable<T> ResultsPage { get; }
    }
}


