using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Serves a page with graphs of the metrics on <c>http://localhost:{port}/</c>, and the samples behind it as
/// JSON on <c>/api/samples</c>. The page is embedded as <c>index.html</c>.
/// </summary>
static class MetricsWeb
{
    public record Sample(
        DateTime Time,
        long Queued,
        string State,
        double Sends5s,
        double Sends1min,
        double Sends10min,
        double Sends1hr,
        double SendsLifetime,
        double SendsTotal,
        double Faults1min,
        double FaultsTotal);

    // One hour of samples at one sample per 2 seconds
    const int Capacity = 1800;

    static readonly Sample[] ring = new Sample[Capacity];
    static readonly object gate = new();
    static int count;
    static int next;
    static HttpListener listener;
    static string page;
    static object settings;

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Url { get; private set; }

    /// <summary>
    /// Starts the listener. Returns false when the port is in use or the listener cannot start; the tool then runs without the web view.
    /// </summary>
    public static bool TryStart(int port, object startupSettings, TextWriter log)
    {
        settings = startupSettings;
        Url = $"http://localhost:{port}/";

        try
        {
            listener = new HttpListener();
            listener.Prefixes.Add(Url);
            listener.Start();
        }
        catch (Exception e)
        {
            log.WriteLine($"Web view not started on {Url}: {e.Message}");
            listener = null;
            return false;
        }

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("index.html")!;
        using var reader = new StreamReader(stream);
        page = reader.ReadToEnd();

        _ = Task.Run(Serve);
        return true;
    }

    public static void Stop()
    {
        listener?.Close();
    }

    public static void Add(Sample sample)
    {
        lock (gate)
        {
            ring[next] = sample;
            next = (next + 1) % Capacity;
            if (count < Capacity) count++;
        }
    }

    /// <summary>
    /// Samples newer than <paramref name="after"/>, oldest first.
    /// </summary>
    static Sample[] SamplesAfter(DateTime after)
    {
        lock (gate)
        {
            var result = new Sample[count];
            var n = 0;

            for (var i = 0; i < count; i++)
            {
                var sample = ring[(next - count + i + Capacity) % Capacity];
                if (sample.Time > after) result[n++] = sample;
            }

            Array.Resize(ref result, n);
            return result;
        }
    }

    static async Task Serve()
    {
        while (listener is { IsListening: true })
        {
            HttpListenerContext context;

            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception)
            {
                // Listener closed
                break;
            }

            _ = Task.Run(() => Handle(context));
        }
    }

    static async Task Handle(HttpListenerContext context)
    {
        var response = context.Response;

        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";

            if (path == "/")
            {
                await Write(response, "text/html; charset=utf-8", page);
            }
            else if (path == "/api/samples")
            {
                var after = DateTime.MinValue;
                var since = context.Request.QueryString["after"];

                if (long.TryParse(since, out var ticks))
                {
                    after = new DateTime(ticks, DateTimeKind.Utc);
                }

                var body = JsonSerializer.Serialize(new { settings, samples = SamplesAfter(after) }, JsonOptions);
                await Write(response, "application/json", body);
            }
            else
            {
                response.StatusCode = 404;
                await Write(response, "text/plain", "Not found");
            }
        }
        catch (Exception)
        {
            // Client went away
        }
        finally
        {
            try { response.Close(); } catch { }
        }
    }

    static async Task Write(HttpListenerResponse response, string contentType, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        response.Headers["Cache-Control"] = "no-store";
        await response.OutputStream.WriteAsync(bytes);
    }
}
