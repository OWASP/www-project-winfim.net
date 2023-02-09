# WinFIM.NET
WinFIM.NET - File Integrity Monitoring For Windows

For a detailed introduction, please visit the [Cyber Security Corner](https://redblueteam.wordpress.com/2020/03/11/winfim-net-windows-file-integrity-monitoring/) technical blog.

# Introduction
There are plenty of commercial tools to do file integrity monitoring (FIM). But, not many freeware / Open Source options, especially for Windows.

A small application named [WinFIM.NET](https://github.com/OWASP/www-project-winfim.net/) has been developed to try to fill this gap.

# characteristics
The characteristics of this application are:

- Identify added / removed / modified files and directories since the previous run
- The monitoring scope can be easily customized
- Path exclusion (e.g. sub-directory) could be configured
- File extension exclusion could be configured (e.g. *.bak, *.tmp, *.log, *.mdf, *.ldf, *.xel, *. installlog)
- Can be launched as a Windows service or as a console application
- Logging is configurable, with the following defaults:
  - Logs to Native Windows Events, which can integrate with existing log management mechanisms (e.g. Windows Event Subscription, Winlogbeat , nxlog, etc.)
  - Logs to file and the console with customisable logging levels (verbose, debug, information, warning, error)
    - The file path and format is customisable (text, JSON)
    - Logs to the console (when the file `WinFIM Service.exe` is launched directly rather than as a windows service. 
- Deployment friendly
- Uses SHA256 for hashing

# Installation (local machine)
## Option 1: Via MSI / setup.exe installer
1. Double click the setup.exe or WinFIM.NET-setup.msi file and follow the prompts to install WinFIM and setup the service
## Option 2: Manual install
1. Download the zip file to the destination computer and extract the contents
2. Unblock the `WinFIM.NET Service.exe` (if required)
3. In an Administrator command prompt, execute the file `install_service.bat`
  
# Installation (Docker)
- To Build and run the WinFIM.NET Docker image
- Requirements: Docker Desktop is installed on the host computer
## Build Docker image from Visual Studio
- Launch visual Studio
- Switch to the "Docker Compose" project
- Select the Release profile
- Click Build > Build Solution

## Build Docker image from commandline
To build the Docker image, from the compiled WinFIM directory:
- Edit Dockerfile
  - Replace the COPY command with: COPY  . C:\\Tools\WinFIM.NET
- Edit .dockerignore
  - replace the contents with: Dockerfile
- Run:
  ```
  docker build --tag winfim.net:latest . 
  ```

# Configuation
1. Configure WinFIM.NET to suit your own environment
    1. `monlist.txt` – put your in-scope monitoring files / directories (Absolute path) line by line under this file
    2. `exclude_path.txt` – put your exclusion (Absolute path) line by line under this file (the exclusion should be overlapped with the paths in `monlist.txt` (e.g. Sub-directory of the in-scope directory)
    3. `exclude_extension.txt` – put all whitelisted file extension (normally, those extensions should be related to some frequent changing files, e.g. *.log, *.tmp)
    4. `scheduler.txt` – This file is to control whether the WinFIM.NET will be run in schedule mode or continuous mode.
        - Put a number `0` to the file, if you want the WinFIM.NET keep running.
        - Put a number (in minute) for the time separation of each run. e.g. 30 (that means file checksum will be run every 30 minutes).
2. Configure Windows Event logs
   1. Windows Event logging is enabled by default. 
      1. To disable, edit the file `WinFIM.NET Service.exe.config` (Sourcecode filename: `App.config`
         1. Change the entry `is_log_to_windows_eventlog` to `False`
   2. If you want to log to Windows Event logs, make sure that the maximum log size is configured according to your deployment environment. By default, only 1MB is reserved for Windows Event logs.
   3. The Windows Event log file is located here: `%SystemRoot%\System32\Winevt\Logs\WinFIM.NET.evtx`
3. File and console level logs use the [Serilog](https://serilog.net/) logging framework.
   1. The Serilog configuration is stored in the a text file called the app.config file. To modify:
      1. Edit the file `WinFIM.NET Service.exe.config` (Sourcecode filename: `App.config`)
         1. Review the entries starting with `<add key="serilog:`
            1. Example: The log file location is defined in setting `<add key="serilog:write-to:File.path" value="c:\tools\WinFIM.NET\.log" />`
               1. Note that the date in `yyyymmdd` format is automatically inserted into the filename before the dot, e.g. `20221004.log`
      2. More information about configuring log settings are here:  https://github.com/serilog/serilog-settings-appsettings
      3. The following Serilog plugins have been installed: 
         1. Serilog.Settings.AppSettings - enables the Serilog settings to be stored in the .NET framework app.config file
         2. Serilog.Sinks.Console - outputs logs to the console when running the .exe file directly (rather than running as a service)
         3. Serilog.Sinks.File - outputs logs to a file
4. Configuring the capture of remote connections
   1. WinFIM.NET can capture the current remote connection status at the beginning of every file checking cycle.  
      When suspicious file changes are identified, this information may able to speed up the whole forensic / threat hunting process.
      1. This is disabled by default. To enable:
         1. Edit the file `WinFIM.NET Service.exe.config` (Sourcecode filename: `App.config`
         2. To enable, edit app.config file, change the entry `is_capture_remote_connection_status` to `True`

# Running
- If the Windows service has been installed, WinFIM.NET will automatically start on system startup
- If the Windows service has not been installed, or if it is not started, executing the file `WinFIM.NET Service.exe` will launch WinFIM.NET as a console application

# Running in Docker
``` powershell
# Run in interactive mode (show a Powershell prompt)
docker run --name winfim --volume "C:\:C:\host:ro" --rm -it winfim.net:latest powershell

# Run in interactive mode (show a Powershell prompt), with Internet access from a Windows host
docker run --name winfim --volume "C:\:C:\host:ro" --net "Default Switch" --rm -it winfim.net:latest powershell

# Run in interactive mode, viewing live logs
docker run --name winfim --volume "C:\:C:\host:ro" --rm -it winfim.net:latest
```

# Uninstallation
## Option 1: Via Add or Remove Programs
Locate the program "WinFIM.NET" and click Uninstall

## Option 2: via the MSI installer
1. Run the MSI installer (or setup.exe file) and click "Remove WinFIM.NET"

## Option 3: Manually uninstall
If you manually installed WinFIM.NET:
- Bring up an Administrator command prompt and navigate to the deployed folder, then execute `uninstall_service.bat`

# Windows Event Log IDs
Therse are the configured Windows event Log ID types:
- 7771 - remote connection status. Potentially useful for threat hunting if suspicious file changes are identified
- 7772 - service heartbeat message
- 7773 - errors
- 7776 - File / directory creation
- 7777 - File modification
- 7778 - File / directory deletion  

# Development notes
- Source code available in Github project [OWASP/www-project-winfim.net](https://github.com/OWASP/www-project-winfim.net)
- Targets the .NET 4.8 framework
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
