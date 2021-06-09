# FileAuditService

Monitoring software that will log file access to specified directories.

## Core Features

Service will monitor access to specified directory.
The service will log the following information below if those information can be traced.

- Output
  - Timestamp
  - User
  - Process ID
  - Access Type

## TODO

- XML Comments
- Unit Tests
- Multiple directories
- Better Exception handling
- Parallel tasks for handle.exe and Win32_Process queries
- Publishing single, self contained executable

## Known Issues

- Services information logging is too slow taking roughly about 5 seconds in which the system that is modifying the file could have been closed. Although it is still possible to detect file change, the problem is that process is set to explorer.exe's pid.
- Minior inconvinience but Handle.exe has to be downloaded seperatly (The version is also important especially when dealing with x64)
- Some application loads into memory and closes handle so you might niss the handle. Possible solution is to monitor Win32_ProcessStartTrace

Example of Win32_ProcessStartTrace monitoring WINWORD.EXE (Placeholder for research)

```C#
            try
            {
                ManagementEventWatcher startWatch = new ManagementEventWatcher(
                    new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace WHERE ProcessName = WINWORD.EXE"));
                startWatch.EventArrived += new EventArrivedEventHandler(startWatch_EventArrived);
                startWatch.Start();

                ManagementEventWatcher stopWatch = new ManagementEventWatcher(
                    new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace WHERE ProcessName = WINWORD.EXE"));
                stopWatch.EventArrived += new EventArrivedEventHandler(stopWatch_EventArrived);
                stopWatch.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
```

Alternative Solution but with no content results:
Using Restart Manager Session to monitor registered resources.

```C#
[DllImport("rstrtmgr.dll", CharSet = CharSet.Auto, SetLastError = true)]
static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

[DllImport("rstrtmgr.dll", CharSet = CharSet.Auto, SetLastError = true)]
static extern int RmRegisterResources(uint pSessionHandle, UInt32 nFiles, string[] rgsFilenames, UInt32 nApplications, [In] RM_UNIQUE_PROCESS[] rgApplications, UInt32 nServices, string[] rgsServiceNames);
```

## Technologies

- [.Net 5 C#](https://devblogs.microsoft.com/dotnet/announcing-net-5-0-preview-1/) - (Language - Backend)
- [Handle.exe](https://docs.microsoft.com/en-us/sysinternals/downloads/handle) - (Handle is a utility that displays information about open handles for any process in the system)
- [Win32_Process class](https://docs.microsoft.com/en-us/windows/win32/cimwin32prov/win32-process) - (The Win32_Process WMI class represents a process on an operating system.)

## Logging

- [Serilog](https://serilog.net/) - (Structured Logging)

## Setup - Visual Studios

- Using visual studios build solution.

## Setup / Configuration

Example Below

```json
{
  "AuditorSettings": {
    "AuditInputDirectories": [ "C:\\Users\\user\\Desktop\\audit_directory_input", "C:\\Users\\user\\Desktop\\audit_directory_input2" ],
    "AuditOutputDirectory": "C:\\Users\\user\\Desktop\\audit_directory_output",
    "Filter": "*.txt",
    "InternalBufferSize": 65536,
    "HandleExecutablePath": "C:\\Users\\user\\Desktop\\handle64.exe",
    "IncludeSubdirectories": true
  },
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console" ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "C:\\Users\\user\\Desktop\\audit_logs\\log.txt",
          "rollingInterval": "Day",
          "outputTemplate": "{Timestamp} {Message}{NewLine:1}{Exception:1}"
        }
      }
    ],
    "Enrich": [
      "FromLogContext",
      "WithMachineName",
      "WithProcessId",
      "WithThreadId"
    ],
    "Properties": {
      "ApplicationName": "File Audit Service"
    }
  }
}
```

## License

[![License](http://img.shields.io/:license-mit-blue.svg?style=flat-square)](https://choosealicense.com/licenses/mit/)
