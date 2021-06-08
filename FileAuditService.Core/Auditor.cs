using FileAuditService.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FileAuditService.Core
{
    /// <summary>
    /// The core logic but offers the capability of overriding the abstract class without messing with it.
    /// </summary>
    public class Auditor : AbstractAudit
    {
        public Auditor(ILogger<AbstractAudit> logger, IAuditorSettings auditorSettings) : base(logger, auditorSettings)
        {
        }
    }
}
