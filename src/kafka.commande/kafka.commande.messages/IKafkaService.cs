using System.Threading;
using System.Threading.Tasks;

namespace kafka.commande.messages
{
    public interface IKafkaService
    {
        Task ConsumeMessages(CancellationToken stoppingToken);
        string ProduceMessage(int keyId);
    }
}