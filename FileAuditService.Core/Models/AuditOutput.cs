using System;

namespace FileAuditService.Core.Models
{
    public class AuditOutput
    {
        public DateTime Timestamp { get; set; }
        public string User { get; set; }
        public int ProcessID { get; set; }
        public string AccessType { get; set; }

        public AuditOutput()
        {
            ProcessID = -1;
        }
    }
}
