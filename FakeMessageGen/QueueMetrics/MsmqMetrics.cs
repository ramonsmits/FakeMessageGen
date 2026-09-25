using System.Threading.Tasks;
using NServiceBus.Transport.Msmq;
using Particular.Msmq;
using Particular.Msmq.Interop;

/// <summary>
/// Reads the queue length using the MSMQ management API (<c>MQMgmtGetInfo</c>) which is O(1),
/// unlike enumerating the messages in the queue.
/// </summary>
class MsmqMetrics : IQueueMetrics
{
    // https://learn.microsoft.com/windows/win32/msmq/propid-mgmt-queue-message-count
    const int PROPID_MGMT_QUEUE_MESSAGE_COUNT = 7;
    const string FormatNamePrefix = "FormatName:";

    public Task<long> GetQueueLengthAsync(string queueName)
    {
        var address = MsmqAddress.Parse(queueName);
        var machineName = address.IsRemote() ? address.Machine : null;
        var objectName = "QUEUE=" + address.FullPath[FormatNamePrefix.Length..];

        var properties = new MessagePropertyVariants(PROPID_MGMT_QUEUE_MESSAGE_COUNT + 1, 0);
        properties.SetNull(PROPID_MGMT_QUEUE_MESSAGE_COUNT);
        var status = UnsafeNativeMethods.MQMgmtGetInfo(machineName, objectName, properties.Lock());
        properties.Unlock();

        if (MessageQueue.IsFatalError(status))
        {
            throw new MessageQueueException(status);
        }

        return Task.FromResult((long)(uint)properties.GetUI4(PROPID_MGMT_QUEUE_MESSAGE_COUNT));
    }
}
