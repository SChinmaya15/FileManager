using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSystemMonitor.Core.Models;

namespace FileSystemMonitor.Core.Interfaces
{
    public interface IFileActivityLogger
    {
        void LogActivity(FileActivity activity);
    }
}
