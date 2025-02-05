using System.Threading.Tasks;
using Azure.Messaging.ServiceBus.Administration;

class ServiceBusMetrics : IQueueMetrics
{
    private readonly ServiceBusAdministrationClient _client;

    public ServiceBusMetrics(string connectionString)
    {
        _client = new ServiceBusAdministrationClient(connectionString);
    }

    public async Task<long> GetQueueLengthAsync(string queueName)
    {
        var properties = await _client.GetQueueRuntimePropertiesAsync(queueName);
        return properties.Value.ActiveMessageCount;
    }
}