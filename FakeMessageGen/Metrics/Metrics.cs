using System.Diagnostics.Metrics;

/// <summary>
/// Counters of the tool. The meter <c>FakeMessageGen</c> is visible to <c>dotnet-counters</c> and other
/// <see cref="MeterListener"/> based tools.
/// </summary>
static class Metrics
{
    static readonly Meter Meter = new("FakeMessageGen");
    static readonly Counter<double> Sends = Meter.CreateCounter<double>("sends", "messages", "Messages that were dispatched");
    static readonly Counter<double> Faults = Meter.CreateCounter<double>("faults", "messages", "Messages whose dispatch failed");

    public static readonly RateAggregator SendsAggregator = new(Sends);
    public static readonly RateAggregator FaultsAggregator = new(Faults);

    /// <summary>
    /// Runs the static initialization now, so the lifetime rate counts from here and not from the first send.
    /// </summary>
    public static void Start()
    {
    }

    /// <summary>
    /// Call after a batch was dispatched.
    /// </summary>
    public static void SendsAdd(int messages) => Sends.Add(messages);

    /// <summary>
    /// Call when the dispatch of a batch failed.
    /// </summary>
    public static void FaultsAdd(int messages) => Faults.Add(messages);
}
