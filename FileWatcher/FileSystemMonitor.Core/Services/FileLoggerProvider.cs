using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FileSystemMonitor.Core.Services
{
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _filePath;
        private readonly ConcurrentDictionary<string, FileLogger> _loggers = new ConcurrentDictionary<string, FileLogger>();

        public FileLoggerProvider(string filePath)
        {
            _filePath = filePath;
            Directory.CreateDirectory(_filePath);
        }

        public ILogger CreateLogger(string categoryName)
        {
            var logFileName = $"watcherLog_{DateTime.Now:yyyyMMdd}.txt";
            var logPath = Path.Combine(_filePath, logFileName);
            return _loggers.GetOrAdd(categoryName, name => new FileLogger(logPath));
        }

        public void Dispose()
        {
            _loggers.Clear();
        }
    }
}
