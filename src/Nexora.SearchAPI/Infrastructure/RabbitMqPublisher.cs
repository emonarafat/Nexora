using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace Nexora.SearchAPI.Infrastructure;

[ExcludeFromCodeCoverage]
public sealed class RabbitMqPublisher(IConfiguration config, ILogger<RabbitMqPublisher> logger)
    : IRabbitMqPublisher, IAsyncDisposable
{
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task PublishAsync<T>(string routingKey, T message)
    {
        try
        {
            await EnsureConnectedAsync();
            if (_channel is null) return;
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
            var exchange = config["RabbitMQ:Exchange"] ?? "nexora.events";
            await _channel.BasicPublishAsync(exchange, routingKey, body);
        }
        catch (Exception ex) { logger.LogWarning(ex, "RabbitMQ publish failed: {Key}", routingKey); }
    }

    private async Task EnsureConnectedAsync()
    {
        if (_connection?.IsOpen == true && _channel?.IsOpen == true) return;
        await _lock.WaitAsync();
        try
        {
            if (_connection?.IsOpen == true && _channel?.IsOpen == true) return;
            var factory = new ConnectionFactory
            {
                HostName = config["RabbitMQ:Host"] ?? "localhost",
                Port = config.GetValue("RabbitMQ:Port", 5672),
                UserName = config["RabbitMQ:Username"] ?? "guest",
                Password = config["RabbitMQ:Password"] ?? "guest"
            };
            _connection = await factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();
            await _channel.ExchangeDeclareAsync(
                config["RabbitMQ:Exchange"] ?? "nexora.events",
                ExchangeType.Topic, durable: true);
        }
        finally { _lock.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
        _lock.Dispose();
    }
}
