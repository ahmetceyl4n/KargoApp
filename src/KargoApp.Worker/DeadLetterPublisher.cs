using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.Json;
using Volo.Abp.RabbitMQ;

namespace KargoApp.Worker;

// Kalıcı olarak işlenemeyen mesajı RabbitMQ'daki dead-letter exchange'ine gönderir.
// ABP'nin event bus'ı bunu yapmadığı için RabbitMQ'ya ABP'nin bağlantı havuzu üzerinden doğrudan yazıyoruz.
public class DeadLetterPublisher : ITransientDependency
{
    public const string ExchangeName = "KargoApp.DeadLetter";

    private readonly IConnectionPool _connectionPool;
    private readonly IJsonSerializer _jsonSerializer;

    public DeadLetterPublisher(IConnectionPool connectionPool, IJsonSerializer jsonSerializer)
    {
        _connectionPool = connectionPool;
        _jsonSerializer = jsonSerializer;
    }

    public async Task SendAsync<TEvent>(TEvent eventData, string reason)
    {
        var connection = await _connectionPool.GetAsync();
        await using var channel = await connection.CreateChannelAsync();

        var body = Encoding.UTF8.GetBytes(_jsonSerializer.Serialize(eventData!));
        var properties = new BasicProperties
        {
            Persistent = true,                       // diske yazılsın (delivery_mode: 2)
            ContentType = "application/json",
            Headers = new Dictionary<string, object?>
            {
                ["x-event-name"] = EventNameAttribute.GetNameOrDefault(typeof(TEvent)),   // "KargoApp.Scan"
                ["x-error"] = reason                                                       // neden buraya düştü
            }
        };

        // Dead-letter exchange'i fanout: routing key'e bakmadan bağlı kuyruğa dağıtır
        await channel.BasicPublishAsync(ExchangeName, routingKey: "", mandatory: false,
                                        basicProperties: properties, body: body);
    }
}
