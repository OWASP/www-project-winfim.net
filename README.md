# WinFIM.NET
WinFIM.NET - File Integrity Monitoring For Windows

For a detailed introduction, please visit the [Cyber Security Corner](https://redblueteam.wordpress.com/2020/03/11/winfim-net-windows-file-integrity-monitoring/) technical blog.

# Introduction
There are plenty of commercial tools to do file integrity monitoring (FIM). But, not many freeware / Open Source options, especially for Windows.

A small application named [WinFIM.NET](https://github.com/OWASP/www-project-winfim.net/) has been developed to try to fill this gap.

# characteristics
The characteristics of this application are:

- Identify added / removed / modified files and directories since the previous run
- Select paths to monitor (files / directories)
- Path exclusions  (files / directories)
- File extension exclusions
- Can be launched as a console application or Windows service
- Logging is configurable, with the following defaults:
  - Logs to file 
  - Logs to the console
    - The file path and format is customisable (text, JSON)
    - Logs to the console (when the file `WinFIM Service.exe` is launched directly rather than as a windows service. 
    - Logs Windows Events
    - Customisable logging levels (verbose, debug, information, warning, error)
- Uses SHA256 for comparing file hashes

# Installing (local machine)
## Option 1: Via MSI / setup.exe installer
1. Double click the setup.exe or WinFIM.NET.Setup.msi file and follow the prompts to install WinFIM and setup the service
## Option 2: Manual install
1. Download the zip file to the destination computer and extract the contents
2. Unblock the `WinFIM.NET.Service.exe` (if required)
3. Launch Powershell as an administrator and run `.\Install-WindowsService.ps1`
  
# Installing (As a Docker container)
## Build Docker image from commandline
- Launch the Powershell file `Build-DockerImage.ps1` to build the docker file
- Requirements: Docker Desktop is installed on the host computer

# Configuation
1. Configure WinFIM.NET to suit your own environment
    1. `monlist.txt` – List the files files / directories (Absolute path) you want monitored, one per line in this file
    2. `exclude_path.txt` – put your exclusion list (Absolute path), one per line in this file
    3. `exclude_extension.txt` – list excluded file extensionsm one per line in this file (normally, these extensions should be related to some frequent changing files, e.g. *.log, *.tmp)
2. Configure Windows Event logs
   1. Windows Event logging is enabled by default. 
      1. To disable, edit the file `appsettings.json`
         1. Change the entry `IsLogToWindowsEventLog` to `false`
   2. If you want to log to Windows Event logs, make sure that the maximum log size is configured according to your deployment environment. By default, only 1MB is reserved for Windows Event logs.
   3. The Windows Event log file is located here: `%SystemRoot%\System32\Winevt\Logs\WinFIM.NET.evtx`
3. File and console level logs use the [Serilog](https://serilog.net/) logging library.
   1. The Serilog configuration is stored in the a text file called `appsettings.json`. To modify:
      1. Edit the file `appsettings.json`
         1. Review the `ConfigurationOptions.Timer` entry
           - This entry controls whether WinFIM.NET will be run in schedule mode or continuous mode. The value is in minutes.
            - Example: `0` = run continuously
            - Example: `30` = run every 30 minutes
         2. Review the entries in the "Serilog" section
            1. The log file setting is stored in the section `Serilog.WriteTo.[Name: File].Args.path".
               1. The default value is `".log"`, which means log to the current directory, with the filename `yyyymmdd.log` 
                 1. Example log file generated: `c:\tools\winfim.net\20221004.log` for a log file created on 4th October 2022, if WinFIM.NET is installed to the `c:\tools\winfim.net` directory
                 2. An example log value of: `c:\logs\fim.log` would save log files such as `c:\logs\fim20221004.log`
                 3. If no directory is specified and WinFIM.NET is run as a service, the logs will be generated in the `c:\windows\system32` folder
        3. More information about configuring Serilog settings are here:  https://github.com/serilog/serilog-settings-configuration
      3. The following Serilog plugins have been installed: 
         1. Serilog.Settings.Configuration - to read Serilog settings stored in appsettings.json
         2. Serilog.Sinks.Console - outputs log entries to the console
         3. Serilog.Sinks.File - outputs logs to a file
         4. Serilog.Expressions - customisable log formatting
         5. Serilog.Extensions.Hosting - routes framework log messages through Serilog
         5. Serilog.Enrichers.Environment - enriches logs, e.g. by adding the machine name to each log entry
4. Configuring the capture of remote connections
   1. WinFIM.NET can capture the current remote connections at the beginning of every file checking cycle.  
      When suspicious file changes are identified, this information may able to speed up the whole forensic / threat hunting process.
      1. This is disabled by default. To enable:
         1. Edit the file `appsettings.json`
         2. To enable, change the entry `IsCaptureRemoteConnectionStatus` to `true`

# Running as a Windows Service
- If the Windows service has been installed, WinFIM.NET will automatically start on system startup

# Running as a console application
- If the Windows service has not been installed, or if the Windows service is not started, launching the file `WinFIM.NET.Service.exe` or running the command `dotnet WinFIM.NET.Service.exe` will launch WinFIM.NET as a console application

# Running in Docker
``` powershell
# Run the container in detached mode, mounting the host operating system's c:\ drive as a readonly drive in the container's "c:\host" directory
docker run --name winfim --volume "C:\:C:\host:ro" -d --rm winfimnetservice:latest

# Run the container in interactive mode - you see the logs - if you press CTRL+C it stops and deletes the container
docker run --name winfim --volume "C:\:C:\host:ro" --rm -it winfimnetservice:latest

# connect to the container in interactive mode, with a Powershell prompt
docker exec -it winfim powershell

# View live logs
docker logs winfim --follow
```

# Uninstalling
## Option 1: Uninstall via the Add or Remove Programs in Control Panel
Locate the program "WinFIM.NET" and click Uninstall

## Option 2: Uninstall via the MSI installer
1. Run the MSI installer (or setup.exe file) and click "Remove WinFIM.NET"

## Option 3: Manually uninstall
If you manually installed WinFIM.NET:
- Launch Powershell as an administrator and then run `.\Uninstall-WindowsService.ps1`
- Optional: Delete the WinFIM.NET directory

# Logging
## Console logs
- If console logging is configured, logs are output to the console.
  - Logs can be in human friendly text format, or machine friendly JSON format (This is configurable in `appsettings.json`)
- If the application is run as a console application, it logs to "Standard Out", which is useful for capturing logs in containers (e.g. Docker, Kubernetes)

# File logs
- If file logging is configured, logs are saved in text files.
  - the default configuration is 1 file is saved per day in the same directory as the WinFIM.NET executable file, with the date format in `yyyymmdd` format.
  - The default format of logs saved to file is the machine friendly JSON format
  - This is configurable in `appsettings.json`

## Windows Event Log IDs
If Windows Event Logging is configured, These are the configured Windows event Log ID types:
- 7772 - Information - Service heartbeat message
- 7773 - Error
- 7776 - Warning - File / directory creation
- 7777 - Warning - File modification
- 7778 - Warning - File / directory deletion  

# Development notes
- Source code available in Github project [OWASP/www-project-winfim.net](https://github.com/OWASP/www-project-winfim.net)
- Targets the .NET 6 framework
- The current database is a SQLite 3 database, and is built if not exist on program startup
- Is currently built with Visual Studio 2022
- Uses the Visual Studio Addon [Microsoft Visual Studio Installer Projects](https://marketplace.visualstudio.com/items?itemName=VisualStudioClient.MicrosoftVisualStudio2022InstallerProjects) to build the MSI installer

## SQLite Database structure
- Filename: fimdb.db
- database type: SQLite version 3
- Tables:
  - BASELINE_PATH
    - Stores the details of paths that were checked in the previous run. At the end of the current run, the contents are deleted then copied from current_table
  - CONF_FILE_CHECKSUM
    - Stores checksums for the config files, e.g. monlist.txt, exclude_extension.txt, exclude_path.txt, monlist.txt
  - CURRENT_PATH
    - Stores the details of the the paths as they are being being checked, so it can be checked against the file details in the baseline_table. The contents are deleted at the end of the current run
  - VERSION_CONTROL
    - Stores the schema version and notes about changes
