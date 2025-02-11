using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Transport;

static partial class Program
{
    static string destination;
    static bool isError;

    static int MaxQueueLength = 100000;
    static int RateLimit = 5000;
    static int MaxConcurrency = 100;
    static int BatchSize = 16;
    private static string ConnectionString;

    static readonly TimeSpan QueryDelayInterval = TimeSpan.FromSeconds(2);

    static readonly SemaphoreSlim concurrency = new(MaxConcurrency);
    static RateGate rateLimiter;
    static IReceivingRawEndpoint sender;
    static State state = State.Running;

    static TransportDefinition transportDefinition;
    static IQueueMetrics queueMetrics;

    static readonly CancellationTokenSource ShutdownCancellationTokenSource = new();
    static readonly TaskCompletionSource<object> ShutdownTcs = new();

    static async Task Main(string[] args)
    {
        SetupEnvironment();

        try
        {
            destination = args[0];
            isError = bool.Parse(args[1]);
            if (args.Length > 2) MaxQueueLength = int.Parse(args[2]);
            if (args.Length > 3) RateLimit = int.Parse(args[3]);
            if (args.Length > 4) MaxConcurrency = int.Parse(args[4]);
            if (args.Length > 5) BatchSize = int.Parse(args[5]);
            if (args.Length > 6) ConnectionString = args[6];
        }
        catch
        {
            Console.WriteLine($"""
                               FakeMessagGen.exe destination isError (maxQueueLength) (rateLimit) (maxConcurrency) (batchSize) (connectionstring)
                               
                                 destination: 
                                 
                                     Queue to send messages to.
                                 
                                 isError:
                                 
                                     true will generate fake error.
                                     false will generate fake audit.
                                 
                                 maxQueueLength: default {MaxQueueLength}
                                 
                                     Will pause seeding message when the queue length exceeds this limit.
                                 
                                 rateLimit: default {RateLimit}
                                 
                                     Will not generate more messages per second than this limit taking
                                     batch size into account.
                                                 
                                 maxConcurrency: default {MaxConcurrency}
                                 
                                     How many concurrency (batch) sends to allow
                                 
                                 batchSize: default {BatchSize}
                                  
                                     The batch size to use for each batch send operation
                                 
                                 connectionstring:
                                 
                                     The connection string to use for the destination.
                               
                                     By default the app will detect connection strings from the envvars and present a
                                     list of transport configuration to choose from when launched.
                                 
                                     Will probe the format to check if it can assume RabbitMQ, Azure Service Bus,
                                     or Learning transport.
                                     
                                     Azure Service Bus:

                                        Are expected to start with "Endpoint="
                               
                                     RabbitMQ:
                                     
                                        Are expected to start with "host="
                               
                                     Learning:
                                     
                                        Are expected to start with `/` (linux path) or contains `:\` (windows drive)

                               """);
            return;
        }

        CreateRateGate();

        if (!SetupTransport()) return;

        try
        {
            Console.WriteLine("\e[?1049h");

            using var f = InitFrames();

            Console.WriteLine($"""
                                        Using: {transportDefinition.GetType().Name}
                                    RateLimit: {RateLimit:N0}/s
                                    BatchSize: {BatchSize:N0}
                                  Destination: {destination}
                                      IsError: {isError}
                               MaxQueueLength: {MaxQueueLength:N0}
                               MaxConcurrency: {MaxConcurrency:N0}
                               """);


            LogManager.UseFactory(new FrameLoggerFactory(logFrame));
            AppDomain.CurrentDomain.UnhandledException += (o, ea) => main.WriteLine(Ansi.GetAnsiColor(ConsoleColor.Magenta) + DateTime.UtcNow + " UnhandledException: " + ((Exception)ea.ExceptionObject).Message + Ansi.Reset);
            AppDomain.CurrentDomain.FirstChanceException += (o, ea) => main.WriteLine(Ansi.GetAnsiColor(ConsoleColor.DarkCyan) + DateTime.UtcNow + " FirstChanceException: " + ea.Exception.Message);

            var senderConfig = RawEndpointConfiguration.CreateSendOnly(endpointName: "EndpointName", transportDefinition);

            var queueLengthTask = CheckQueue(ShutdownCancellationTokenSource.Token);

            sender = await RawEndpoint.Start(senderConfig);

            var sendLoopTask = SendLoop(ShutdownCancellationTokenSource.Token);

            Console.WriteLine("Press CTRL+C to exit...");
            await ShutdownTcs.Task;

            Console.WriteLine("Waiting max 5 seconds for running tasks to complete...");
            await Task.WhenAny(
                Task.Delay(TimeSpan.FromSeconds(5)),
                Task.WhenAll(sendLoopTask, queueLengthTask)
            );
        }
        finally
        {
            Console.Write("\e[?1049l\e[!p\e[m");
            Console.WriteLine("Fin!");
        }
    }

    static async Task SendLoop(CancellationToken token)
    {
        var activeTasks = new ConcurrentDictionary<Task, byte>();

        while (!token.IsCancellationRequested)
        {
            await rateLimiter.WaitAsync(token);
            await concurrency.WaitAsync(token);
            Metrics.BatchSizeAdd(BatchSize);

            async void Do()
            {
                var t = Send(isError);
                activeTasks.TryAdd(t, 0);
                await t;
                activeTasks.TryRemove(t, out _);
            }

            Do();
        }

        await Task.WhenAll(activeTasks.Keys);
    }

    enum Transports
    {
        AzureServiceBus,
        RabbitMQ,
        Learning
    }

    static bool SetupTransport()
    {
        var envvars = Environment.GetEnvironmentVariables();

        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            List<(string key, Transports type, string v)> transports = new();

            foreach (var e in envvars.Cast<DictionaryEntry>())
            {
                var k = (string)e.Key;
                var isConnectionString = k.Contains("CONNECTIONSTRING", StringComparison.OrdinalIgnoreCase);
                var v = (string)e.Value;
                if ((v.StartsWith("/") || v.Contains(":\\")) && isConnectionString) transports.Add((k, Transports.Learning, v));
                if (v.StartsWith("Endpoint")) transports.Add((k, Transports.AzureServiceBus, v));
                if (v.StartsWith("host=")) transports.Add((k, Transports.RabbitMQ, v));
            }

            if (transports.Count == 0)
                return false;

            Console.WriteLine("Select transport configuration:\n\n");

            for (int i = 0; i < transports.Count; i++)
            {
                var cyan = Ansi.GetAnsiColor(ConsoleColor.DarkCyan);
                Console.WriteLine($"  [{cyan}{i}{Ansi.Reset}] {transports[i].key}: {transports[i].type}");
            }

            Console.Write("\nOption: ");

            var x = int.Parse(Console.ReadLine());

            ConnectionString = transports[x].v;
            ConnectionString = Environment.ExpandEnvironmentVariables(ConnectionString);
        }

        if (!string.IsNullOrEmpty(ConnectionString))
        {
            if (ConnectionString.StartsWith("Endpoint"))
            {
                SetupAzureServiceBus();
            }
            else if (ConnectionString.StartsWith("/") || ConnectionString.Contains(":\\"))
            {
                SetupLearning();
            }
            else
            {
                SetupRabbitMQ();
            }

            return true;
        }


        var hasASB = envvars.Contains("CONNECTIONSTRING_AZURESERVICEBUS");
        var hasRMQ = envvars.Contains("CONNECTIONSTRING_RABBITMQ");
        var hasLearning = envvars.Contains("CONNECTIONSTRING_LEARNING");

        if (hasASB) Console.WriteLine(" [a] Azure ServiceBus");
        if (hasRMQ) Console.WriteLine(" [r] RabbitMQ");
        if (hasLearning) Console.WriteLine(" [l] Learning Transport");

        Console.WriteLine();

        var transportSelector = Console.ReadKey(true).KeyChar;

        if (hasASB && transportSelector == 'a') SetupAzureServiceBus();
        else if (hasRMQ && transportSelector == 'r') SetupRabbitMQ();
        else if (hasLearning && transportSelector == 'l') SetupLearning();
        else
        {
            Console.WriteLine($"Option {transportSelector} is not supported or not available");
            return true;
        }

        return false;

        static void SetupLearning()
        {
            transportDefinition = new LearningTransport
            {
                StorageDirectory = ConnectionString
            };
            queueMetrics = new LearningMetrics(ConnectionString);
        }

        static void SetupRabbitMQ()
        {
            transportDefinition = new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Quorum), ConnectionString);
            queueMetrics = RabbitMqMetrics.Parse(ConnectionString);
        }

        static void SetupAzureServiceBus()
        {
            transportDefinition = new AzureServiceBusTransport(ConnectionString);
            queueMetrics = new ServiceBusMetrics(ConnectionString);
        }
    }

    static void SetupEnvironment()
    {
        Console.OutputEncoding = Encoding.UTF8;
        var customCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        customCulture.DateTimeFormat.ShortDatePattern = "yyyy-MM-dd";
        customCulture.DateTimeFormat.LongTimePattern = "HH:mm:ss";
        CultureInfo.CurrentCulture = customCulture;
        CultureInfo.CurrentUICulture = customCulture;

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            ShutdownTcs.TrySetResult(null);
            ShutdownCancellationTokenSource.Cancel();
        };
    }

    static void CreateRateGate()
    {
        var batchesPerSec = RateLimit / BatchSize; // 6 (integer division)
        var windowMs = 1000.0 / batchesPerSec * RateLimit / BatchSize;
        var timeUnit = TimeSpan.FromMilliseconds(windowMs);
        rateLimiter = new(batchesPerSec, timeUnit);
    }

    static readonly FastArrayProvider<TransportOperation> _provider =
        new(BatchSize, () => CreateOperation());


    static async Task Send(bool isError)
    {
        var (array, length) = _provider.Rent();
        try
        {
            await sender.Dispatch(
                outgoingMessages: new TransportOperations(array),
                transaction: new TransportTransaction()
            );
        }
        catch (Exception ex)
        {
            Metrics.FaultsAdd(1);
            log.WriteLine(DateTime.UtcNow + " Send error: " + ex.Message);
        }
        finally
        {
            concurrency.Release();
            _provider.Return(array, length);
        }
    }

    static TransportOperation CreateOperation()
    {
        var msg = FakeMessageGenerator.Create(isError);
        var request = new OutgoingMessage(msg.id, headers: msg.headers, body: msg.body);
        var operation = new TransportOperation(request, new UnicastAddressTag(destination));
        return operation;
    }

    static async Task CheckQueue(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var queueLength = await queueMetrics.GetQueueLengthAsync(destination);
                var rates = Metrics.SendsAggregator.GetRates();
                queue.WriteLine(
                    $"{DateTime.Now} [queued: {queueLength,10:N0}" +
                    $" Rates/sec [now:{rates.CurrentPerSecond,8:F1}/s]" +
                    $" [10s:{rates.LastTenSecondsPerSecond,8:F1}/s]" +
                    $" [1min:{rates.LastMinutePerSecond,8:F1}/s]" +
                    $" [10min:{rates.LastTenMinutesPerSecond,8:F1}/s]" +
                    $" [1hr:{rates.LastHourPerSecond,8:F1}/s]" +
                    $" [lifetime:{rates.LifetimePerSecond,8:F1}/s]"
                );

                if (queueLength > MaxQueueLength)
                {
                    // Ensure paused
                    var result = InterlockedEx.CompareExchange(ref state, State.Paused, State.Running);

                    if (result == State.Running)
                    {
                        queue.WriteLine($"{State.Paused} until under {MaxQueueLength}");
                        for (var x = 0; x < MaxConcurrency; x++)
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
                        queue.WriteLine($"{State.Running} until above {MaxQueueLength}");
                        concurrency.Release(MaxConcurrency);
                    }
                }

                await Task
                    .Delay(QueryDelayInterval, cancellationToken)
                    .ContinueWith(t => Task.CompletedTask, TaskContinuationOptions.ExecuteSynchronously);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}