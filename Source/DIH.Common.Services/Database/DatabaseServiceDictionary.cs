namespace DIH.Common.Services.Database
{
    public class DatabaseServiceDictionary : IDatabaseServiceDictionary
    {
        Dictionary<Layer, IDatabaseService> _namedServices;
        public DatabaseServiceDictionary(Dictionary<Layer, IDatabaseService> namedServices)
        {
            ArgumentNullException.ThrowIfNull(namedServices, nameof(namedServices));
            if (namedServices.Keys.Count == 0) throw new ArgumentException("Cannot construct with an empty dictionary", nameof(namedServices));

            _namedServices = namedServices;
        }

        public IDatabaseService Get(Layer layer)
        {
            if (!_namedServices.ContainsKey(layer)) throw new ArgumentException($"This IDatabaseServiceDictionary has not been initialized with a IDatabaseService named '{layer}'", nameof(layer));

            return _namedServices[layer];
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var service in _namedServices.Values)
            {
                await service.DisposeAsync();
            }
        }
    }
}


