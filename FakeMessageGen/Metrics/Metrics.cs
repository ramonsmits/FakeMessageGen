using System.Diagnostics.Metrics;

static class Metrics
{
    static Meter Meter = new Meter("FakeMessageGen");
    static Counter<double> Sends = Meter.CreateCounter<double>("sends");
    static Counter<double> Faults = Meter.CreateCounter<double>("faults");
    public static RateAggregator SendsAggregator = new RateAggregator(Sends);

    public static void BatchSizeAdd(int batchSize)
    {
        Sends.Add(batchSize);
    }

    public static void FaultsAdd(int i)
    {
        Faults.Add(1);
    }
}