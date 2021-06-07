using FileAuditService.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FileAuditService.Core
{
    public class Auditor : AbstractAudit
    {
        public Auditor(ILogger<AbstractAudit> logger, IAuditorSettings auditorSettings) : base(logger, auditorSettings)
        {
        }
    }
}
