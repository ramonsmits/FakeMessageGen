using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using App.Metrics;
using App.Metrics.Counter;
using App.Metrics.Meter;
using App.Metrics.Scheduling;
using NServiceBus;
using NServiceBus.Extensibility;
using NServiceBus.Logging;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Sagas;
using NServiceBus.Transport;
using ServiceControl.Transports;
using ServiceControl.Transports.ASBS;

static partial class Program
{
    const string destination = "audit";

    const int MaxConcurrency = 100;
    const int MaxQueueLength = 1000;

    static readonly object lockObj = new();
    static readonly RateGate rateLimiter = new(200, TimeSpan.FromSeconds(1));
    static readonly string prefix = Guid.NewGuid().ToString("n") + "/" + DateTime.UtcNow.ToString("s") + "/";
    static readonly SemaphoreSlim concurrency = new(MaxConcurrency);

    static IReceivingRawEndpoint sender;
    static State state = State.Running;
    static int count;

    static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        InitFrames();

        //LogManager.Use<DefaultFactory>().Level(LogLevel.Debug);
        LogManager.UseFactory(new FrameLoggerFactory(logFrame));

        FrameMaster.Out.WriteLine("Pause, \u001b[31mHello World!\u001b[0m...");

        var metrics = new MetricsBuilder()
            .Report.ToConsole(o =>
            {
                o.MetricsOutputFormatter = new App.Metrics.Formatters.Ascii.MetricsTextOutputFormatter();
                o.FlushInterval = TimeSpan.FromSeconds(5);
            })
            .Build();

        var sent = new CounterOptions {Name = "sent"};

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
                        queueFrame.WriteLine("Pause, draining semaphore...");
                        for (var x = 0; x < 100; x++)
                        {
                            concurrency.Wait();
                        }

                        queueFrame.WriteLine("Drained!");
                    }
                }
                else
                {
                    // Ensure running
                    var result = InterlockedEx.CompareExchange(ref state, State.Running, State.Paused);

                    if (result == State.Paused)
                    {
                        queueFrame.WriteLine("Continue, releasing");
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
                metrics.Measure.Counter.Increment(sent);
                metrics.Measure.Meter.Mark(rate);

                _ = Send(false);
            }
        });

        do
        {
//            await mainFrame.WriteLineAsync("Press ESC to exit...");
            Console.WriteLine("Press ESC to exit...");
        } while (Console.ReadKey(true).Key != ConsoleKey.Escape);

        await sender.Stop();
        await queueLengthProvider.Stop();
    }

    const string Sentences = "ABCD EFGH IJKL MNOP QRST UVWX YZ01 2345 6789\n";
    const string Lines = "ABCD EFGH IJKL MNOP QRST UVWX YZ01 2345 6789";
    const string Types = "ABCDEFGHIJKLMNOPQRSTUVWXYZ.";

    static string RandomString(int length, string chars)
    {
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[StaticRandom.Next(s.Length)]).ToArray());
    }

    static async Task Send(bool isError)
    {
        try
        {
            var body = new byte[StaticRandom.Next(1000, 10000)];

            StaticRandom.NextBytes(body);


            var now = DateTime.UtcNow;

            var intents = new[] {"Send", "Publish", "Reply"};
            var headers = new Dictionary<string, string>
            {
                [Headers.MessageId] = prefix + Interlocked.Increment(ref count),
                [Headers.ContentType] = "random",
                [Headers.EnclosedMessageTypes] = "random_" + StaticRandom.Next(250),
                [Headers.CorrelationId] = now.ToString("yyyy-M-d hh") + " " + StaticRandom.Next(100000),
                [Headers.ConversationId] = now.ToString("yyyy-M-d hh") + " " + StaticRandom.Next(100000),
                //[Headers.RelatedTo] = "random",
                [Headers.MessageIntent] = intents[StaticRandom.Next(3)],
                [Headers.TimeSent] = DateTimeExtensions.ToWireFormattedString(now),
                ["NServiceBus.OriginatingEndpoint"] = "endpoint_" + StaticRandom.Next(100),
                ["NServiceBus.OriginatingMachine"] = Environment.MachineName
            };

            if (isError)
            {
                headers["NServiceBus.FailedQ"] = "endpoint_" + StaticRandom.Next(100);
                headers["NServiceBus.ExceptionInfo.ExceptionType"] = RandomString(StaticRandom.Next(15, 250), Types);
                headers["NServiceBus.ExceptionInfo.InnerExceptionType"] = RandomString(StaticRandom.Next(15, 250), Types);
                headers["NServiceBus.ExceptionInfo.Message"] = RandomString(StaticRandom.Next(15, 250), Lines);
                headers["NServiceBus.ExceptionInfo.StackTrace"] =
                    RandomString(StaticRandom.Next(1000, 8000), Sentences);
                //["NServiceBus.ExceptionInfo.HelpLink"] //The exception help link.
                //["NServiceBus.ExceptionInfo.Source"] //The full type name of the InnerException if it exists. It is obtained by calling Exception.InnerException.GetType().FullName.
            }
            else
            {
                headers["NServiceBus.ProcessingEndpoint"] = "endpoint_" + StaticRandom.Next(100);
                headers["NServiceBus.ProcessingMachine"] = Environment.MachineName;
                headers["NServiceBus.ProcessingStarted"] = DateTimeExtensions.ToWireFormattedString(now);
                headers["NServiceBus.ProcessingEnded"] = DateTimeExtensions.ToWireFormattedString(now.AddMilliseconds(StaticRandom.Next(20, 20000)));
            }

            var request = new OutgoingMessage(
                messageId: Guid.NewGuid().ToString(),
                headers: headers,
                body: body);

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
