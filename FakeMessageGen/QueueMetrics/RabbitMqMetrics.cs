using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using RabbitMQ.Client;

class RabbitMqMetrics : IQueueMetrics
{
    private readonly ConnectionFactory _factory;

    static string GetValueOrDefault(DbConnectionStringBuilder builder, string key, string defaultValue) => builder.TryGetValue(key, out var value) ? value.ToString() : defaultValue;

    public static RabbitMqMetrics Parse(string connectionString)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

        return new RabbitMqMetrics(
            GetValueOrDefault(builder, "host", "localhost"),
            GetValueOrDefault(builder, "username", "guest"),
            GetValueOrDefault(builder, "password", "guest"),
            GetValueOrDefault(builder, "virtualhost", "/"),
            int.Parse(GetValueOrDefault(builder, "port", "5672"))
        );
    }

    public RabbitMqMetrics(string hostName = "localhost", string userName = "guest", string password = "guest", string virtualHost = "/", int port = 5672)
    {
        _factory = new ConnectionFactory
        {
            HostName = hostName,
            UserName = userName,
            Password = password,
            VirtualHost = virtualHost,
            Port = port
        };
    }

    public async Task<long> GetQueueLengthAsync(string queueName)
    {
        await using var connection = await _factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        var data = await channel.QueueDeclarePassiveAsync(queueName);
        return data.MessageCount;
    }

    private static Dictionary<string, string> ParseRabbitMqConnectionString(string connectionString)
    {
        return connectionString
            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split(new[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length == 2)
            .ToDictionary(
                parts => parts[0].Trim().ToLowerInvariant(),
                parts => parts[1].Trim(),
                StringComparer.OrdinalIgnoreCase
            );
    }
}