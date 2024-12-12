namespace DIH.Common.Services.Database
{
    /// <summary>
    /// Contains IDatabaseService instances to access database services of diffferent layers.
    /// </summary>
    public interface IDatabaseServiceDictionary : IAsyncDisposable
    {
        /// <summary>
        /// Returna a IDatabaseService instance to access the database from the specified layer
        /// </summary>
        /// <param name="name">The layer owning the database</param>
        /// <returns>Database service</returns>
        IDatabaseService Get(Layer name);
    }
}


