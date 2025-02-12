using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Confluent.Kafka;
using FileSystemMonitor.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace FileSystemMonitor.Core.Services
{
    public class KafkaProducerService : IKafkaProducer, IDisposable
    {
        private readonly IProducer<Null, string> _producer;
        private readonly string _topic;

        public KafkaProducerService(IConfiguration configuration)
        {
            var config = new ProducerConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"]
            };

            _producer = new ProducerBuilder<Null, string>(config).Build();
            _topic = configuration["Kafka:Topic"];
        }

        public async Task ProduceMessageAsync(string topic, string message)
        {
            await _producer.ProduceAsync(topic, new Message<Null, string>
            {
                Value = message
            });
        }

        public void Dispose()
        {
            _producer?.Dispose();
        }
    }
}
