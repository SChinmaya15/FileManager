using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSystemMonitor.Core.Interfaces
{
    public interface IKafkaProducer
    {
        Task ProduceMessageAsync(string topic, string message);
    }
}
