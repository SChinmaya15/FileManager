using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FileSystemMonitor.Core.Interfaces;
using FileSystemMonitor.Core.Models;

namespace FileSystemMonitor.Core.Services
{
    public class FileSystemMonitorService : IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly IFileActivityLogger _logger;
        private readonly IKafkaProducer _kafkaProducer;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly BlockingCollection<FileActivity> _fileActivities;
        private readonly List<FileSystemWatcher> _watchers;
        private readonly ConcurrentDictionary<string, DateTime> _processedEvents;
        private readonly int _processorCount;
        private readonly int _batchSize;
        private readonly TimeSpan _batchTimeout;
        private readonly object _watcherLock = new object();

        public FileSystemMonitorService(
            string path,
            IFileActivityLogger logger,
            IKafkaProducer kafkaProducer,
            int batchSize = 100,
            int batchTimeoutMs = 1000)
        {
            _logger = logger;
            _kafkaProducer = kafkaProducer;
            _cancellationTokenSource = new CancellationTokenSource();
            _fileActivities = new BlockingCollection<FileActivity>();
            _watchers = new List<FileSystemWatcher>();
            _processedEvents = new ConcurrentDictionary<string, DateTime>();
            _processorCount = Environment.ProcessorCount;
            _batchSize = batchSize;
            _batchTimeout = TimeSpan.FromMilliseconds(batchTimeoutMs);

            InitializeWatchers(path);
            StartEventProcessors();
        }

        private void InitializeWatchers(string path)
        {
            // Create multiple watchers for better performance
            for (int i = 0; i < _processorCount; i++)
            {
                var watcher = new FileSystemWatcher(path)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName |
                                  NotifyFilters.DirectoryName | NotifyFilters.Size |
                                  NotifyFilters.Attributes | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = true,
                    InternalBufferSize = 65536 // 64KB buffer
                };

                watcher.Changed += OnFileSystemEvent;
                watcher.Created += OnFileSystemEvent;
                watcher.Deleted += OnFileSystemEvent;
                watcher.Renamed += OnFileSystemEvent;
                watcher.Error += OnWatcherError;

                _watchers.Add(watcher);
            }
        }

        private void StartEventProcessors()
        {
            // Start multiple consumer tasks for processing events
            for (int i = 0; i < _processorCount; i++)
            {
                Task.Factory.StartNew(
                    ProcessEventsWorker,
                    _cancellationTokenSource.Token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
            }

            // Start the batch processor
            Task.Factory.StartNew(
                ProcessBatchesWorker,
                _cancellationTokenSource.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        private async Task ProcessBatchesWorker()
        {
            var currentBatch = new FileActivityBatch(_batchSize);
            var batchTimer = Stopwatch.StartNew();

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    if (_fileActivities.TryTake(out FileActivity activity, 100, _cancellationTokenSource.Token))
                    {
                        if (!currentBatch.TryAdd(activity) || batchTimer.ElapsedMilliseconds >= _batchTimeout.TotalMilliseconds)
                        {
                            await ProcessBatchAsync(currentBatch);
                            currentBatch = new FileActivityBatch(_batchSize);
                            batchTimer.Restart();
                        }
                    }
                    else if (currentBatch.Activities.Count > 0 && batchTimer.ElapsedMilliseconds >= _batchTimeout.TotalMilliseconds)
                    {
                        await ProcessBatchAsync(currentBatch);
                        currentBatch = new FileActivityBatch(_batchSize);
                        batchTimer.Restart();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in batch processing: {ex.Message}");
                    await Task.Delay(1000, _cancellationTokenSource.Token); // Back off on error
                }
            }
        }

        private async Task ProcessBatchAsync(FileActivityBatch batch)
        {
            if (batch.Activities.Count == 0) return;

            try
            {
                // Process all activities in the batch
                foreach (var activity in batch.Activities)
                {
                    _logger.LogActivity(activity);
                }

                // Send batch to Kafka
                var batchJson = JsonSerializer.Serialize(batch.Activities);
               // await _kafkaProducer.ProduceMessageAsync("file-activities-batch", batchJson);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing batch: {ex.Message}");
            }
        }

        private void ProcessEventsWorker()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    if (_fileActivities.TryTake(out FileActivity activity, -1, _cancellationTokenSource.Token))
                    {
                        var key = $"{activity.FilePath}_{activity.ChangeType}";
                        var now = DateTime.UtcNow;

                        // Deduplicate events within a small time window
                        if (!_processedEvents.TryGetValue(key, out DateTime lastProcessed) ||
                            (now - lastProcessed).TotalMilliseconds > 100)
                        {
                            _processedEvents.AddOrUpdate(key, now, (_, _) => now);

                            try
                            {
                                EnrichFileActivity(activity);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error enriching file activity: {ex.Message}");
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in event processor: {ex.Message}");
                }
            }
        }

        private void EnrichFileActivity(FileActivity activity)
        {
            try
            {
                var fileInfo = new FileInfo(activity.FilePath);
                if (fileInfo.Exists)
                {
                    activity.FileSize = fileInfo.Length.ToString();
                    activity.LastModified = fileInfo.LastWriteTime.ToString();
                    activity.CreationTime = fileInfo.CreationTime.ToString();
                    activity.FileAttributes = fileInfo.Attributes.ToString();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error enriching file metadata: {ex.Message}");
            }
        }

        private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
        {
            try
            {
                var fileInfo = new FileInfo(e.FullPath);
                var activity = new FileActivity
                {
                    FilePath = e.FullPath,
                    ChangeType = e.ChangeType.ToString(),
                    Timestamp = DateTime.Now,
                    FileSize = fileInfo.Exists ? fileInfo.Length.ToString() : "N/A",
                    LastModified = fileInfo.Exists ? fileInfo.LastWriteTime.ToString() : "N/A",
                    CreationTime = fileInfo.Exists ? fileInfo.CreationTime.ToString() : "N/A",
                    FileAttributes = fileInfo.Exists ? fileInfo.Attributes.ToString() : "N/A"
                };

                _fileActivities.Add(activity);

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling file system event: {ex.Message}");
            }
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            Debug.WriteLine($"FileSystemWatcher error: {e.GetException().Message}");

            // Restart the watcher
            lock (_watcherLock)
            {
                var watcher = (FileSystemWatcher)sender;
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.EnableRaisingEvents = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error restarting watcher: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();

            foreach (var watcher in _watchers)
            {
                watcher.Dispose();
            }

            _fileActivities.Dispose();
            _cancellationTokenSource.Dispose();
        }
    }
}
