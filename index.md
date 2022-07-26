---

layout: col-sidebar
title: OWASP WinFIM.NET
tags: deployment-tag
level: 2
type: tool
pitch: File Integrity Monitoring For Windows

---

# WinFIM.NET
WinFIM.NET - File Integrity Monitoring For Windows

<b>#Introduction</b>
There are plenty of commercial tools to do file integrity monitoring (FIM). But, for freeware / Open Source, especially for Windows, it seems not much options.

A small Windows Service named [“WinFIM.NET”](https://github.com/OWASP/www-project-winfim.net/tree/master/WinFIM.NET) was developed trying to fill up this gap.

<b>#characteristics</b>
The characteristics of this small application are:

<li>It will identify add / remove / modify of files and directories</li>
<li>Monitoring scope could be easily customized</li>
<li>Path exclusion (e.g. sub-directory) could be configured</li>
<li>File extension exclusion could be configured (e.g. *.bak, *.tmp, *.log, *.mdf, *.ldf, *.xel, *. installlog)</li>
<li>All the events are saved as native Windows Events, which could easily integrate with users’ existing log management mechanism (e.g. Windows Event Subscription, Winlogbeat , nxlog, etc.)</li>
<li>Deployment friendly</li>
<li>Using SHA256 for hashing</li>

<b>#Installation (single machine)</b>

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
  
<b>#Uninstallation</b>
Bring up an Administrator command prompt and navigate to the deployed folder, then execute “uninstall_service.bat”


<b>#Windows Event ID for file / directory changes</b>
  <li>Event ID 7776 – File / Directory creation</li>
  <li>Event ID 7777 – File modification</li>
  <li>Event ID 7778 – File / Directory deletion</li>
  
 <br>Enjoy!
 
 Cheers<br>
 Henry
