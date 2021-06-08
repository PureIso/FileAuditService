using System.Collections.Generic;
using FileAuditService.Core.Interfaces;

namespace FileAuditService.Core.Models
{
    public class AuditorSettings : IAuditorSettings
    {
        public List<string> AuditInputDirectories { get; set; }
        public string AuditOutputDirectory { get; set; }
        public string HandleExecutablePath { get; set; }
        public string Filter { get; set; }
        public int InternalBufferSize { get; set; }
        public bool IncludeSubdirectories { get; set; }
    }
}
