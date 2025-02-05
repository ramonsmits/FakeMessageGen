using System;
using System.Diagnostics.Metrics;
using System.Collections.Concurrent;
using System.Linq;

public class RateAggregator : IDisposable
{
    private record struct Bucket(DateTime Time, double Value);

    // For current second measurements only
    private readonly ConcurrentQueue<(DateTime Time, double Value)> currentSecond = new();

    // Pre-aggregated 1-second buckets for longer windows
    private readonly ConcurrentQueue<Bucket> buckets = new();

    private readonly DateTime startTime = DateTime.UtcNow;
    private readonly MeterListener listener;
    private double totalCount;
    private DateTime lastBucketTime = DateTime.UtcNow;

    public RateAggregator(Instrument instrumentToTrack)
    {
        listener = new MeterListener();
        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument == instrumentToTrack)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            totalCount += measurement;
            currentSecond.Enqueue((DateTime.UtcNow, measurement));
            AggregateBuckets();
            CleanupOldBuckets();
        });

        listener.Start();
    }

    private void AggregateBuckets()
    {
        var now = DateTime.UtcNow;
        var currentBucketTime = new DateTime(now.Year, now.Month, now.Day,
            now.Hour, now.Minute, now.Second, 0, DateTimeKind.Utc);

        if (currentBucketTime > lastBucketTime)
        {
            // Aggregate previous second into a bucket
            var prevSecondMeasurements = currentSecond
                .Where(m => m.Time < currentBucketTime)
                .ToList();

            if (prevSecondMeasurements.Any())
            {
                var sum = prevSecondMeasurements.Sum(m => m.Value);
                buckets.Enqueue(new Bucket(lastBucketTime, sum));

                // Clean up aggregated measurements
                while (currentSecond.TryPeek(out var oldest) && oldest.Time < currentBucketTime)
                {
                    currentSecond.TryDequeue(out _);
                }
            }

            lastBucketTime = currentBucketTime;
        }
    }

    private double CalculateRate(TimeSpan window)
    {
        var now = DateTime.UtcNow;
        var windowStart = now - window;

        // For windows larger than 1 second, use the pre-aggregated buckets
        var relevantBuckets = buckets
            .Where(b => b.Time >= windowStart)
            .ToList();

        if (!relevantBuckets.Any()) return 0;

        var sum = relevantBuckets.Sum(b => b.Value);
        var latestBucket = relevantBuckets.Max(b => b.Time);

        // If the last bucket is older than our window, decay the rate
        if ((now - latestBucket) > window)
            return 0;

        return sum / window.TotalSeconds;
    }

    public double CurrentRate
    {
        get
        {
            var now = DateTime.UtcNow;
            var lastSecond = currentSecond.Where(m => (now - m.Time).TotalSeconds <= 1);
            if (!lastSecond.Any()) return 0;
            return lastSecond.Sum(m => m.Value);
        }
    }

    public double LastTenSecondsRate => CalculateRate(TimeSpan.FromSeconds(10));
    public double LastMinuteRate => CalculateRate(TimeSpan.FromMinutes(1));
    public double LastTenMinutesRate => CalculateRate(TimeSpan.FromMinutes(10));
    public double LastHourRate => CalculateRate(TimeSpan.FromHours(1));
    public double LifetimeRate => totalCount / (DateTime.UtcNow - startTime).TotalSeconds;

    private void CleanupOldBuckets()
    {
        var cutoff = DateTime.UtcNow - TimeSpan.FromHours(1);
        while (buckets.TryPeek(out var oldest) && oldest.Time < cutoff)
        {
            buckets.TryDequeue(out _);
        }
    }

    public record Rates(
        double CurrentPerSecond,
        double LastTenSecondsPerSecond,
        double LastMinutePerSecond,
        double LastTenMinutesPerSecond,
        double LastHourPerSecond,
        double LifetimePerSecond
    );

    public Rates GetRates() => new(
        CurrentPerSecond: CurrentRate,
        LastTenSecondsPerSecond: LastTenSecondsRate,
        LastMinutePerSecond: LastMinuteRate,
        LastTenMinutesPerSecond: LastTenMinutesRate,
        LastHourPerSecond: LastHourRate,
        LifetimePerSecond: LifetimeRate
    );

    public void Dispose()
    {
        listener?.Dispose();
    }
}