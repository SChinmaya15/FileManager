using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSystemMonitor.Core.Interfaces;
using FileSystemMonitor.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace FileSystemMonitor.Core.Services
{
    public class FileActivityLogger : IFileActivityLogger
    {
        private readonly string _logDirectory;
        private readonly object _lockObject = new object();

        public FileActivityLogger(IConfiguration congfiguration)
        {
            _logDirectory = congfiguration["MonitorSettings:MonitorPath"];//logDirectory;
            Directory.CreateDirectory(_logDirectory);
        }

        public void LogActivity(FileActivity activity)
        {
            var logFileName = $"watcherLog_{DateTime.Now:yyyyMMdd}.txt";
            var logPath = Path.Combine(_logDirectory, logFileName);

            var logEntry = $"{activity.Timestamp:yyyy-MM-dd HH:mm:ss.fff} | " +
                          $"Action: {activity.ChangeType} | " +
                          $"Path: {activity.FilePath} | " +
                          $"Size: {activity.FileSize} | " +
                          $"Modified: {activity.LastModified} | " +
                          $"Created: {activity.CreationTime} | " +
                          $"Attributes: {activity.FileAttributes}";

            lock (_lockObject)
            {
                File.AppendAllLines(logPath, new[] { logEntry });
            }
        }
    }
}
