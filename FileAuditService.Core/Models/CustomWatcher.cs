using System.IO;

namespace FileAuditService.Core.Models
{
    public class CustomWatcher
    {
        #region Properties
        public FileSystemEventHandler FileSystemWatcherQueueAction { get; set; }
        public RenamedEventHandler FileSystemWatcherOnRenamedQueueAction { get; set; }
        public ErrorEventHandler FileSystemWatcherErrorAction { get; set; }
        #endregion

        #region Fields
        private FileSystemWatcher _fileSystemWatcher;
        private readonly int _internalBufferSize;
        private readonly string _monitorDirectory;
        private readonly string _filter;
        private readonly NotifyFilters _notifyFilters;
        private readonly bool _includeSubdirectories;
        #endregion
        public CustomWatcher(int internalBufferSize, string directory, string filter, NotifyFilters notifyFilter, bool includeSubdirectories)
        {
            _internalBufferSize = internalBufferSize;
            _monitorDirectory = directory;
            _filter = filter;
            _notifyFilters = notifyFilter;
            _includeSubdirectories = includeSubdirectories;
        }
        #region Methods
        public void Start()
        {
            _fileSystemWatcher = new FileSystemWatcher
            {
                InternalBufferSize = _internalBufferSize,
                Path = _monitorDirectory,
                NotifyFilter = _notifyFilters,
                Filter = _filter,
            };
            if (FileSystemWatcherQueueAction != null)
            {
                _fileSystemWatcher.Changed += FileSystemWatcherQueueAction;
                _fileSystemWatcher.Created += FileSystemWatcherQueueAction;
                _fileSystemWatcher.Deleted += FileSystemWatcherQueueAction;
                _fileSystemWatcher.Renamed += FileSystemWatcherOnRenamedQueueAction;
            }
            if (FileSystemWatcherErrorAction != null)
            {
                _fileSystemWatcher.Error += FileSystemWatcherErrorAction;
            }
            _fileSystemWatcher.IncludeSubdirectories = _includeSubdirectories;
            _fileSystemWatcher.EnableRaisingEvents = true;
        }
        public void Stop()
        {
            if (FileSystemWatcherQueueAction != null)
            {
                _fileSystemWatcher.Changed -= FileSystemWatcherQueueAction;
                _fileSystemWatcher.Created -= FileSystemWatcherQueueAction;
                _fileSystemWatcher.Deleted -= FileSystemWatcherQueueAction;
            }
            if (FileSystemWatcherErrorAction != null)
            {
                _fileSystemWatcher.Error -= FileSystemWatcherErrorAction;
            }
        }
        #endregion
    }
}
