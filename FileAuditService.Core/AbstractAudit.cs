using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FileAuditService.Core.Interfaces;
using FileAuditService.Core.Models;
using Microsoft.Extensions.Logging;

namespace FileAuditService.Core
{
    /// <summary>
    /// The core logic
    /// </summary>
    public abstract class AbstractAudit : IAuditor
    {
        #region Protected Property
        protected readonly ILogger<AbstractAudit> Logger;
        #endregion

        #region Fields
        private readonly ConcurrentQueue<AuditQueue> _auditQueue;
        private readonly IAuditorSettings _auditorSettings;
        private readonly List<CustomWatcher> _customWatchers;
        private bool _running;
        #endregion

        #region Constructor
        protected AbstractAudit(ILogger<AbstractAudit> logger, IAuditorSettings auditorSettings)
        {
            _auditQueue = new ConcurrentQueue<AuditQueue>();
            _customWatchers = new List<CustomWatcher>();
            Logger = logger;
            _auditorSettings = auditorSettings;
            _running = false;
        }
        #endregion

        #region Methods
        /// <summary>
        /// https://docs.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher?redirectedfrom=MSDN&view=net-5.0
        /// </summary>
        /// <returns>True if the service is running else false</returns>
        public virtual bool Start()
        {
            try
            {
                Logger.LogInformation($"InternalBufferSize: {_auditorSettings.InternalBufferSize}");
                Logger.LogInformation($"Filter: {_auditorSettings.Filter}");
                Logger.LogInformation($"IncludeSubdirectories: {_auditorSettings.IncludeSubdirectories}");
                if (string.IsNullOrEmpty(_auditorSettings.AuditOutputDirectory))
                    throw new Exception("AuditDirectoryOutput is null or empty.");
                if (!Directory.Exists(_auditorSettings.AuditOutputDirectory))
                    Directory.CreateDirectory(_auditorSettings.AuditOutputDirectory);
                foreach (string auditorSettingsAuditInputDirectory in _auditorSettings.AuditInputDirectories)
                {
                    Logger.LogInformation($"Monitor Path: {auditorSettingsAuditInputDirectory}");
                    CustomWatcher customWatcher = new CustomWatcher(
                        _auditorSettings.InternalBufferSize,
                        auditorSettingsAuditInputDirectory, 
                        _auditorSettings.Filter,
                        NotifyFilters.Attributes
                        | NotifyFilters.CreationTime
                        | NotifyFilters.DirectoryName
                        | NotifyFilters.FileName
                        | NotifyFilters.LastAccess
                        | NotifyFilters.LastWrite
                        | NotifyFilters.Security
                        | NotifyFilters.Size,
                        _auditorSettings.IncludeSubdirectories);
                    customWatcher.FileSystemWatcherQueueAction += FileSystemWatcherQueue;
                    customWatcher.FileSystemWatcherErrorAction += FileSystemWatcherOnError;
                    customWatcher.FileSystemWatcherOnRenamedQueueAction += FileSystemWatcherOnOnRenamed;
                    _customWatchers.Add(customWatcher);
                    customWatcher.Start();
                }
                _running = true;
                Task.Factory.StartNew(AuditQueueHandler);
                return _running;
            }
            catch (Exception e)
            {
                DetailedExceptionHandler(e);
            }
            return _running;
        }
        public virtual bool Stop()
        {
            foreach (CustomWatcher customWatcher in _customWatchers)
            {
                customWatcher.Stop();
            }
            _running = false;
            return _running;
        }
        public virtual void FileSystemWatcherOnError(object sender, ErrorEventArgs e)
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
        public virtual void FileSystemWatcherOnOnRenamed(object sender, RenamedEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(x =>
            {
                try
                {
                    if (e.FullPath.Contains("~$"))  //to avoid the corruption - microsoft office usually
                        return;
                    if (!File.Exists(e.FullPath))   //to avoid some temp files that need not be visible
                        return;                     //but happens with doc files.
                    _auditQueue.Enqueue(new AuditQueue
                    {
                        TimeStamp = DateTime.Now,
                        FullFilePath = e.FullPath,
                        Directory = Path.GetDirectoryName(e.FullPath),
                        FileName = Path.GetFileName(e.FullPath),    //subdirectories issue with invalid filename
                        AccessType = e.ChangeType
                    });
                }
                catch (Exception exception)
                {
                    DetailedExceptionHandler(exception);
                }
            });
        }
        public virtual void FileSystemWatcherQueue(object sender, FileSystemEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(x =>
            {
                try
                {
                    if (e.ChangeType == WatcherChangeTypes.Renamed)
                        return;
                    if (e.FullPath.Contains("~$"))  //to avoid the corruption - microsoft office usually
                        return;
                    if (!File.Exists(e.FullPath))   //to avoid some temp files that need not be visible
                        return;                     //but happens with doc files. 
                    _auditQueue.Enqueue(new AuditQueue
                    {
                        TimeStamp = DateTime.Now,
                        FullFilePath = e.FullPath,
                        Directory = Path.GetDirectoryName(e.FullPath),
                        FileName = Path.GetFileName(e.FullPath),    //subdirectories issue with invalid filename
                        AccessType = e.ChangeType
                    });
                }
                catch (Exception exception)
                {
                    DetailedExceptionHandler(exception);
                }
            });
        }
        public virtual void AuditQueueHandler()
        {
            AppDomain.CurrentDomain.DomainUnload += (s, e) => { _running = false; };
            while (_running)
            { 
                if (!_auditQueue.TryDequeue(out AuditQueue auditQueue) || !_running) 
                    continue;
                ThreadPool.QueueUserWorkItem(x =>
                {
                    try
                    {
                        string outputFileName = $"audit_{DateTime.Now.Date:ddMMyyyy}.txt";
                        string fullFilePath = Path.Combine(_auditorSettings.AuditOutputDirectory, outputFileName);
                        if (!File.Exists(fullFilePath))
                            File.Create(fullFilePath).Close();
                        //Faster
                        List<AuditOutput> auditOutputList = Win32ProcessQuery(auditQueue);
                        if (auditOutputList == null || !auditOutputList.Any())
                        {
                            //Alternative but slower
                            auditOutputList = HandleExecutableProcess(auditQueue);
                        }
                        if (auditOutputList == null || !auditOutputList.Any()) 
                            return;
                        foreach (AuditOutput auditOutput in auditOutputList)
                        {
                            if (auditOutput.ProcessID == -1)
                                continue;
                            string output = $"Detected: " +
                                            $"Timestamp: {auditOutput.Timestamp} " +
                                            $"User: {auditOutput.User} " +
                                            $"Process ID: {auditOutput.ProcessID} " +
                                            $"Access Type: {auditOutput.AccessType}";
                            Logger.LogInformation(output);
                            File.AppendAllText(fullFilePath, output + Environment.NewLine);
                        }
                    }
                    catch (Exception e)
                    {
                        DetailedExceptionHandler(e);
                    }
                });
            }
        }
        /// <summary>
        /// https://docs.microsoft.com/en-us/windows/win32/cimwin32prov/win32-process
        /// </summary>
        /// <param name="auditQueue"></param>
        public virtual List<AuditOutput> Win32ProcessQuery(AuditQueue auditQueue)
        {
            List<AuditOutput> auditOutputList = new List<AuditOutput>();
            string query = $"select * from Win32_Process where CommandLine like \"%{auditQueue.FileName}%\"";
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
                ManagementObjectCollection managementObjectCollection = searcher.Get();
                if (managementObjectCollection.Count > 0)
                {
                    foreach (ManagementBaseObject managementBaseObject in managementObjectCollection)
                    {
                        try
                        {
                            AuditOutput auditOutput = new AuditOutput();
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
                            auditOutput.Timestamp = auditQueue.TimeStamp;
                            auditOutput.AccessType = auditQueue.AccessType.ToString();
                            auditOutput.User = owner;
                            auditOutputList.Add(auditOutput);
                        }
                        catch (Exception e)
                        {
                            DetailedExceptionHandler(e);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Error Query: {query}");
                DetailedExceptionHandler(e);
            }
            return auditOutputList;
        }
        public virtual List<AuditOutput> HandleExecutableProcess(AuditQueue auditQueue)
        {
            //Words
            List<AuditOutput> auditOutputList = new List<AuditOutput>();
            try
            {
                if (string.IsNullOrEmpty(_auditorSettings.HandleExecutablePath))
                    return auditOutputList;
                string command = $"{_auditorSettings.HandleExecutablePath} /accepteula -u \"{auditQueue.FullFilePath}\"";
                string outputTool;
                ProcessStartInfo procStartInfo = new ProcessStartInfo("cmd", "/c " + command)
                {
                    RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
                };
                //release hProcess
                using (Process process = new Process())
                {
                    process.StartInfo = procStartInfo;
                    process.Start();
                    // Add this: wait until process does its work
                    process.WaitForExit();
                    // and only then read the result
                    outputTool = process.StandardOutput.ReadToEnd();
                }
                const string pattern = @"\s*pid: ([0-9]*)\s*type: ([^ ]*)\s*([^ ]*)\s*(.*?): (.*)";
                Regex regex = new Regex(pattern);
                MatchCollection matches = regex.Matches(outputTool);
                AuditOutput auditOutput = new AuditOutput();
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
                    auditOutput.Timestamp = auditQueue.TimeStamp;
                    auditOutput.AccessType = auditQueue.AccessType.ToString();
                    auditOutputList.Add(auditOutput);
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Error Path: {auditQueue.FullFilePath}");
                DetailedExceptionHandler(e);
            }
            return auditOutputList;
        }
        public virtual void DetailedExceptionHandler(Exception ex)
        {
            while (true)
            {
                if (ex == null) 
                    return;
                Logger.LogError($"Message: {ex.Message} \r\nStacktrace: {ex.StackTrace}");
                ex = ex.InnerException;
            }
        }
        #endregion
    }
}
