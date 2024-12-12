namespace DIH.Common.Services.Messaging
{
    public class MessagingServiceDictionary : IMessagingServiceDictionary
    {
        Dictionary<Layer, IMessagingService> _namedServices;
        public MessagingServiceDictionary(Dictionary<Layer, IMessagingService> namedServices)
        {
            ArgumentNullException.ThrowIfNull(namedServices, nameof(namedServices));
            if (namedServices.Keys.Count == 0) throw new ArgumentException("Cannot construct with an empty dictionary", nameof(namedServices));

            _namedServices = namedServices;
        }

        public IMessagingService Get(Layer layer)
        {
            if (!_namedServices.ContainsKey(layer)) throw new ArgumentException($"This IMessagingServiceDictionary has not been initialized with a IMessagingService named '{layer}'", nameof(layer));

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


