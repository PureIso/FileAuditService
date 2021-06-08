using System;
using System.Collections.Generic;
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
        void FileSystemWatcherOnError(object sender, ErrorEventArgs e);
        void FileSystemWatcherOnOnRenamed(object sender, RenamedEventArgs e);
        List<AuditOutput> Win32ProcessQuery(AuditQueue auditQueue);
        List<AuditOutput> HandleExecutableProcess(AuditQueue auditQueue);
        void DetailedExceptionHandler(Exception ex);
    }
}
