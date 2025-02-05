using System.IO;
using System.Linq;
using System.Threading.Tasks;

class LearningMetrics(string rootFolder) : IQueueMetrics
{
    public Task<long> GetQueueLengthAsync(string queueName)
    {
        var queueFolder = Path.Combine(rootFolder, queueName);
        return Task.Run(() => Directory.EnumerateFiles(queueFolder).LongCount());
    }
}