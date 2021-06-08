using System.Collections.Generic;

namespace FileAuditService.Core.Interfaces
{
    /// <summary>
    /// Auditor settings interface
    /// </summary>
    public interface IAuditorSettings
    {
        List<string> AuditInputDirectories { get; set; }
        string AuditOutputDirectory { get; set; }
        string HandleExecutablePath { get; set; }
        string Filter { get; set; }
        int InternalBufferSize { get; set; }
        bool IncludeSubdirectories { get; set; }
    }
}