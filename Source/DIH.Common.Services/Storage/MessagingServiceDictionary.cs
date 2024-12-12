namespace DIH.Common.Services.Storage
{
    public class StorageServiceDictionary : IStorageServiceDictionary
    {
        Dictionary<Layer, IStorageService> _namedServices;
        public StorageServiceDictionary(Dictionary<Layer, IStorageService> namedServices)
        {
            ArgumentNullException.ThrowIfNull(namedServices, nameof(namedServices));
            if (namedServices.Keys.Count == 0) throw new ArgumentException("Cannot construct with an empty dictionary", nameof(namedServices));

            _namedServices = namedServices;
        }

        public IStorageService Get(Layer layer)
        {
            if (!_namedServices.ContainsKey(layer)) throw new ArgumentException($"This IStorageServiceDictionary has not been initialized with a IStorageService named '{layer}'", nameof(layer));

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


