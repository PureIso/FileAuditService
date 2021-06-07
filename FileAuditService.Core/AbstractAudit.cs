using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FileAuditService.Core.Interfaces;
using FileAuditService.Core.Models;
using Microsoft.Extensions.Logging;

namespace FileAuditService.Core
{
    public abstract class AbstractAudit : IAuditor
    {
        #region Fields
        private readonly ConcurrentQueue<AuditQueue> _auditQueue;
        private readonly ILogger<AbstractAudit> _logger;
        private readonly IAuditorSettings _auditorSettings;
        private FileSystemWatcher _fileSystemWatcher;
        private bool _running;
        #endregion

        #region Constructor
        protected AbstractAudit(ILogger<AbstractAudit> logger, IAuditorSettings auditorSettings)
        {
            _auditQueue = new ConcurrentQueue<AuditQueue>();
            _logger = logger;
            _auditorSettings = auditorSettings;
            _running = false;
        }
        #endregion

        #region Methods
        /// <summary>
        /// https://docs.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher?redirectedfrom=MSDN&view=net-5.0
        /// </summary>
        /// <returns></returns>
        public virtual bool Start()
        {
            try
            {
                _logger.LogInformation($"InternalBufferSize: {_auditorSettings.InternalBufferSize}");
                _logger.LogInformation($"Monitor Path: {_auditorSettings.AuditDirectoryInput}");
                _logger.LogInformation($"Filter: {_auditorSettings.Filter}");
                _logger.LogInformation($"IncludeSubdirectories: {_auditorSettings.IncludeSubdirectories}");
                if (string.IsNullOrEmpty(_auditorSettings.AuditDirectoryOutput))
                    throw new Exception("AuditDirectoryOutput is null or empty.");
                if (!Directory.Exists(_auditorSettings.AuditDirectoryOutput))
                    Directory.CreateDirectory(_auditorSettings.AuditDirectoryOutput);
                //Parallel execution
                Task.Factory.StartNew(AuditQueueHandler);
                _fileSystemWatcher = new FileSystemWatcher
                {
                    InternalBufferSize = _auditorSettings.InternalBufferSize,
                    Path = _auditorSettings.AuditDirectoryInput,
                    NotifyFilter = NotifyFilters.Attributes
                                   | NotifyFilters.CreationTime
                                   | NotifyFilters.DirectoryName
                                   | NotifyFilters.FileName
                                   | NotifyFilters.LastAccess
                                   | NotifyFilters.LastWrite
                                   | NotifyFilters.Security
                                   | NotifyFilters.Size,
                    Filter = _auditorSettings.Filter,
                    
                };
                _fileSystemWatcher.Changed += FileSystemWatcherQueue;
                _fileSystemWatcher.Created += FileSystemWatcherQueue;
                _fileSystemWatcher.Deleted += FileSystemWatcherQueue;
                _fileSystemWatcher.Renamed += FileSystemWatcherQueue;
                _fileSystemWatcher.Error += FileSystemWatcherOnError;
                _fileSystemWatcher.IncludeSubdirectories = _auditorSettings.IncludeSubdirectories;
                _fileSystemWatcher.EnableRaisingEvents = true;
                _running = true;
                return _running;
            }
            catch (Exception e)
            {
                DetailedExceptionHandler(e);
            }
            return _running;
        }

        private void FileSystemWatcherOnError(object sender, ErrorEventArgs e)
        {
            try
            {
                Exception exception = e.GetException();
                if (exception != null)
                    DetailedExceptionHandler(exception);
            }
            catch (Exception ex)
            {
                DetailedExceptionHandler(ex);
            }
        }

        public virtual bool Stop()
        {
            _running = false;
            _fileSystemWatcher.Changed -= FileSystemWatcherQueue;
            _fileSystemWatcher.Created -= FileSystemWatcherQueue;
            _fileSystemWatcher.Deleted -= FileSystemWatcherQueue;
            _fileSystemWatcher.Renamed -= FileSystemWatcherQueue;
            _fileSystemWatcher = null;
            return _running;
        }
        public virtual void FileSystemWatcherQueue(object sender, FileSystemEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(x =>
            {
                try
                {
                    _auditQueue.Enqueue(new AuditQueue
                    {
                        TimeStamp = DateTime.Now,
                        FullFilePath = e.FullPath,
                        Directory = Path.GetDirectoryName(e.FullPath),
                        FileName = e.Name,
                        AccessType = e.ChangeType
                    });
                }
                catch (Exception e)
                {
                    DetailedExceptionHandler(e);
                }
            });
        }
        public virtual void AuditQueueHandler()
        {
            AppDomain.CurrentDomain.DomainUnload += (s, e) => { _running = false; };
            try
            {
                while (_running)
                {
                    if (!_auditQueue.TryDequeue(out AuditQueue auditQueue) || !_running) 
                        continue;
                    string outputFileName = $"audit_{DateTime.Now.Date:ddMMyyyy}.txt";
                    string fullFilePath = Path.Combine(_auditorSettings.AuditDirectoryOutput, outputFileName);
                    if (!File.Exists(fullFilePath))
                        File.Create(fullFilePath).Close();
                    AuditOutput auditOutput = new AuditOutput
                    {
                        Timestamp = auditQueue.TimeStamp, 
                        AccessType = auditQueue.AccessType.ToString()
                    };
                    AuditOutput auditOutputHandleExecutable = HandleExecutableProcess(auditQueue);
                    if (auditOutputHandleExecutable != null && auditOutputHandleExecutable.ProcessID != -1)
                    {
                        auditOutput.ProcessID = auditOutputHandleExecutable.ProcessID;
                        auditOutput.User = auditOutputHandleExecutable.User;
                    }
                    else
                    {
                        //backup
                        AuditOutput auditOutputWin32 = Win32ProcessQuery(auditQueue.FileName);
                        if (auditOutputWin32 != null && auditOutputWin32.ProcessID != -1)
                        {
                            auditOutput.ProcessID = auditOutputWin32.ProcessID;
                            auditOutput.User = auditOutputWin32.User;
                        }
                    }
                    if (auditOutput.ProcessID == -1) 
                        continue;
                    string output = $"Detected: " +
                                    $"Timestamp: {auditOutput.Timestamp} " +
                                    $"User: {auditOutput.User} " +
                                    $"Process ID: {auditOutput.ProcessID} " +
                                    $"Access Type: {auditOutput.AccessType}";
                    _logger.LogInformation(output);
                    File.AppendAllText(fullFilePath, output + Environment.NewLine);
                }
            }
            catch (Exception e)
            {
                DetailedExceptionHandler(e);
            }
        }
        /// <summary>
        /// https://docs.microsoft.com/en-us/windows/win32/cimwin32prov/win32-process
        /// </summary>
        /// <param name="filename">The filename of the current file that is being processed</param>
        public virtual AuditOutput Win32ProcessQuery(string filename)
        {
            AuditOutput auditOutput = new AuditOutput();
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher($"select * from Win32_Process where CommandLine like '%{filename}%'");
                foreach (ManagementBaseObject managementBaseObject in searcher.Get())
                {
                    ManagementObject managementObject = (ManagementObject)managementBaseObject;
                    object processId = managementObject["ProcessId"];
                    if (processId != null && int.TryParse(processId.ToString(), out int intParseProcessId))
                        auditOutput.ProcessID = intParseProcessId;
                    object[] arguments = { string.Empty, string.Empty };
                    object invokeResultObject = managementObject.InvokeMethod("GetOwner", arguments);
                    if (invokeResultObject == null)
                        continue;
                    int invokeResultValue = Convert.ToInt32(invokeResultObject);
                    if (invokeResultValue != 0)
                        continue;
                    object computerName = arguments[1];
                    object userName = arguments[0];
                    if (computerName == null || userName == null)
                        continue;
                    string owner = computerName + "\\" + userName;
                    auditOutput.User = owner;
                    return auditOutput;
                }
            }
            catch (Exception e)
            {
                DetailedExceptionHandler(e);
            }
            return null;
        }
        public virtual AuditOutput HandleExecutableProcess(AuditQueue auditQueue)
        {
            AuditOutput auditOutput = new AuditOutput();
            try
            {
                string argument = $"/accepteula -a -u \"{auditQueue.Directory}\"";
                Process process = new Process
                {
                    StartInfo =
                    {
                        FileName = _auditorSettings.HandleExecutablePath,
                        Arguments = argument,
                        RedirectStandardOutput = true,
                    }
                };
                process.Start();
                string outputTool = process.StandardOutput.ReadToEnd();
                const string pattern = @"\s*pid: ([0-9]*)\s*type: ([^ ]*)\s*([^ ]*)\s*(.*?): (.*)";
                Regex regex = new Regex(pattern);
                MatchCollection matches = regex.Matches(outputTool);
                foreach (Match match in matches)
                {
                    string procId = match.Groups[1].Value;
                    //string type = match.Groups[2].Value;
                    string user = match.Groups[3].Value;
                    //string handle = match.Groups[4].Value;
                    string path = match.Groups[5].Value.Replace("\r\n", "").Replace("\r", "").Replace("\n", "");
                    if (path != auditQueue.FullFilePath)
                        continue;
                    auditOutput.ProcessID = int.Parse(procId);
                    auditOutput.User = user;
                }
                process.WaitForExit();
                return auditOutput;
            }
            catch (Exception e)
            {
                DetailedExceptionHandler(e);
            }
            return auditOutput;
        }
        public virtual void DetailedExceptionHandler(Exception ex)
        {
            while (true)
            {
                if (ex == null) 
                    return;
                _logger.LogError($"Message: {ex.Message} \r\nStacktrace: {ex.StackTrace}");
                ex = ex.InnerException;
            }
        }
        #endregion
    }
}
