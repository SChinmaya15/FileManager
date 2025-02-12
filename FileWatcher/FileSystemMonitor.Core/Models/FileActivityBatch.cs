using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSystemMonitor.Core.Models
{
    public class FileActivityBatch
    {
        public List<FileActivity> Activities { get; } = new();
        public int BatchSize { get; private set; }
        public DateTime BatchStartTime { get; }

        public FileActivityBatch(int maxSize)
        {
            BatchSize = maxSize;
            BatchStartTime = DateTime.UtcNow;
        }

        public bool TryAdd(FileActivity activity)
        {
            if (Activities.Count >= BatchSize) return false;
            Activities.Add(activity);
            return true;
        }
    }

}
