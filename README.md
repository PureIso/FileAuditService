# FileAuditService

Monitoring software that will log file access to specified directories.

## Core Features

Service will monitor specified directory and output the following on detection to a specifide output directory

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
- Service currently only accepts single directory
- Minior inconvinience but Handle.exe has to be downloaded seperatly

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
    "AuditDirectoryInput": "C:\\Users\\user\\Desktop\\audit_directory_input",
    "AuditDirectoryOutput": "C:\\Users\\user\\Desktop\\audit_directory_output",
    "Filter": "*.txt",
    "InternalBufferSize": 65536,
    "HandleExecutablePath": "C:\\Users\\user\\Desktop\\handle.exe"
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
