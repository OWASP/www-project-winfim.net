# WinFIM.NET
WinFIM.NET - File Integrity Monitoring For Windows

For detail introduction, please visit my [Cyber Security Corner](https://redblueteam.wordpress.com/2020/03/11/winfim-net-windows-file-integrity-monitoring/) technical blog.

# Introduction
There are plenty of commercial tools to do file integrity monitoring (FIM). But, for freeware / Open Source, especially for Windows, it seems not much options.

I have developed a small Windows Service named [WinFIM.NET](https://github.com/redblueteam/WinFIM.NET) trying to fill up this gap.

# characteristics
The characteristics of this small application are:

- It will identify add / remove / modify of files and directories
- Monitoring scope could be easily customized
- Path exclusion (e.g. sub-directory) could be configured
- File extension exclusion could be configured (e.g. *.bak, *.tmp, *.log, *.mdf, *.ldf, *.xel, *. installlog)
- All the events are saved as native Windows Events, which could easily integrate with users’ existing log management mechanism (e.g. Windows Event Subscription, Winlogbeat , nxlog, etc.)
- Deployment friendly
- Using SHA256 for hashing

# Installation (single machine)
1. Manual download all files to destination computer
2. Configure the parameters to fill your own environment
    1. `monlist.txt` – put your in-scope monitoring files / directories (Absolute path) line by line under this file<br>
    2. `exclude_path.txt` – put your exclusion (Absolute path) line by line under this file (the exclusion should be overlapped with the paths in `monlist.txt’ (e.g. Sub-directory of the in-scope directory)<br>
    3. `exclude_extension.txt` – put all whitelisted file extension (normally, those extensions should be related to some frequent changing files, e.g. *.log, *.tmp)<br>
    4. `scheduler.txt` – This file is to control whether the WinFIM.NET will be run in schedule mode or continuous mode.<br>
        - Put a number `0’ to the file, if you want the WinFIM.NET keep running.
        - Put a number (in minute) for the time separation of each run. e.g. 30 (that means file checksum will be run every 30 minutes).
3. Unblock the `WinFIM.NET Service.exe` (if required)
4. Install the Windows Service
    - Bring up an Administrator command prompt and navigate to the deployed folder, then execute `install_service.bat`
5. Verify if the Windows Service is up and running
6. Please make sure maximum log size is configured according to your deployment environment. By default, it only reserves around 1MB for it.
    - `%SystemRoot%\System32\Winevt\Logs\WinFIM.NET.evtx`
  
# Uninstallation
  Bring up an Administrator command prompt and navigate to the deployed folder, then execute `uninstall_service.bat`
  
# Windows Event ID for file / directory changes
- Event ID 7776 – File / Directory creation
- Event ID 7777 – File modification
- Event ID 7778 – File / Directory deletion
  
Enjoy!
 
# Development notes
- Source code available in Github project [OWASP/www-project-winfim.net](https://github.com/OWASP/www-project-winfim.net)
- Targets the .NET 4.8 framework
- The .mdb is Database is a SQL Server Local DB
- Is currently built with Visual Studio

 Cheers
 
 Henry
