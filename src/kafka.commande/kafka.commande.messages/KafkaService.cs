using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avro.Generic;
using kafka.commande.messages.Schemas;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Microsoft.Extensions.Logging;

namespace kafka.commande.messages
{
    public class KafkaService : IKafkaService
    {
        //Contantes
        const string ENV_TOPICS = "topic_logs";
        const string ENV_PARTITION = "partition_logs";
        const string ENV_KAFKA = "kafka";
        const string ENV_ES = "es";
        const string ENV_VERIFICATION_DELAY = "verif_delay";
        const string ENV_CONSUMER_GROUPE_ID = "consumer_group_id";
        const string ENV_KAFKA_SCHEMA_REGISTRY_URL = "KAFKA_SCHEMA_REGISTRY_URL";

        private readonly IConfiguration _serviceConfig;
        private readonly List<string> _topics;
        private readonly SchemaRegistryConfig _schemaRegistryConfig;
        private readonly ILogger _logger;

        private string Kafka => _serviceConfig.GetSection(ENV_KAFKA).Value;
        private string Topics => _serviceConfig.GetSection(ENV_TOPICS).Value;
        private int Partition => Convert.ToInt32(_serviceConfig.GetSection(ENV_PARTITION).Value);
        private string ElasticSearch => _serviceConfig.GetSection(ENV_ES).Value;
        private int VerificationDelay => Convert.ToInt32(_serviceConfig.GetSection(ENV_VERIFICATION_DELAY).Value);
        private string ConsumerGroupId => _serviceConfig.GetSection(ENV_CONSUMER_GROUPE_ID).Value;


        public KafkaService(IConfiguration configuration, ILogger logger)
        {
            _serviceConfig = configuration;
            _logger = logger;
            _topics = new List<string> { Topics };
            _schemaRegistryConfig = new SchemaRegistryConfig
            {
                SchemaRegistryUrl = _serviceConfig.GetSection(ENV_KAFKA_SCHEMA_REGISTRY_URL).Value,
                SchemaRegistryRequestTimeoutMs = 5000,
                SchemaRegistryMaxCachedSchemas = 10
            };
        }


        public string ProduceMessage(int keyId)
        {

            var message = $"Producer value-{keyId}";
            var eventToSend = new Event
            {
                id = Guid.NewGuid().ToString(),
                date = DateTime.Now.ToString("u"),
                message = new Message
                {
                    header = new Schemas.Header
                    {
                        msg_id = Guid.NewGuid().ToString(),
                        msg_date = DateTime.Now.ToString("u")
                    },
                    Body = message
                }
            };

            try
            {
                var kafkaConfig = new ProducerConfig { BootstrapServers = Kafka };
                var prod = new ProducerBuilder<Key, Event>(kafkaConfig);
                prod.SetErrorHandler(ProducerErrorHandler);
                prod.SetLogHandler(ProducerLogHandler);

                using (var schemaRegistry = new CachedSchemaRegistryClient(_schemaRegistryConfig))
                {
                    prod.SetKeySerializer(new AvroSerializer<Key>(schemaRegistry));
                    prod.SetValueSerializer(new AvroSerializer<Event>(schemaRegistry));

                    using (var p = prod.Build())
                    {
                        try
                        {
                            var dr = p.ProduceAsync("logs", new Message<Key, Event> { Key = new Key { id = keyId }, Value = eventToSend }).GetAwaiter().GetResult();
                            _logger.LogInformation($"Kafka Producer service delivered the message '{message}' to Topic: {dr.Topic}");
                        }
                        catch (ProduceException<Key, Event> e)
                        {
                            _logger.LogInformation($"Kafka Producer service Delivery failed: {e.Error.Reason}");
                        }
                    }
                }
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogError(oce, "OperationCanceled - Error in delivered message");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error in delivered message");
                throw;
            }

            return $"The message '{message}' has been sent to the broker.";
        }



        public async Task ConsumeMessages(CancellationToken stoppingToken)
        {
            var kafkaConfig = new ConsumerConfig
            {
                // Note: The AutoOffsetReset property determines the start offset in the event
                // there are not yet any committed offsets for the consumer group for the
                // topic/partitions of interest. By default, offsets are committed
                // automatically, so in this example, consumption will only start from the
                // earliest message in the topic 'my-topic' the first time you run the program.
                AutoOffsetReset = AutoOffsetReset.Earliest,
                GroupId = ConsumerGroupId,
                BootstrapServers = Kafka
            };

            var consumer = new ConsumerBuilder<Key, Event>(kafkaConfig);
            consumer.SetErrorHandler(ConsumerErrorHandler);
            consumer.SetStatisticsHandler(ConsumerStatsHandler);

            using (var schemaRegistry = new CachedSchemaRegistryClient(_schemaRegistryConfig))
            {
                consumer.SetKeyDeserializer(new AvroDeserializer<Key>(schemaRegistry).AsSyncOverAsync());
                consumer.SetValueDeserializer(new AvroDeserializer<Event>(schemaRegistry).AsSyncOverAsync());

                using (var c = consumer.Build())
                {
                    c.Subscribe(_topics.FirstOrDefault());

                    Console.CancelKeyPress += (_, e) =>
                    {
                        e.Cancel = true; // prevent the process from terminating.
                    };

                    try
                    {
                        while (!stoppingToken.IsCancellationRequested)
                        {
                            try
                            {
                                var cr = c.Consume(stoppingToken);
                                if (!cr.IsPartitionEOF)
                                {
                                    ProcessMessage(cr);
                                }
                            }
                            catch (ConsumeException e)
                            {
                                _logger.LogError($"Error occured in Kafka Consumer service: {e.Error.Reason + Environment.NewLine + e.StackTrace}");
                            }

                            await Task.Delay(VerificationDelay, stoppingToken);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Ensure the consumer leaves the group cleanly and final offsets are committed.
                        c.Close();
                    }
                }
            }
        }


        private void ConsumerStatsHandler(IConsumer<Key, Event> consumer, string json)
        {
            _logger.LogInformation("Stats : {json}", json);
        }

        private void ConsumerErrorHandler(IConsumer<Key, Event> consumer, Error error)
        {
            var errorType = error.IsBrokerError ? "broker " : error.IsLocalError ? "local " : string.Empty;
            var message = $"Kafka {errorType}error : {error.Reason} for {consumer.Assignment}";
            _logger.LogError(message);
        }

        private void ProducerErrorHandler(IProducer<Key, Event> producer, Error error)
        {
            if (error.IsError)
            { _logger.LogInformation($"Kafka Producer service error : '{error.Reason}'"); }
        }

        private void ProducerLogHandler(IProducer<Key, Event> producer, LogMessage logMessage)
        {
            _logger.LogInformation($"Kafka Producer service log message: {logMessage.Level} - {logMessage.Message}");
        }

        private void ProcessMessage(ConsumeResult<Key, Event> consumeResult)
        {
            _logger.LogInformation($"Kafka Consumer service received the message '{consumeResult.Value.message.Body}' from '{consumeResult.TopicPartitionOffset}'.");
        }
    }
}
