namespace DIH.Common.Services.Database.Helpers
{
    internal class IndexPagedDatabaseResult<T> : IIndexPagedDatabaseResult<T> where T : class
    {
        private IList<T> _resultItems;
        private int _pageSize;
        private int _itemOffset;
        private int _totalItemsCount;

        internal IndexPagedDatabaseResult(IList<T> resultItems, int pageSize, int itemOffset, int totalItemsCount)
        {
            _resultItems = resultItems;
            _pageSize = pageSize;
            _itemOffset = itemOffset;
            _totalItemsCount = totalItemsCount;
        }

        public IEnumerable<T> ResultItems => _resultItems;

        public int PageSize => _pageSize;
        public int TotalItemsCount => _totalItemsCount;
        public int TotalPagesCount => ((int)Math.Ceiling((decimal)_totalItemsCount / _pageSize));
        public int ResultItemsCount => _resultItems.Count();
        public bool HasPreviousPage => _itemOffset > 0;
        public bool HasNextPage => (_itemOffset + ResultItemsCount) < TotalItemsCount;

        public int ResultItemsIndexStart => _itemOffset;

        public int ResultItemsIndexEnd => _itemOffset + ResultItemsCount - 1;
    }
}


