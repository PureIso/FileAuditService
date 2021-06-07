namespace FileAuditService.Core.Interfaces
{
    public interface IAuditorSettings
    {
        string AuditDirectoryInput { get; set; }
        string AuditDirectoryOutput { get; set; }
        string HandleExecutablePath { get; set; }
        string Filter { get; set; }
        int InternalBufferSize { get; set; }
        bool IncludeSubdirectories { get; set; }
    }
}