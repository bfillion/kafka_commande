using System;
using System.Threading;
using System.Threading.Tasks;
using kafka.commande.messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace kafka.commande.Services
{
    public class ServiceConsumer : BackgroundService
    {
        private readonly ILogger<ServiceConsumer> _logger;
        private readonly IKafkaService _kafkaService;

        public ServiceConsumer(ILogger<ServiceConsumer> logger, IKafkaService kafkaService)
        {
            _logger = logger;
            _kafkaService = kafkaService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.Register(() => _logger.LogInformation("Kafka Consumer service begining to stop at : {Now}", DateTime.Now));

            Task.Run(() => _kafkaService.ConsumeMessages(stoppingToken));
        }
    }
}
