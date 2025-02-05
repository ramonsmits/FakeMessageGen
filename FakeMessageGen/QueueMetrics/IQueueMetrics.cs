using System.Threading.Tasks;

interface IQueueMetrics
{
    Task<long> GetQueueLengthAsync(string queueName);
}