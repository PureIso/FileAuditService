using FileAuditService.Core.Interfaces;

namespace FileAuditService.Core.Models
{
    public class AuditorSettings : IAuditorSettings
    {
        public string AuditDirectoryInput { get; set; }
        public string AuditDirectoryOutput { get; set; }
        public string HandleExecutablePath { get; set; }
        public string Filter { get; set; }
        public int InternalBufferSize { get; set; }
        public bool IncludeSubdirectories { get; set; }
    }
}
