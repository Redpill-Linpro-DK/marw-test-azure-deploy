namespace DIH.Common.Services.Database.Helpers
{
    internal class TokenPagedDatabaseResult<T> : ITokenPagedDatabaseResult<T> where T : class
    {
        private IAsyncEnumerable<T> _resultsPage;
        private string? _continuationToken;

        internal TokenPagedDatabaseResult(IAsyncEnumerable<T> resultsPage, string? continuationToken)
        {
            _resultsPage = resultsPage;
            _continuationToken = continuationToken;
        }

        public IAsyncEnumerable<T> ResultsPage => _resultsPage;

        public string? ContinuationToken => _continuationToken;
    }
}


