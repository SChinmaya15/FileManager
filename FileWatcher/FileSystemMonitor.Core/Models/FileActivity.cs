using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSystemMonitor.Core.Models
{
    public class FileActivity
    {
        public string FilePath { get; set; }
        public string ChangeType { get; set; }
        public DateTime Timestamp { get; set; }
        public string FileSize { get; set; }
        public string LastModified { get; set; }
        public string CreationTime { get; set; }
        public string FileAttributes { get; set; }
    }
}
