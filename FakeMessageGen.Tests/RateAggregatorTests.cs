using System;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

public class RateAggregatorTests
{
    Meter meter;
    Counter<double> counter;
    FakeTimeProvider clock;
    RateAggregator aggregator;

    [SetUp]
    public void SetUp()
    {
        meter = new Meter("RateAggregatorTests." + Guid.NewGuid());
        counter = meter.CreateCounter<double>("sends");
        clock = new FakeTimeProvider();
        aggregator = new RateAggregator(counter, clock);
    }

    [TearDown]
    public void TearDown()
    {
        aggregator.Dispose();
        meter.Dispose();
    }

    [Test]
    public void Window_rate_divides_by_the_uptime_when_shorter_than_the_window()
    {
        clock.Advance(TimeSpan.FromSeconds(0.5));
        counter.Add(100);
        clock.Advance(TimeSpan.FromSeconds(1.5));

        // Two complete seconds since the start, not sixty
        Assert.That(aggregator.Rate(TimeSpan.FromMinutes(1)), Is.EqualTo(50));
        Assert.That(aggregator.Rate(TimeSpan.FromHours(1)), Is.EqualTo(50));
    }

    [Test]
    public void Steady_rate_is_reported_by_every_window()
    {
        for (var second = 0; second < 70; second++)
        {
            counter.Add(10);
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        var rates = aggregator.GetRates();

        Assert.That(rates.LastFiveSecondsPerSecond, Is.EqualTo(10));
        Assert.That(rates.LastTenSecondsPerSecond, Is.EqualTo(10));
        Assert.That(rates.LastMinutePerSecond, Is.EqualTo(10));
        Assert.That(rates.LastTenMinutesPerSecond, Is.EqualTo(10));
        Assert.That(rates.LastHourPerSecond, Is.EqualTo(10));
        Assert.That(rates.LifetimePerSecond, Is.EqualTo(10));
        Assert.That(rates.Total, Is.EqualTo(700));
    }

    [Test]
    public void Window_rate_excludes_the_current_partial_second()
    {
        clock.Advance(TimeSpan.FromSeconds(10));
        counter.Add(16);

        Assert.That(aggregator.Rate(TimeSpan.FromSeconds(5)), Is.EqualTo(0));

        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.That(aggregator.Rate(TimeSpan.FromSeconds(5)), Is.EqualTo(16.0 / 5));
    }

    [Test]
    public void Window_rate_drops_to_zero_when_the_window_passes_without_measurements()
    {
        counter.Add(50);
        clock.Advance(TimeSpan.FromSeconds(11));

        Assert.That(aggregator.Rate(TimeSpan.FromSeconds(10)), Is.EqualTo(0));
        Assert.That(aggregator.Rate(TimeSpan.FromMinutes(1)), Is.EqualTo(50.0 / 11));
    }

    [Test]
    public void Bucket_slots_are_not_reused_after_an_hour()
    {
        counter.Add(3600);
        clock.Advance(TimeSpan.FromSeconds(3601));
        counter.Add(1);
        clock.Advance(TimeSpan.FromSeconds(1));

        // The first measurement is older than one hour and its slot was reused by the second one
        Assert.That(aggregator.Rate(TimeSpan.FromHours(1)), Is.EqualTo(1.0 / 3600));
        Assert.That(aggregator.Total, Is.EqualTo(3601));
    }

    [Test]
    public void Lifetime_rate_includes_idle_time()
    {
        counter.Add(100);
        clock.Advance(TimeSpan.FromSeconds(50));

        Assert.That(aggregator.LifetimeRate, Is.EqualTo(2));
    }

    [Test]
    public void Only_the_tracked_instrument_is_counted()
    {
        var other = meter.CreateCounter<double>("faults");
        other.Add(999);
        counter.Add(1);
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.That(aggregator.Total, Is.EqualTo(1));
    }
}
