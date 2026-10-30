using System;
using System.Diagnostics.Metrics;

/// <summary>
/// Tracks one counter instrument and reports its rate over sliding windows of whole seconds.
/// </summary>
/// <remarks>
/// Measurements go into a ring of per-second buckets. A window rate is the sum of the complete seconds
/// in the window divided by the number of complete seconds available, so the rate is correct from the
/// second second of the process on, also when the process has run shorter than the window. The current,
/// partial second is not part of any window.
/// </remarks>
public sealed class RateAggregator : IDisposable
{
    // One hour of complete seconds plus the slot of the current second
    const int Capacity = 3600 + 1;

    readonly double[] buckets = new double[Capacity];
    readonly long[] bucketSecond = new long[Capacity];
    readonly object gate = new();
    readonly TimeProvider timeProvider;
    readonly long startTimestamp;
    readonly MeterListener listener;
    double total;

    public RateAggregator(Instrument instrument) : this(instrument, TimeProvider.System)
    {
    }

    public RateAggregator(Instrument instrument, TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
        startTimestamp = timeProvider.GetTimestamp();
        Array.Fill(bucketSecond, -1L);

        listener = new MeterListener();
        listener.InstrumentPublished = (published, l) =>
        {
            if (published == instrument)
            {
                l.EnableMeasurementEvents(published);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, measurement, _, _) => Add(measurement));
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => Add(measurement));
        listener.SetMeasurementEventCallback<int>((_, measurement, _, _) => Add(measurement));
        listener.Start();
    }

    TimeSpan Elapsed => timeProvider.GetElapsedTime(startTimestamp);

    void Add(double value)
    {
        var second = (long)Elapsed.TotalSeconds;
        var slot = (int)(second % Capacity);

        lock (gate)
        {
            if (bucketSecond[slot] != second)
            {
                bucketSecond[slot] = second;
                buckets[slot] = 0;
            }

            buckets[slot] += value;
            total += value;
        }
    }

    /// <summary>
    /// Average per second over the last <paramref name="window"/> complete seconds, or over all complete
    /// seconds since the start when that is less.
    /// </summary>
    public double Rate(TimeSpan window)
    {
        var current = (long)Elapsed.TotalSeconds;
        var seconds = Math.Min((long)window.TotalSeconds, current);

        if (seconds <= 0)
        {
            return 0;
        }

        double sum = 0;

        lock (gate)
        {
            for (var second = current - seconds; second < current; second++)
            {
                var slot = (int)(second % Capacity);

                if (bucketSecond[slot] == second)
                {
                    sum += buckets[slot];
                }
            }
        }

        return sum / seconds;
    }

    /// <summary>
    /// Sum of all measurements since the start.
    /// </summary>
    public double Total
    {
        get
        {
            lock (gate)
            {
                return total;
            }
        }
    }

    /// <summary>
    /// Average per second since the start, pauses included.
    /// </summary>
    public double LifetimeRate
    {
        get
        {
            var seconds = Elapsed.TotalSeconds;
            return seconds > 0 ? Total / seconds : 0;
        }
    }

    public record Rates(
        double Total,
        double LastFiveSecondsPerSecond,
        double LastTenSecondsPerSecond,
        double LastMinutePerSecond,
        double LastTenMinutesPerSecond,
        double LastHourPerSecond,
        double LifetimePerSecond
    );

    public Rates GetRates() => new(
        Total: Total,
        LastFiveSecondsPerSecond: Rate(TimeSpan.FromSeconds(5)),
        LastTenSecondsPerSecond: Rate(TimeSpan.FromSeconds(10)),
        LastMinutePerSecond: Rate(TimeSpan.FromMinutes(1)),
        LastTenMinutesPerSecond: Rate(TimeSpan.FromMinutes(10)),
        LastHourPerSecond: Rate(TimeSpan.FromHours(1)),
        LifetimePerSecond: LifetimeRate
    );

    public void Dispose()
    {
        listener.Dispose();
    }
}
