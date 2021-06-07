using System;
using System.IO;
using FileAuditService.Core.Models;

namespace FileAuditService.Core.Interfaces
{
    /// <summary>
    /// Auditor interface
    /// </summary>
    public interface IAuditor
    {
        bool Start();
        bool Stop();
        void AuditQueueHandler();
        void FileSystemWatcherQueue(object sender, FileSystemEventArgs e);
        AuditOutput Win32ProcessQuery(string filename);
        AuditOutput HandleExecutableProcess(AuditQueue auditQueue);
        void DetailedExceptionHandler(Exception ex);
    }
}
