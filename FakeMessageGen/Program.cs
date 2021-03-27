using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using App.Metrics;
using App.Metrics.Counter;
using App.Metrics.Meter;
using App.Metrics.Scheduling;
using NServiceBus;
using NServiceBus.Extensibility;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Transport;
//===
using ServiceControl.Transports;
using ServiceControl.Transports.ASBS;

namespace FakeMessageGen
{
    class Program
    {
        static RateGate rateLimiter = new RateGate(33, TimeSpan.FromSeconds(1));
        
        static readonly string prefix = Guid.NewGuid().ToString("n") + "/" + DateTime.UtcNow.ToString("s") + "/";
        static SemaphoreSlim concurrency = new SemaphoreSlim(MaxConcurrency);
        static IReceivingRawEndpoint sender;

        static State state = State.Running;

        const int MaxConcurrency = 100;
        const int MaxQueueLength = 1000;
        static object l = new object();

        static async Task Main(string[] args)
        {
            var metrics = new MetricsBuilder()
                .Report.ToConsole()
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
                Console.WriteLine($"{DateTime.Now} {b.InputQueue} = {a[0].Value}");

                lock (l)
                {
                    if (count > MaxQueueLength)
                    {
                        // Ensure paused
                        var result = InterlockedEx.CompareExchange<State>(ref state, State.Paused, State.Running);

                        if (result == State.Running)
                        {
                            Console.WriteLine("Pause, draining semaphore...");
                            for (var x = 0; x < 100; x++)
                            {
                                concurrency.Wait();
                            }

                            Console.WriteLine("Drained!");
                        }
                    }
                    else
                    {
                        // Ensure running
                        var result = InterlockedEx.CompareExchange(ref state, State.Running, State.Paused);

                        if (result == State.Paused)
                        {
                            Console.WriteLine("Continue, releasing");
                            concurrency.Release(100);
                        }
                    }
                }
            });

            queueLengthProvider.TrackEndpointInputQueue(new EndpointToQueueMapping("error", "error"));

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

                    _ = Send();
                }
            });

            do
            {
                await Console.Out.WriteLineAsync("Press ESC to exit...");
            } while (Console.ReadKey(true).Key != ConsoleKey.Escape);

            await sender.Stop();
            await queueLengthProvider.Stop();
        }

        static async Task Send()
        {
            try
            {
                var body = new byte[StaticRandom.Next(1000, 10000)];

                StaticRandom.NextBytes(body);

                var count = 0;

                var intents = new[] {"Send", "Publish", "Reply"};
                var headers = new Dictionary<string, string>
                {
                    [Headers.MessageId] = prefix + ++count,
                    [Headers.ContentType] = "random",
                    [Headers.EnclosedMessageTypes] = "random_" + StaticRandom.Next(250),
                    [Headers.CorrelationId] = DateTime.UtcNow.ToString("yyyy-M-d hh") + " " + StaticRandom.Next(100000),
                    [Headers.ConversationId] = DateTime.UtcNow.ToString("yyyy-M-d hh") + " " + StaticRandom.Next(100000),
                    //[Headers.RelatedTo] = "random",
                    [Headers.MessageIntent] = intents[StaticRandom.Next(3)],
                    ["NServiceBus.FailedQ"] = "endpoint_" + StaticRandom.Next(100)
                };

                var request = new OutgoingMessage(
                    messageId: Guid.NewGuid().ToString(),
                    headers: headers,
                    body: body);

                var operation = new TransportOperation(
                    request,
                    new UnicastAddressTag("error"));

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
}


/// <summary>
/// Thread-safe equivalent of System.Random, using just static methods.
/// If all you want is a source of random numbers, this is an easy class to
/// use. If you need to specify your own seeds (eg for reproducible sequences
/// of numbers), use System.Random.
/// </summary>
static class StaticRandom
{
    static Random random = new Random();
    static object myLock = new object();

    /// <summary>
    /// Returns a nonnegative random number. 
    /// </summary>		
    /// <returns>A 32-bit signed integer greater than or equal to zero and less than Int32.MaxValue.</returns>
    public static int Next()
    {
        lock (myLock)
        {
            return random.Next();
        }
    }

    /// <summary>
    /// Returns a nonnegative random number less than the specified maximum. 
    /// </summary>
    /// <returns>
    /// A 32-bit signed integer greater than or equal to zero, and less than maxValue; 
    /// that is, the range of return values includes zero but not maxValue.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">maxValue is less than zero.</exception>
    public static int Next(int max)
    {
        lock (myLock)
        {
            return random.Next(max);
        }
    }

    /// <summary>
    /// Returns a random number within a specified range. 
    /// </summary>
    /// <param name="min">The inclusive lower bound of the random number returned. </param>
    /// <param name="max">
    /// The exclusive upper bound of the random number returned. 
    /// maxValue must be greater than or equal to minValue.
    /// </param>
    /// <returns>
    /// A 32-bit signed integer greater than or equal to minValue and less than maxValue;
    /// that is, the range of return values includes minValue but not maxValue.
    /// If minValue equals maxValue, minValue is returned.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">minValue is greater than maxValue.</exception>
    public static int Next(int min, int max)
    {
        lock (myLock)
        {
            return random.Next(min, max);
        }
    }

    /// <summary>
    /// Returns a random number between 0.0 and 1.0.
    /// </summary>
    /// <returns>A double-precision floating point number greater than or equal to 0.0, and less than 1.0.</returns>
    public static double NextDouble()
    {
        lock (myLock)
        {
            return random.NextDouble();
        }
    }

    /// <summary>
    /// Fills the elements of a specified array of bytes with random numbers.
    /// </summary>
    /// <param name="buffer">An array of bytes to contain random numbers.</param>
    /// <exception cref="ArgumentNullException">buffer is a null reference (Nothing in Visual Basic).</exception>
    public static void NextBytes(byte[] buffer)
    {
        lock (myLock)
        {
            random.NextBytes(buffer);
        }
    }
}

enum State
{
    Running,
    Paused
}


static class CompareExchangeEnumImpl<T>
{
    public delegate T dImpl(ref T location, T value, T comparand);

    public static readonly dImpl Impl = CreateCompareExchangeImpl();

    static dImpl CreateCompareExchangeImpl()
    {
        var underlyingType = Enum.GetUnderlyingType(typeof(T));
        var dynamicMethod = new DynamicMethod(string.Empty, typeof(T),
            new[] {typeof(T).MakeByRefType(), typeof(T), typeof(T)});
        var ilGenerator = dynamicMethod.GetILGenerator();
        ilGenerator.Emit(OpCodes.Ldarg_0);
        ilGenerator.Emit(OpCodes.Ldarg_1);
        ilGenerator.Emit(OpCodes.Ldarg_2);
        ilGenerator.Emit(
            OpCodes.Call,
            typeof(Interlocked).GetMethod(
                "CompareExchange",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] {underlyingType.MakeByRefType(), underlyingType, underlyingType},
                null));
        ilGenerator.Emit(OpCodes.Ret);
        return (dImpl) dynamicMethod.CreateDelegate(typeof(dImpl));
    }
}

public static class InterlockedEx
{
    public static T CompareExchange<T>(ref T location, T value, T comparand)
    {
        return CompareExchangeEnumImpl<T>.Impl(ref location, value, comparand);
    }
}

