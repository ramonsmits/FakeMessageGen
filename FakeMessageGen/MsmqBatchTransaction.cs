using System;
using NServiceBus.Transport;
using Particular.Msmq;

/// <summary>
/// Sends a batch within a single native MSMQ transaction. Without it the MSMQ dispatcher
/// uses an internal transaction per message which is considerably slower.
/// </summary>
sealed class MsmqBatchTransaction : IDisposable
{
    readonly MessageQueueTransaction transaction = new();

    MsmqBatchTransaction()
    {
    }

    public static MsmqBatchTransaction Begin(TransportTransaction transportTransaction)
    {
        var batch = new MsmqBatchTransaction();
        batch.transaction.Begin();
        transportTransaction.Set(batch.transaction);
        return batch;
    }

    public void Commit() => transaction.Commit();

    // Disposing a pending transaction aborts it
    public void Dispose() => transaction.Dispose();
}
