using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using DIH.Common.Credential;

namespace DIH.Common.Services.Messaging
{
    public class AzureServiceBusQueueService : IMessagingService
    {
        DihLogger<AzureServiceBusQueueService> Logger;
        private readonly string _queueServiceEndpoint;
        private readonly ServiceBusClient _queueServiceClient;

        public AzureServiceBusQueueService(
            ILogger<AzureServiceBusQueueService> logger,
            string queueServiceEndpoint)
        {
            if (string.IsNullOrEmpty(queueServiceEndpoint)) throw new ArgumentNullException(nameof(queueServiceEndpoint));

            Logger = logger.AsDihLogger();
            _queueServiceEndpoint = queueServiceEndpoint;
            _queueServiceClient = new ServiceBusClient(_queueServiceEndpoint, AzureCredentialFactory.BuildDefault());
        }

        public async Task EnqueueMessageAsync<T>(string queueOrTopicName, T message, bool compact = false, DateTimeOffset? scheduledEnqueueTime = null, string? label = null) where T : IMessage
        {
            Logger.DihDebug($"EnqueueMessageAsync sending on {queueOrTopicName} {(scheduledEnqueueTime != null ? $"(scheduled {scheduledEnqueueTime})" : "")}");

            if (string.IsNullOrEmpty(queueOrTopicName)) throw new ArgumentNullException(nameof(queueOrTopicName));
            ArgumentNullException.ThrowIfNull(message, nameof(message));

            // Create a sender for the queue
            await using (ServiceBusSender sender = _queueServiceClient.CreateSender(queueOrTopicName))
            {
                var messageString = JsonConvert.SerializeObject(message, compact ? Formatting.None : Formatting.Indented);

                // Create a message
                ServiceBusMessage serviceBusMessage = new ServiceBusMessage(Encoding.UTF8.GetBytes(messageString));

                if (label is not null)
                {
                    serviceBusMessage.Subject = label;
                }

                if (scheduledEnqueueTime.HasValue)
                {
                    serviceBusMessage.ScheduledEnqueueTime = scheduledEnqueueTime.Value;
                }

                // Send the message
                await sender.SendMessageAsync(serviceBusMessage);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _queueServiceClient.DisposeAsync();
        }
    }
}


