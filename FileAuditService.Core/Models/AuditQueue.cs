using System;
using System.IO;

namespace FileAuditService.Core.Models
{
    public class AuditQueue
    {
        public DateTime TimeStamp { get; set; }
        public string FileName { get; set; }
        public string Directory { get; set; }
        public string FullFilePath { get; set; }
        public WatcherChangeTypes AccessType { get; set; }
    }
}
