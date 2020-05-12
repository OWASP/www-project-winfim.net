### WinFIM.NET Information
WinFIM.NET - File Integrity Monitoring For Windows

For detail introduction, please visit my <a href="https://redblueteam.wordpress.com/2020/03/11/winfim-net-windows-file-integrity-monitoring/">Cyber Security Corner</a> technical blog.

<b>#Introduction</b>
There are plenty of commercial tools to do file integrity monitoring (FIM). But, for freeware / Open Source, especially for Windows, it seems not much options.

I have developed a small Windows Service named [“WinFIM.NET”](https://github.com/OWASP/www-project-winfim.net/tree/master/WinFIM.NET) trying to fill up this gap.

<b>#characteristics</b>
The characteristics of this small application are:

<li>It will identify add / remove / modify of files and directories</li>
- Monitoring scope could be easily customized
- Path exclusion (e.g. sub-directory) could be configured
- File extension exclusion could be configured (e.g. *.bak, *.tmp, *.log, *.mdf, *.ldf, *.xel, *. installlog)
- All the events are saved as native Windows Events, which could easily integrate with users’ existing log management mechanism (e.g. Windows Event Subscription, Winlogbeat , nxlog, etc.)
- Deployment friendly
- Using SHA256 for hashing

<b>#Installation (single machine)</b><p>
  1) Manual download all files to destination computer
  2) Configure the parameters to fill your own environment
    a) ‘monlist.txt‘ – put your in-scope monitoring files / directories (Absolute path) line by line under this file<br>
    b) ‘exclude_path.txt‘ – put your exclusion (Absolute path) line by line under this file (the exclusion should be overlapped with the paths in ‘monlist.txt’ (e.g. Sub-directory of the in-scope directory)<br>
    c) ‘exclude_extension.txt‘ – put all whitelisted file extension (normally, those extensions should be related to some frequent changing files, e.g. *.log, *.tmp)<br>
    d) ‘scheduler.txt‘ – This file is to control whether the WinFIM.NET will be run in schedule mode or continuous mode.<br>
      -  Put a number ‘0’ to the file, if you want the WinFIM.NET keep running.
      -  Put a number (in minute) for the time separation of each run. e.g. 30 (that means file checksum will be run every 30 minutes).
  3) Unblock the “WinFIM.NET Service.exe”
  4) Install the Windows Service
    - Bring up an Administrator command prompt and navigate to the deployed folder, then execute “install_service.bat”
  5) Verify if the Windows Service is up and running
  6) Please make sure maximum log size is configured according to your deployment environment. By default, it only reserves around 1MB for it.
    - %SystemRoot%\System32\Winevt\Logs\WinFIM.NET.evtx
  
<b>#Uninstallation</b><p>
  Bring up an Administrator command prompt and navigate to the deployed folder, then execute “uninstall_service.bat”
  
<b>#Windows Event ID for file / directory changes</b><p> 
  Event ID 7776 – File / Directory creation<p>
  Event ID 7777 – File modification<p>
  Event ID 7778 – File / Directory deletion<p>

### Downloads or Social Links
* Developer's blog: [https://redblueteam.wordpress.com/](https://redblueteam.wordpress.com/)

### Change Log
* 2020-03-11 Initial release.

