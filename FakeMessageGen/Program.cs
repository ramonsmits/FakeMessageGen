using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using App.Metrics;
using App.Metrics.Meter;
using App.Metrics.Scheduling;
using NServiceBus;
using NServiceBus.Extensibility;
using NServiceBus.Logging;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Transport;
using ServiceControl.Transports;
using ServiceControl.Transports.ASBS;

static partial class Program
{
    static string destination;
    static bool isError;

    static int MaxConcurrency = 100;
    static int MaxQueueLength = 1000;
    static int RateLimit = 1000;

    static readonly object lockObj = new();
    static readonly SemaphoreSlim concurrency = new(MaxConcurrency);
    static RateGate rateLimiter; 
    static IReceivingRawEndpoint sender;
    static State state = State.Running;

    static async Task Main(string[] args)
    {
        try
        {
            destination = args[0];
            isError = bool.Parse(args[1]);
            if (args.Length > 2) MaxQueueLength = int.Parse(args[2]);
            if (args.Length > 3) RateLimit = int.Parse(args[3]);
            if (args.Length > 4) MaxConcurrency = int.Parse(args[4]);
        }
        catch
        {
            Console.WriteLine("destination isError (maxQueueLength) (rateLimit) (maxConcurrency)");
            return;
        }

        rateLimiter = new(RateLimit, TimeSpan.FromSeconds(1));

        Console.OutputEncoding = Encoding.UTF8;

        InitFrames();

        LogManager.UseFactory(new FrameLoggerFactory(logFrame));

        var metrics = new MetricsBuilder()
            .Report.ToConsole(o =>
            {
                o.MetricsOutputFormatter = new App.Metrics.Formatters.Ascii.MetricsTextOutputFormatter();
                o.FlushInterval = TimeSpan.FromSeconds(5);
            })
            .Build();

        var rate = new MeterOptions
        {
            Name = "rate",
            MeasurementUnit = Unit.Calls,
            RateUnit = TimeUnit.Seconds
        };

        var scheduler = new AppMetricsTaskScheduler(
            TimeSpan.FromSeconds(5),
            async () => { await Task.WhenAll(metrics.ReportRunner.RunAllAsync()); });
        scheduler.Start();


        var senderConfig = RawEndpointConfiguration.CreateSendOnly(endpointName: "EndpointName");

        var cs = Environment.ExpandEnvironmentVariables(
            Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString")!);

        var transport = senderConfig.UseTransport<AzureServiceBusTransport>();
        transport.ConnectionString(cs);

        var queueLengthProvider = new QueueLengthProvider();
        queueLengthProvider.Initialize(cs, (a, b) =>
        {
            var count = a[0].Value;
            queueFrame.WriteLine($"{DateTime.Now} {b.InputQueue} = {a[0].Value}");

            lock (lockObj)
            {
                if (count > MaxQueueLength)
                {
                    // Ensure paused
                    var result = InterlockedEx.CompareExchange(ref state, State.Paused, State.Running);

                    if (result == State.Running)
                    {
                        queueFrame.WriteLine("Pause");
                        for (var x = 0; x < 100; x++)
                        {
                            concurrency.Wait();
                        }
                    }
                }
                else
                {
                    // Ensure running
                    var result = InterlockedEx.CompareExchange(ref state, State.Running, State.Paused);

                    if (result == State.Paused)
                    {
                        queueFrame.WriteLine("Continue");
                        concurrency.Release(100);
                    }
                }
            }
        });

        queueLengthProvider.TrackEndpointInputQueue(new EndpointToQueueMapping(destination, destination));

        await queueLengthProvider.Start();

        sender = await RawEndpoint.Start(senderConfig);

        _ = Task.Run(async () =>
        {
            while (true)
            {
                await rateLimiter.WaitAsync();
                await concurrency.WaitAsync();
                metrics.Measure.Meter.Mark(rate);

                _ = Send(isError);
            }
        });

        do
        {
            Console.WriteLine("Press ESC to exit...");
        } while (Console.ReadKey(true).Key != ConsoleKey.Escape);

        await sender.Stop();
        await queueLengthProvider.Stop();
    }

    static async Task Send(bool isError)
    {
        try
        {
            var msg = FakeMessageGenerator.Create(isError);

            var request = new OutgoingMessage(msg.id, headers: msg.headers, body: msg.body);

            var operation = new TransportOperation(
                request,
                new UnicastAddressTag(destination));

            await sender.Dispatch(
                outgoingMessages: new TransportOperations(operation),
                transaction: new TransportTransaction(),
                context: new ContextBag());
        }
        finally
        {
            concurrency.Release();
        }
    }

}