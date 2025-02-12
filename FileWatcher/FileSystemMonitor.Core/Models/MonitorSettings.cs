namespace FileSystemMonitor.Core.Models
{
    public class MonitorSettings
    {
        public bool Enabled { get; set; } = true;
        public string MonitorPath { get; set; }
        public string LogPath { get; set; }
        public int BatchSize { get; set; } = 100;
        public int BatchTimeoutMs { get; set; } = 1000;
        public bool AutoRestartOnError { get; set; } = true;
    }
}
