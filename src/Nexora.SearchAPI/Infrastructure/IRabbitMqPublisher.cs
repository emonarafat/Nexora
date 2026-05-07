namespace Nexora.SearchAPI.Infrastructure;

public interface IRabbitMqPublisher
{
    Task PublishAsync<T>(string routingKey, T message);
}
